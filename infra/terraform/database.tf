resource "aws_db_subnet_group" "database" {
  name_prefix = "${var.name_prefix}-"
  subnet_ids  = [for subnet in aws_subnet.database : subnet.id]
}

resource "aws_db_parameter_group" "database" {
  name_prefix = "${var.name_prefix}-"
  family      = "sqlserver-ex-16.0"

  parameter {
    name         = "rds.force_ssl"
    value        = "1"
    apply_method = "pending-reboot"
  }
}

resource "aws_db_instance" "database" {
  identifier                      = "${var.name_prefix}-sql"
  engine                          = "sqlserver-ex"
  engine_version                  = var.rds_engine_version
  license_model                   = "license-included"
  instance_class                  = var.rds_instance_class
  allocated_storage               = var.rds_storage_gib
  max_allocated_storage           = 0
  storage_type                    = "gp3"
  storage_encrypted               = true
  availability_zone               = var.availability_zones[0]
  multi_az                        = false
  publicly_accessible             = false
  port                            = 1433
  db_subnet_group_name            = aws_db_subnet_group.database.name
  vpc_security_group_ids          = [aws_security_group.database.id]
  parameter_group_name            = aws_db_parameter_group.database.name
  username                        = "lab_admin"
  manage_master_user_password     = true
  backup_retention_period         = var.backup_retention_days
  copy_tags_to_snapshot           = true
  delete_automated_backups        = false
  deletion_protection             = true
  skip_final_snapshot             = false
  final_snapshot_identifier       = var.final_snapshot_identifier
  apply_immediately               = false
  allow_major_version_upgrade     = false
  auto_minor_version_upgrade      = false
  performance_insights_enabled    = false
  monitoring_interval             = 0
  enabled_cloudwatch_logs_exports = []

  lifecycle {
    prevent_destroy = true
  }
}
