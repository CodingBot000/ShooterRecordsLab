resource "aws_vpc" "lab" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true
  tags                 = { Name = var.name_prefix }

  lifecycle {
    precondition {
      condition     = length(trimspace(var.configuration_review_reference)) > 0
      error_message = "Draft only: exact account/settings and a recorded configuration review are required. This check does not authorize apply."
    }
  }
}

resource "aws_subnet" "relay" {
  vpc_id                  = aws_vpc.lab.id
  availability_zone       = var.availability_zones[0]
  cidr_block              = cidrsubnet(var.vpc_cidr, 4, 0)
  map_public_ip_on_launch = false
  tags                    = { Name = "${var.name_prefix}-relay" }
}

resource "aws_subnet" "database" {
  for_each                = { for index, az in var.availability_zones : az => index }
  vpc_id                  = aws_vpc.lab.id
  availability_zone       = each.key
  cidr_block              = cidrsubnet(var.vpc_cidr, 4, each.value + 1)
  map_public_ip_on_launch = false
  tags                    = { Name = "${var.name_prefix}-db-${each.value}" }
}

resource "aws_internet_gateway" "relay" {
  vpc_id = aws_vpc.lab.id
}

resource "aws_route_table" "relay" {
  vpc_id = aws_vpc.lab.id
}

resource "aws_route" "relay_https_path" {
  route_table_id         = aws_route_table.relay.id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.relay.id
}

resource "aws_route_table_association" "relay" {
  subnet_id      = aws_subnet.relay.id
  route_table_id = aws_route_table.relay.id
}

resource "aws_route_table" "database" {
  vpc_id = aws_vpc.lab.id
  # No default route, IGW or NAT route for database subnets.
}

resource "aws_route_table_association" "database" {
  for_each       = aws_subnet.database
  subnet_id      = each.value.id
  route_table_id = aws_route_table.database.id
}

resource "aws_security_group" "relay" {
  name_prefix = "${var.name_prefix}-relay-"
  description = "SSM relay with zero inbound rules"
  vpc_id      = aws_vpc.lab.id
}

resource "aws_security_group" "database" {
  name_prefix = "${var.name_prefix}-db-"
  description = "Private SQL Server; access only from relay SG"
  vpc_id      = aws_vpc.lab.id
}

resource "aws_vpc_security_group_ingress_rule" "sql_from_relay" {
  security_group_id            = aws_security_group.database.id
  referenced_security_group_id = aws_security_group.relay.id
  from_port                    = 1433
  to_port                      = 1433
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "relay_sql" {
  security_group_id            = aws_security_group.relay.id
  referenced_security_group_id = aws_security_group.database.id
  from_port                    = 1433
  to_port                      = 1433
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "relay_https" {
  security_group_id = aws_security_group.relay.id
  cidr_ipv4         = "0.0.0.0/0"
  from_port         = 443
  to_port           = 443
  ip_protocol       = "tcp"
}
