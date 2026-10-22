resource "aws_iam_role" "relay" {
  depends_on  = [aws_vpc.lab]
  name_prefix = "${var.name_prefix}-ssm-"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Action    = "sts:AssumeRole"
      Principal = { Service = "ec2.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "ssm" {
  role       = aws_iam_role.relay.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_instance_profile" "relay" {
  name_prefix = "${var.name_prefix}-ssm-"
  role        = aws_iam_role.relay.name
}

resource "aws_instance" "relay" {
  ami                         = var.ec2_ami_id
  instance_type               = var.ec2_instance_type
  availability_zone           = var.availability_zones[0]
  subnet_id                   = aws_subnet.relay.id
  vpc_security_group_ids      = [aws_security_group.relay.id]
  associate_public_ip_address = true
  iam_instance_profile        = aws_iam_instance_profile.relay.name
  monitoring                  = false
  tags                        = { Name = "${var.name_prefix}-ssm-relay" }

  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 1
  }

  credit_specification {
    cpu_credits = "standard"
  }

  root_block_device {
    encrypted             = true
    volume_type           = "gp3"
    volume_size           = var.ec2_root_volume_gib
    delete_on_termination = true
  }

  depends_on = [aws_iam_role_policy_attachment.ssm, aws_route_table_association.relay]
}
