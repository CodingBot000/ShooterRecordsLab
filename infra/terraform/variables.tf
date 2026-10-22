# No policy/cost inputs have defaults. Offline test fixtures are not deployment settings.
variable "aws_profile" {
  type        = string
  description = "Explicitly selected local AWS CLI/SSO profile. Never provide access keys here."
  validation {
    condition     = length(trimspace(var.aws_profile)) > 0
    error_message = "An approved profile is required; implicit/default credentials are not selected."
  }
}

variable "allowed_account_id" {
  type        = string
  sensitive   = true
  description = "Exact approved account, kept outside committed files/logs."
  validation {
    condition     = can(regex("^[0-9]{12}$", var.allowed_account_id))
    error_message = "An exact approved account is required."
  }
}

variable "configuration_review_reference" {
  type        = string
  default     = ""
  description = "Recorded review of candidate costs/account/settings before a real plan. Empty draft blocks planning; this reference NEVER authorizes apply."
}

variable "name_prefix" {
  type        = string
  description = "Approved resource naming prefix."
  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{2,29}$", var.name_prefix)) && !endswith(var.name_prefix, "-")
    error_message = "Use 3..30 lowercase letters/digits/hyphens, starting with a letter and not ending with a hyphen."
  }
}

variable "vpc_cidr" {
  type        = string
  description = "Approved non-overlapping IPv4 VPC CIDR, /16..20; subnets derive from this network."
  validation {
    condition     = can(cidrnetmask(var.vpc_cidr)) && can(regex("/(16|17|18|19|20)$", var.vpc_cidr))
    error_message = "Provide an approved IPv4 CIDR with /16..20 prefix."
  }
}

variable "availability_zones" {
  type        = tuple([string, string])
  description = "Two distinct approved Seoul AZs; actual account availability must be queried before use."
  validation {
    condition     = var.availability_zones[0] != var.availability_zones[1] && alltrue([for az in var.availability_zones : can(regex("^ap-northeast-2[a-z]$", az))])
    error_message = "Two different ap-northeast-2 availability zones are required."
  }
}

variable "ec2_ami_id" {
  type        = string
  description = "Exact approved Amazon Linux 2023 x86_64 AMI, resolved and inspected before the real plan."
  validation {
    condition     = can(regex("^ami-[0-9a-f]{17}$", var.ec2_ami_id))
    error_message = "Resolve and approve an actual AMI ID before planning."
  }
}

variable "ec2_instance_type" {
  type        = string
  description = "Cost-approved x86_64 SSM relay instance type; API capacity is re-evaluated in P5."
  validation {
    condition     = contains(["t3.micro", "t3.small"], var.ec2_instance_type)
    error_message = "Draft supports only the two priced x86_64 candidates; expand after separate cost review."
  }
}

variable "ec2_root_volume_gib" {
  type        = number
  description = "Approved encrypted gp3 root volume size, at least the approved AMI minimum."
  validation {
    condition     = var.ec2_root_volume_gib >= 8 && var.ec2_root_volume_gib == floor(var.ec2_root_volume_gib)
    error_message = "Use an integer of at least 8 GiB and check the selected AMI's minimum."
  }
}

variable "rds_instance_class" {
  type        = string
  description = "Priced candidate; exact engine/class/storage orderability remains unverified."
  validation {
    condition     = contains(["db.t3.micro", "db.t3.small"], var.rds_instance_class)
    error_message = "Draft supports only the priced micro/small candidates."
  }
}

variable "rds_engine_version" {
  type        = string
  description = "Exact SQL Server 2022 Express 16.00 build from approved read-only orderability discovery; no latest selection."
  validation {
    condition     = can(regex("^16\\.00\\.[0-9]+\\.[0-9]+\\.v[0-9]+$", var.rds_engine_version))
    error_message = "Select an exact RDS SQL Server 2022 16.00 build."
  }
}

variable "rds_storage_gib" {
  type        = number
  description = "Approved fixed gp3 allocation; storage autoscaling is disabled. Check actual orderable minimum."
  validation {
    condition     = var.rds_storage_gib >= 20 && var.rds_storage_gib == floor(var.rds_storage_gib)
    error_message = "Draft requires at least 20 GiB and exact orderable-option validation."
  }
}

variable "backup_retention_days" {
  type        = number
  description = "User-approved automated backup retention; mandatory, no assumed retention policy."
  validation {
    condition     = var.ephemeral_validation_enabled ? var.backup_retention_days == 0 : (var.backup_retention_days >= 1 && var.backup_retention_days <= 35 && var.backup_retention_days == floor(var.backup_retention_days))
    error_message = "Choose 1..35 days normally; explicitly approved ephemeral validation requires zero."
  }
}

variable "final_snapshot_identifier" {
  type        = string
  description = "Explicit approved final snapshot name for eventual deletion; deletion itself is separately authorized."
  validation {
    condition     = var.ephemeral_validation_enabled ? true : (can(regex("^[a-z][a-z0-9-]{2,62}$", var.final_snapshot_identifier)) && !endswith(var.final_snapshot_identifier, "-") && !strcontains(var.final_snapshot_identifier, "--"))
    error_message = "Provide a valid explicit final snapshot identifier."
  }
}

variable "monthly_budget_usd" {
  type        = number
  description = "User-approved monthly account COST budget in USD. Notification only, never a spending cap."
  validation {
    condition     = !var.enable_budget_notifications || var.monthly_budget_usd > 0
    error_message = "A user-approved positive monthly budget is required."
  }
}

variable "budget_alert_email" {
  type        = string
  sensitive   = true
  description = "User-selected recipient for AWS Budget actual/forecast alerts; no emails are sent by local validation."
  validation {
    condition     = !var.enable_budget_notifications || can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.budget_alert_email))
    error_message = "Specify the approved budget alert recipient outside committed files."
  }
}

variable "ephemeral_validation_enabled" {
  type        = bool
  default     = false
  description = "Only after explicit consent: disposable test data, no final snapshot/retained backups, deletion allowed."
}

variable "enable_budget_notifications" {
  type        = bool
  default     = true
  description = "Budget email requires an approved recipient; one-off validation can use an explicit local cost/time control instead."
}

variable "enable_container_deployment" {
  type        = bool
  default     = false
  description = "Create a private ECR repository and application-only secret reference for the EC2 container."
}
