# P3 Terraform 초안 — 리소스 생성 미승인

서울 리전의 코드·검토용 초안이다. 사용자에게 승인받은 것은 견적·IaC 준비이며 AWS 리소스/IAM/Budget 이메일/DB를 생성·변경하지 않았다. 비용·미정 입력은 [견적과 결정 목록](../../docs/aws-cost-estimate.md)을 먼저 확인한다.

## 포함한 변경 제안

- 전용 VPC, public subnet 1개·IGW·relay route, 2개 AZ의 private DB subnets·외부 경로 없는 route table.
- 인바운드가 없는 SSM EC2(x86_64). 아웃바운드 HTTPS와 DB SG 1433만 허용. IMDSv2, 암호화 gp3, EC2 CPU `standard`.
- SQL Server 2022 Express Single-AZ private RDS. EC2와 같은 AZ. 강제 TLS, 암호화 storage, 고유 parameter/subnet group. Dev/Test 논리 DB는 이후 별도 승인된 SQL 관리 작업으로 생성하며 `db_name`을 지정하지 않는다.
- master password는 RDS가 Secrets Manager에서 생성/관리한다. Terraform이 비밀번호를 읽거나 `random_password`/평문 `password`를 state에 넣지 않는다. EC2 role은 SSM Core만 부여하고 master secret 접근·ECR·배포 권한은 추가하지 않는다.
- 계정 범위 월 COST Budget 실제/예측 100% 초과 이메일 알림 제안. 월 한도·수신자는 필수 입력이다. 알림은 강제 과금 차단이 아니다. 현재 알림 이메일을 설정하거나 보내지 않았다.
- RDS prevent_destroy/deletion protection, 최종 snapshot 필수·자동 백업 보존 제안. 백업 기간은 필수 입력. EC2 종료 시 root EBS 삭제는 학습용 초안 제안으로, 실제 삭제/보존 정책은 실행 전에 확정한다.

NAT Gateway/ALB/EIP/VPC Endpoint/SSH key pair/SSM 세션/사용자 생성 SQL/DB seed/API 배포는 포함하지 않는다. EC2에 public IPv4가 있어도 인바운드는 0개이며 주소 요금은 견적에 포함했다.

## 로컬 검증 (AWS 계정/자격증명 불필요)

Terraform 1.17.0과 AWS provider 6.70.0을 사용했다. 현재 Mac의 도구 경로:

```bash
export PATH="$HOME/.local/share/shooterrecords-terraform:$PATH"
./infra/scripts/verify-infra.sh
```

저장소 루트에서 실행한다. 스크립트는 public provider 다운로드를 위한 `init -backend=false`, fmt/validate와 **mock_provider + command=plan** 테스트만 실행한다. 실제 provider plan/apply/destroy를 실행하지 않는다. 테스트 fixture의 계정·AMI·엔진·예산·이메일은 명시적으로 가짜이며 배포 입력으로 사용하면 안 된다.

테스트는 private/암호화/Single-AZ·같은 AZ·SG 1433 참조·IMDSv2·삭제 보호·고정 저장소, 정적 force_ssl의 pending-reboot, review 누락·잘못된 AZ/엔진/예산 거부를 확인한다. provider 모의 값은 실제 AWS 네트워크/엔진/쿼터/IAM/순서 호환성을 입증하지 않는다. 추가 ingress/route/리소스 누락은 코드 리뷰도 병행해야 한다.

최초 테스트는 plan 시 computed ingress/route가 unknown이라 실패했다. 모의 생성값을 plan 단계에 제공하고 안전한 빈 기본값을 지정해 assertion을 유지한 채 수정했다. SQL Server `rds.force_ssl`은 공식 문서상 static이므로 `pending-reboot`를 사용한다.

## 실제 plan을 준비할 때

`terraform.tfvars.example`은 의도적으로 빈 값/null이며 바로 실행할 수 없다. 실행 설정으로 임의 숫자를 채우지 않는다. 비용·사양·보존/삭제·수신자를 결정한 뒤 gitignore되는 private tfvars나 보안 환경으로 주입한다. 계정 번호·이메일·자격정보를 커밋/로그에 남기지 않는다. AWS access key를 이 모듈에 입력하지 않는다.

`configuration_review_reference`는 확정된 후보 설정 검토 기록을 가리킨다. 빈 값이면 계획을 막지만, 이 문자열을 채운 것 자체는 **apply의 사용자 승인이나 보안 경계가 아니다**. `allowed_account_ids`로 실제 계정도 제한한다. 코드를 변경해 가드를 우회할 수 있으므로 반드시 사람이 비용·diff·대상을 검토한다.

계정/프로파일과 읽기 전용 범위를 먼저 확정하고 아래 항목을 조회한다. 현재 조회하지 않았으며 이 문서는 조회 결과가 아니다.

```bash
# APPROVED_* 값은 확인된 비공개 로컬 설정. 실제 값은 문서/로그에 출력하지 않는다.
aws --profile "$APPROVED_PROFILE" --region ap-northeast-2 rds describe-db-engine-versions \
  --engine sqlserver-ex \
  --query 'DBEngineVersions[?starts_with(EngineVersion, `16.00.`)].{Version:EngineVersion,Family:DBParameterGroupFamily}'
aws --profile "$APPROVED_PROFILE" --region ap-northeast-2 rds describe-orderable-db-instance-options \
  --engine sqlserver-ex --engine-version "$APPROVED_ENGINE_VERSION" \
  --db-instance-class "$APPROVED_RDS_CLASS" --license-model license-included \
  --query 'OrderableDBInstanceOptions[?StorageType==`gp3`].{Class:DBInstanceClass,Version:EngineVersion,MinStorage:MinStorageSize,MaxStorage:MaxStorageSize,Encrypted:SupportsStorageEncryption,AZs:AvailabilityZones[].Name}'
```

별도로 정확한 계정(내용은 로그 비공개), AZ·EC2 AMI(x86_64/SSM Agent/루트 최소 크기), RDS SQL Server managed secret/암호화/force_ssl 지원·쿼터·신규 계정 제약을 확인한다. 가격표 SKU/형식 검증은 이를 대신하지 않는다. AMI/엔진 버전을 latest로 자동 결정하지 않는다.

그 뒤 실제 plan을 생성/검토하고 사용자에게 생성 변경 목록·요금·정리 계획을 제시해 **별도 실행 승인**을 받는다. 현재 실제 plan·apply·destroy는 NOT_RUN이다. Terraform state/plan에는 비밀 참조·계정·내부 주소가 들어갈 수 있다. `.terraform`, state/backup, tfvars, plan은 Git에서 제외하며 실제 사용 전 저장·암호화·동시 잠금 방식을 확정한다. 이 초안에서 remote backend를 생성하지 않았다.

## 생성 이후의 별도 승인 작업

SSM Online/SG/private endpoint·암호화·TLS parameter 적용을 확인한다. 승인된 Dev/Test DB·최소권한 사용자를 만들고 Migration/seed/실DB 테스트를 수행한다. 관리 계정을 API에 넣지 않는다. [DB 검증 안내](../../docs/database-validation.md)와 [AWS runbook](../../docs/aws-deployment.md)을 따른다.

중지/삭제·최종 snapshot/retained backup/EBS/secret 보존과 종료 시점은 미정이다. 리소스 생성 승인 때 함께 정해야 하며, 승인 없이 자동 destroy/cleanup/주기 작업을 설정하지 않는다.
