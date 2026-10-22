output "relay_instance_id" {
  value       = aws_instance.relay.id
  description = "SSM target, available only after separately approved creation."
}

output "rds_address" {
  value       = aws_db_instance.database.address
  description = "Private RDS address; use as HostNameInCertificate through the SSM tunnel."
}

output "master_secret_arn" {
  value       = aws_db_instance.database.master_user_secret[0].secret_arn
  sensitive   = true
  description = "Reference only. No password is read by Terraform or granted to the EC2 role."
}
