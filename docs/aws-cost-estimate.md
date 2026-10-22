# 서울 리전 AWS 견적·구성 제안 — P3 준비

확인일: **2026-10-23 KST**. 사용자가 승인한 범위는 서울(`ap-northeast-2`) 기준 **견적·IaC 초안 작성, 리소스 생성 없음**이다. 월 예산·프로파일·실제 사양·보존/삭제 정책은 미정이다. 아래 값은 비교용 시나리오이며 승인된 정책·과금 상한이 아니다.

## 비교안

공통 가정: SQL Server 2022 Express Single-AZ RDS 1대, EC2 Linux x86_64 `t3.micro` SSM 중계 1대, RDS gp3 20 GiB·EC2 gp3 8 GiB를 한 달 보유, public IPv4 1개, RDS 관리형 master secret 1개. 730시간은 계산 관례이며 실제 달력 시간과 다르다. P5 API 배포 용량은 포함하지 않았다.

| 안 | RDS | 월 실행 시간 | 기본 합계 USD | 영향/권장 |
|---|---|---:|---:|---|
| A 최소 비용 후보 | db.t3.micro, 1 GiB | 730 | **39.52** | 기본요금 최소. 메모리 여유가 작고 CPU 크레딧 추가요금 가능. 성능 미검증 |
| B 여유 메모리 후보 | db.t3.small, 2 GiB | 730 | **62.15** | **권장 후보**: 메모리 여유를 늘린다. 실측에 의한 보장은 아니며 예산에 맞는지 사용자가 결정 |
| C 짧은 실습 예시 | db.t3.small, 2 GiB | 60 | **8.55** | 저장소·secret은 한 달 유지하고 compute/IPv4만 60시간이라는 가정. stop/start 운용이 필요하며 자동화·일정은 미승인 |

기본 합계는 **추가 CPU 크레딧·과금 백업/스냅샷·Secrets API·KMS 요청·전송·선택 서비스·세금·환율을 제외**한다. Free Tier/크레딧/약정/할인은 0으로 가정했다. 실제 계정의 무료 혜택과 기존 자원을 조사하지 않았으므로 적용을 보장하지 않는다. EC2도 `t3.small`로 키우면 730시간 기준 기본 합계에 $9.49가 더해진다.

## 단가와 산식

| 항목 | 공식 단가 USD | A 월 기준 | B 월 기준 |
|---|---:|---:|---:|
| RDS SQL Server Express Single-AZ | micro 0.031/h, small 0.062/h | 22.63 | 45.26 |
| EC2 Linux t3.micro On-Demand Shared/Used | 0.013/h | 9.49 | 9.49 |
| RDS gp3 20 GiB | 0.131/GB-month | 2.62 | 2.62 |
| EC2 EBS gp3 8 GiB | 0.0912/GB-month | 0.7296 | 0.7296 |
| 사용 중 public IPv4 1개 | 0.005/h | 3.65 | 3.65 |
| 관리형 master secret 1개 | 0.40/secret-month | 0.40 | 0.40 |
| 반올림 전 합계 | | 39.5196 | 62.1496 |

계산: `h × (RDS 시간 단가 + 0.013 + 0.005) + 20 × 0.131 + 8 × 0.0912 + 0.40`. [재계산 스크립트](../infra/scripts/estimate-aws-cost.py)는 저장한 공식 단가 발췌만 읽으며 AWS에 접속하지 않는다. [산출 결과](validation/p3-preparation/cost-scenarios.json), [SKU·원본 URL·게시 시각·SHA-256](validation/p3-preparation/pricing-extract.json)을 보존했다.

RDS는 2026-10-20 게시, EC2는 2026-10-22 게시 가격표를 사용했다. RDS의 `UnusedBox` 용량 예약 항목을 제외하고 `InstanceUsage`/Express/Single-AZ/License included를 선택했다. EC2는 Linux/Shared/NA/Used/OnDemand를 선택했다. 가격표에 SKU가 있다는 사실은 해당 **엔진 버전·계정·리전에서 주문 가능함을 보장하지 않는다**.

공식 출처: [서울 RDS 가격표 JSON](https://pricing.us-east-1.amazonaws.com/offers/v1.0/aws/AmazonRDS/current/ap-northeast-2/index.json), [서울 EC2/EBS 가격표 CSV](https://pricing.us-east-1.amazonaws.com/offers/v1.0/aws/AmazonEC2/current/ap-northeast-2/index.csv), [IPv4 요금](https://aws.amazon.com/vpc/pricing/), [Secrets Manager 요금](https://aws.amazon.com/secrets-manager/pricing/).

## 기본 합계 외 비용과 중지 후 잔존 요금

- **RDS T3 CPU**: SQL Server T3는 Unlimited이고 초과 크레딧은 **$0.144/vCPU-hour**다. 예를 들어 과금되는 초과 사용 100 vCPU-hour는 **$14.40 추가**다. 현재 RDS 소비량을 실측하지 않아 이를 0이라고 보장할 수 없다. [공식 CPU 크레딧 설명](https://aws.amazon.com/rds/sqlserver/pricing/).
- EC2 초안은 `standard` CPU credit 모드로 초과 credit 과금을 피하고 baseline 초과 시 throttling을 수용한다. API 배포 전 재검토한다.
- 과금되는 RDS 백업/스냅샷은 **$0.095/GB-month**다. 예를 들어 과금 대상 20 GB-month면 $1.90 추가다. 무료 할당·retained backup·최종 snapshot·보존 기간은 실제 구성과 계정 상태에 따라 달라진다. 이 비용을 기본 합계에 0으로 확정한 것이 아니다.
- secret API는 **$0.05/10,000 호출**. master 이외 app/test 계정 secret을 추가하면 secret 개수와 호출 요금도 추가된다. 초기 예시는 master secret 1개만 계산했다.
- AWS 관리형 KMS 키를 사용하는 초안이며 고객 관리형 키를 새로 만들지 않는다. KMS 요청·암호화 관련 사용량 요금은 별도 확인한다.
- EC2/RDS를 같은 AZ에 배치하는 초안이다. 다른 AZ 통신·외부 전송·무료 한도 초과는 별도이고 계정 전체 무료 전송량을 가정하지 않는다.
- Enhanced Monitoring/CloudWatch 로그 export·ECR·NAT Gateway·ALB·EIP·VPC Endpoint·Route53·고객 관리형 KMS 키는 생성 목록에 없다. 추가하면 별도 견적/승인이 필요하다.
- 기본 COST Budget 이메일 알림은 계정 범위 제안이다. Action/자동 중지/강제 지출 차단을 구현하지 않는다. 알림 지연과 기존 계정 지출이 있을 수 있으므로 **예산 값은 청구 상한이 아니다**. [AWS Budgets 요금/범위](https://aws.amazon.com/aws-cost-management/aws-budgets/pricing/).
- RDS는 최대 7일 연속 중지 후 자동 시작할 수 있다. 중지해도 저장소·백업은 남는다. EC2 중지도 EBS 비용을 없애지 않는다. C안의 60시간을 유지하려면 승인된 수동 운영 또는 별도로 요청한 자동화와 종료 계획이 필요하다. [RDS 중지 정책](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/USER_StopInstance.html).

## Terraform 초안과 아직 필요한 결정

초안은 `infra/terraform/`에 있다. 실제 AWS API plan/apply를 하지 않았고, 모의 provider의 plan-only 테스트만 수행했다.

| 항목 | 현재 상태/제안 | 실행 전 확인 |
|---|---|---|
| 리전 | 서울 기준 초안 준비 승인 | 실제 생성 승인에 포함 |
| 월 예산 | **미정** | USD 상한·AWS Budgets 수신자·계정/프로젝트 알림 범위 결정 |
| 계정·프로파일 | **미정, 계정 조회 안 함** | 정확한 CLI/SSO 프로파일·계정 확인, state에 들어갈 정보 보호 |
| RDS 사양·엔진 | A/B 후보, SQL Server 2022 Express | describe-orderable-db-instance-options로 exact version/class/gp3/암호화·용량/AZ 지원 확인 |
| EC2 | t3.micro SSM 중계 제안 | 사양·x86_64 AL2023 exact AMI·최소 EBS 크기·쿼터 확인 |
| 네트워크 | public subnet relay(인바운드 0), 2개 AZ private DB subnets | CIDR 충돌·AZ 가용성·아웃바운드 HTTPS/SQL 규칙 승인 |
| 비밀 | RDS 관리형 master secret 제안, EC2에 master 권한 없음 | 관리 접근 방식·app/test 최소 권한·추가 secret 비용/회전 정책 확정 |
| 백업·삭제 | 보존 일수 **미정**. 보호 ON·최종 snapshot 필수 제안 | 보존 일수/백업 범위/스냅샷·EBS 삭제/종료 시점 결정 |
| 삭제 보호 | prevent_destroy + deletion_protection | 삭제 시 별도 승인 후 보호 해제·스냅샷 확인; 승인 없는 자동 destroy 없음 |
| 패치 | major/minor 자동 업그레이드 비활성 제안 | 보안 패치 점검·적용 책임과 주기 확정; 장기 방치하지 않음 |
| 실제 배포/DB | **미실행** | 실DB 적용/테스트·EC2 API 배포 각각 승인 후 진행 |

다음 순서는 사양/예산/프로파일 확정 → 허용된 계정 읽기 전용 주문 가능성 조사 → 확정 입력으로 실제 plan·비용 재검토 → 별도 리소스 생성 승인 → apply이다. 지금의 준비 범위 선택은 생성·IAM 변경·알림 이메일 설정·DB 적용 승인이 아니다.

## 후속 실행 기록

이 문서는 최초 비용 비교 시점의 가정과 후보를 보존한다. 후속 사용자 승인 범위와 구현 변경은 ADR-010 이후의 결정을 따른다. 실제 기능/DB/배포 검증의 결과는 [실환경 보고서](aws-validation.md)에 구분해서 기록했다. 이 견적을 실제 청구 확정값이나 상시 운영 예산으로 간주하지 않는다.
