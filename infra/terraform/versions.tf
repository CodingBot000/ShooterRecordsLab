terraform {
  required_version = "~> 1.17.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "= 6.70.0"
    }
  }
}

provider "aws" {
  region              = "ap-northeast-2"
  profile             = var.aws_profile
  allowed_account_ids = [var.allowed_account_id]

  default_tags {
    tags = {
      Project   = "ShooterRecordsLab"
      Purpose   = "Learning"
      ManagedBy = "Terraform"
    }
  }
}
