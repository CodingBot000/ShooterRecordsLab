# Every run is plan-only with a mocked provider. These are fictitious fixtures,
# never approved resource IDs, engine builds, costs, recipients or policies.
mock_provider "aws" {
  override_during = plan
  mock_resource "aws_security_group" {
    defaults = { ingress = [], egress = [] }
  }
  mock_resource "aws_route_table" {
    defaults = { route = [] }
  }
}

variables {
  aws_profile                    = "offline-fixture"
  allowed_account_id             = "000000000000"
  configuration_review_reference = "OFFLINE-TEST-ONLY-NOT-AUTHORIZATION"
  name_prefix                    = "offline-lab"
  vpc_cidr                       = "10.42.0.0/16"
  availability_zones             = ["ap-northeast-2a", "ap-northeast-2c"]
  ec2_ami_id                     = "ami-00000000000000000"
  ec2_instance_type              = "t3.micro"
  ec2_root_volume_gib            = 8
  rds_instance_class             = "db.t3.micro"
  rds_engine_version             = "16.00.0000.0.v1"
  rds_storage_gib                = 20
  backup_retention_days          = 1
  final_snapshot_identifier      = "offline-final-snapshot"
  monthly_budget_usd             = 1
  budget_alert_email             = "offline@example.invalid"
}

run "draft_is_private_and_has_no_inbound_to_relay" {
  command = plan

  assert {
    condition     = aws_db_instance.database.availability_zone == aws_instance.relay.availability_zone && !aws_db_instance.database.publicly_accessible && !aws_db_instance.database.multi_az && aws_db_instance.database.storage_encrypted
    error_message = "Database must remain private, Single-AZ and encrypted."
  }

  assert {
    condition     = length(aws_subnet.database) == 2 && alltrue([for subnet in aws_subnet.database : !subnet.map_public_ip_on_launch])
    error_message = "Two private database subnets are required."
  }

  assert {
    condition     = aws_vpc_security_group_ingress_rule.sql_from_relay.security_group_id == aws_security_group.database.id && aws_vpc_security_group_ingress_rule.sql_from_relay.referenced_security_group_id == aws_security_group.relay.id && aws_vpc_security_group_ingress_rule.sql_from_relay.from_port == 1433 && aws_vpc_security_group_ingress_rule.sql_from_relay.to_port == 1433 && aws_vpc_security_group_ingress_rule.sql_from_relay.cidr_ipv4 == null && aws_vpc_security_group_ingress_rule.sql_from_relay.cidr_ipv6 == null
    error_message = "SQL ingress must use a referenced security group, never Internet CIDRs."
  }

  assert {
    condition     = length(aws_security_group.relay.ingress) == 0 && length(aws_route_table.database.route) == 0
    error_message = "Relay must have no inbound rules and database route table no Internet routes."
  }

  assert {
    condition     = aws_instance.relay.metadata_options[0].http_tokens == "required" && aws_instance.relay.root_block_device[0].encrypted && aws_instance.relay.credit_specification[0].cpu_credits == "standard"
    error_message = "IMDSv2/encrypted EBS/standard EC2 CPU credits are required."
  }

  assert {
    condition     = one(aws_db_parameter_group.database.parameter).name == "rds.force_ssl" && one(aws_db_parameter_group.database.parameter).value == "1" && one(aws_db_parameter_group.database.parameter).apply_method == "pending-reboot"
    error_message = "SQL Server force_ssl is static and must be applied with pending-reboot."
  }

  assert {
    condition     = aws_db_instance.database.manage_master_user_password && aws_db_instance.database.deletion_protection && !aws_db_instance.database.skip_final_snapshot && !aws_db_instance.database.delete_automated_backups && aws_db_instance.database.max_allocated_storage == 0
    error_message = "Managed credentials, deletion protection, preserved backups and fixed storage are required."
  }
}

run "missing_configuration_review_fails" {
  command = plan
  variables {
    configuration_review_reference = ""
  }
  expect_failures = [aws_vpc.lab]
}

run "same_database_az_fails" {
  command = plan
  variables {
    availability_zones = ["ap-northeast-2a", "ap-northeast-2a"]
  }
  expect_failures = [var.availability_zones]
}

run "wrong_engine_family_fails" {
  command = plan
  variables {
    rds_engine_version = "15.00.0000.0.v1"
  }
  expect_failures = [var.rds_engine_version]
}

run "unset_budget_fails" {
  command = plan
  variables {
    monthly_budget_usd = 0
  }
  expect_failures = [var.monthly_budget_usd]
}

run "explicit_ephemeral_validation_can_be_fully_cleaned" {
  command = plan
  variables {
    ephemeral_validation_enabled = true
    enable_container_deployment  = true
    enable_budget_notifications  = false
    backup_retention_days        = 0
    monthly_budget_usd           = 0
    budget_alert_email           = ""
    final_snapshot_identifier    = ""
  }
  assert {
    condition     = !aws_db_instance.database.deletion_protection && aws_db_instance.database.skip_final_snapshot && aws_db_instance.database.delete_automated_backups && aws_db_instance.database.backup_retention_period == 0
    error_message = "Explicit disposable validation must not retain database backups or snapshots."
  }
  assert {
    condition     = length(aws_budgets_budget.account) == 0 && aws_ecr_repository.application[0].force_delete && aws_secretsmanager_secret.application[0].recovery_window_in_days == 0
    error_message = "Explicit cleanup must cover images and the application secret, without unrequested email configuration."
  }
}
