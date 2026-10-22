resource "aws_ecr_repository" "application" {
  count                = var.enable_container_deployment ? 1 : 0
  name                 = "${var.name_prefix}-api"
  image_tag_mutability = "IMMUTABLE"
  force_delete         = var.ephemeral_validation_enabled
  encryption_configuration {
    encryption_type = "AES256"
  }
  image_scanning_configuration {
    scan_on_push = true
  }
  depends_on = [aws_vpc.lab]
}

resource "aws_secretsmanager_secret" "application" {
  count                   = var.enable_container_deployment ? 1 : 0
  name_prefix             = "${var.name_prefix}-api-"
  recovery_window_in_days = var.ephemeral_validation_enabled ? 0 : 7
  # Secret values are injected outside Terraform so no passwords enter state/plan.
  depends_on = [aws_vpc.lab]
}

resource "aws_iam_role_policy" "application" {
  count = var.enable_container_deployment ? 1 : 0
  name  = "application-only"
  role  = aws_iam_role.relay.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["ecr:GetAuthorizationToken"]
        Resource = "*"
      },
      {
        Effect   = "Allow"
        Action   = ["ecr:BatchGetImage", "ecr:GetDownloadUrlForLayer", "ecr:BatchCheckLayerAvailability"]
        Resource = aws_ecr_repository.application[0].arn
      },
      {
        Effect   = "Allow"
        Action   = ["secretsmanager:GetSecretValue"]
        Resource = aws_secretsmanager_secret.application[0].arn
      }
    ]
  })
}
