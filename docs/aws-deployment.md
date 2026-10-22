# AWS 구성 및 배포 지침 — RDS SQL Server + EC2

> 초안 리전 `ap-northeast-2` · **이 문서는 유료 리소스 생성 승인이 아니다** · 학습 목적으로 외부 공개 없는 private-first 구성

## 1. 선택한 기본 아키텍처

```text
                  [개발 단계 P0~P4]
Mac (.NET API) ── AWS CLI/SSM localPort 11433 ──▶ SSM 관리 EC2
                                                 │
                                      same VPC / DB SG 1433 only from EC2 SG
                                                 ▼
                                        RDS SQL Server 2022 Express
                                        PubliclyAccessible = false
                                                 ▲
                       [배포 단계 P5]             │
EC2 Linux x86_64 ── ASP.NET Core API Docker ──────┘
    ▲
    └── SSM app port forwarding (localhost:18080 → EC2:8080)
```

SQL Server는 **RDS**에서 실행하므로 Apple Silicon Mac의 SQL Server amd64 컨테이너 호환성 문제가 사라진다. Mac에는 `.NET 10 SDK`, AWS CLI, SSM plugin, 코드 편집기만 필요하다. 앱은 먼저 Mac에서 개발한 뒤 AWS EC2 Docker로 배포한다.

## 2. 채택/미채택 사유

| 안 | 평가 | 판단 |
|---|---|---|
| **RDS SQL Server Express + EC2(SSM)** | 관리형 백업/DB 패치와 실DB 테스트 가능; 컴퓨트·라이선스 포함 요금과 네트워크 설정 필요 | **기본 선택** |
| EC2 한 대에 SQL Server + API Docker | 구조·비용 단순할 수 있으나 DB 백업/운영 직접 책임, 충분한 메모리 필요 | 비용 우선 시 대안(사용자 재승인) |
| Azure SQL | 편리하지만 AWS에 올리겠다는 목표와 불일치 | 이번 범위 제외 |
| 퍼블릭 RDS 1433 개방 | 로컬 접근 쉬워도 공격면 과다 | 금지 |

RDS Express의 SQL Server 엔진 라이선스는 비용 모델에 포함될 수 있지만, **RDS 인스턴스·스토리지·백업·IPv4·EC2 비용이 무료라는 뜻은 아니다**. 특정 최저 사양/요금은 리전·일시·용량에 따라 바뀌므로 실제 콘솔/AWS Pricing Calculator의 견적을 사용자에게 제시한다.

## 3. AWS 작업 실행 전 사용자 승인 체크

Codex는 다음 확인값을 표로 제시하고 **명시적 승인이 있기 전에는 Terraform apply/리소스 생성/수정/삭제를 실행하지 않는다.**

- AWS 계정/프로파일(계정 번호/키는 문서·로그에 기록하지 않음), 리전 최종 확정.
- 월 예산 상한 및 AWS Budgets 알림 설정 계획. 견적에 RDS 시간 요금, 스토리지, 백업, EC2, EBS, 공용 IPv4, 네트워크 전송, 로그/Secrets Manager 비용 포함.
- RDS SQL Server 2022 Express 버전/가용 인스턴스 종류(실제 `describe-orderable-db-instance-options`/콘솔에서 확인), 규모와 자동 백업 보존 기간.
- EC2 인스턴스 유형/OS/역할/원격 관리 방식. 의도치 않은 NAT Gateway/ALB/Elastic IP 생성 금지.
- EC2와 RDS의 SG 규칙, 삭제/종료 및 재생성 전략, 이름과 태그.
- RDS master credential의 비밀 보관 방식, DB 사용자 및 테스트 권한 분리.
- 승인 대상 변경 diff와 예상 과금 발생 시간, 복구 계획.

## 4. 보안 네트워크 기본값

- VPC: public subnet의 SSM-managed EC2(외부로 AWS 서비스 접속 가능한 환경; 인바운드 0)와 **서로 다른 2개 가용 영역(AZ)의 private subnet으로 구성한 RDS DB subnet group**. RDS 인스턴스 자체는 학습용 Single-AZ로 시작한다. DB subnet group에는 최소 2개 AZ가 필요하다.
- RDS `PubliclyAccessible=false`; RDS SG inbound TCP 1433은 **EC2의 보안 그룹에서만**, 인터넷 CIDR 금지.
- EC2 inbound 22/1433/8080 포함 모두 닫음. SSM 연결은 instance role과 SSM Agent를 이용한다.
- EC2가 SSM 서비스를 사용할 수 있도록 outbound/network 경로를 마련한다. 저비용 실습에서는 Internet Gateway + public IPv4 경로도 가능하지만 IPv4 비용을 견적에 넣는다. NAT gateway/복수 VPC endpoint는 사용자 승인 없이는 추가하지 않는다.
- 계정 자격증명은 로컬 AWS SSO/CLI 자격방식으로 처리. IAM access key를 파일/코드/환경 예시에 실제 값으로 쓰지 않는다.
- RDS는 storage encryption 및 TLS 사용. 가능하면 `rds.force_ssl=1`, RDS CA/호스트명 인증서 검증으로 MITM 방어.
- DB master 계정은 초기 관리에만 사용. API는 최소권한 전용 SQL 로그인(사용자/DB 접근 분리).

## 5. Mac → private RDS 연결 (SSM 포트 포워딩)

전제: EC2가 SSM의 관리형 노드로 Online 상태, SSM Agent/로컬 Session Manager Plugin 설치, EC2 SG가 RDS 1433에 연결 가능, 필요한 IAM 권한 보유.

```bash
# 자리표시자는 실제 승인된 값으로 치환. 아래 명령 자체는 세션을 시작하므로 승인 후 실행.
aws ssm start-session \
  --region ap-northeast-2 \
  --target i-REPLACE_WITH_APPROVED_EC2_ID \
  --document-name AWS-StartPortForwardingSessionToRemoteHost \
  --parameters '{"host":["RDS_ENDPOINT_REPLACE"],"portNumber":["1433"],"localPortNumber":["11433"]}'
```

Mac 애플리케이션은 `Server=127.0.0.1,11433;Database=ShooterRecordsLabDev;...`로 연결한다. 연결 문자열/비밀번호는 `.NET user-secrets` 또는 로컬 전용 gitignore 파일을 통해 전달한다. **연결 암호화 + RDS 서버 인증서 검증을 유지**하고 포트 포워딩의 localhost 이름과 RDS 인증서 호스트명이 다를 때는 Microsoft.Data.SqlClient의 지원되는 hostname verification 옵션/인증서 CA 구성으로 해결한다. 개발 편의를 이유로 `TrustServerCertificate=True`를 기본값으로 고정하지 않는다. 실제 접속 코드에서 검증.

개발/테스트 DB는 별도 이름과 접근 계정을 사용한다. P3 DB 테스트는 RDS에서 실제로 실행해야 완료라고 기록한다.

## 6. AWS P3: DB 준비 순서

1. RDS, EC2, VPC, SG, IAM 최소구성을 **Terraform 코드로 작성**하고 `terraform fmt`/`validate`/`plan` 결과와 견적을 리뷰(이 단계에서 인프라 미생성).
2. 승인 후 IaC `apply` 및 RDS private endpoint/SSM 온라인 확인. 인프라 상태와 Terraform state는 민감 정보 취급.
3. RDS 관리 사용자로 `ShooterRecordsLabDev`, `ShooterRecordsLabTest`를 명시적으로 생성하고 앱/테스트 전용 권한 구성.
4. 마이그레이션 SQL 검토→승인된 Dev/Test DB에만 적용, 가상 데이터 seed(Dev/Test 전용).
5. SSM 경유 Mac 연결, CRUD/트랜잭션/고유 키/Stored Procedure/성능 기초 테스트, 실패 시 복구 가이드 작성.
6. 사용을 마친 리소스에 대해 중지/삭제 여부 사용자 확인. 승인 없이 자동 삭제·무한 유지 모두 금지.

### 개발비용 절감 유의

- RDS는 한 번 중지해도 최대 7일 후 자동 재시작할 수 있고 중지 중에도 저장소/백업 비용 등이 남는다.
- SQL Server Express의 엔진 무료 여부와 별개로 RDS 사용료 발생. EC2 정지도 EBS와 특정 네트워크 리소스 비용을 없애지 않는다.
- 각 단계 종료 시 실시간 리소스/비용/남은 위험을 표시. 방치된 DB를 막기 위해 명확한 정리 일정/방법을 사용자와 합의한다.

## 7. AWS P5: ASP.NET Core 컨테이너 배포

1. .NET 10 공식 SDK 이미지로 multi-stage 빌드, 런타임 이미지는 ASP.NET Core 10. 아키텍처 x86_64, 사용한 digest 또는 확정 버전 고정.
2. 컨테이너는 non-root/읽기 전용 구성을 검토하고 8080만 리슨. Docker host 바인드는 초기에는 `127.0.0.1:8080:8080`.
3. ECR private repository(승인 후 생성)에 이미지 업로드. EC2 IAM role로 ECR pull, 비밀관리 접근은 최소 권한.
4. RDS 연결 문자열/패스워드는 외부 관리형 Secrets Manager/SSM Parameter Store 등으로 주입. 컨테이너 이미지, Git, 사용자에게 보이는 shell history에 비밀 노출 금지.
5. EC2에서 API health/DB 연결 확인. 퍼블릭 인터넷 포트 개방 없이 다음과 같이 SSM으로 Mac에서 접속 테스트:

```bash
aws ssm start-session \
  --region ap-northeast-2 \
  --target i-REPLACE_WITH_APPROVED_EC2_ID \
  --document-name AWS-StartPortForwardingSession \
  --parameters '{"portNumber":["8080"],"localPortNumber":["18080"]}'

# 별도 터미널에서: curl -i http://127.0.0.1:18080/health/live
```

6. 롤백: 이전 확정 이미지로 재배포. DB Migration은 API 이미지 롤백만으로 되돌아가지 않을 수 있으므로 schema rollback/forward compatibility를 별도 설계.
7. 외부 공개가 필요하면 HTTPS 도메인/인증서/인그레스(ALB 또는 reverse proxy), 접근 제한, 인증, 레이트리밋과 비용을 **다시 승인**받는다.

## 8. 삭제/종료(runbook) — 사용자 승인 후에만

- 개발/테스트 DB 백업/스냅샷 보존이 필요한지 확인.
- EC2 API 컨테이너 중지, EC2 종료 또는 stop, RDS stop/delete, ECR 이미지, EBS 볼륨, IPv4 비용, 로그, Terraform state 등 잔존 리소스 확인.
- RDS delete 전에 명시적 최종 스냅샷/삭제 보호/테스트 데이터 회수 정책을 확인한다.
- 자격증명 회수/회전 및 접근 경로 폐쇄까지 검토.
- 과금 종료 여부는 Cost Explorer/Billing 등에서 사후 확인; 즉시 0원 된다고 단정하지 않는다.

## 9. 공식 참고자료

- SQL Server RDS 개요: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/CHAP_SQLServer.html
- RDS private 접속: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/CHAP_GettingStarted.CreatingConnecting.SQLServer.html
- RDS SQL Server 인스턴스 등급: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/SQLServer.Concepts.General.InstanceClasses.html
- RDS 일시 중지 제한: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/USER_StopInstance.html
- RDS SQL Server TLS: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/SQLServer.Concepts.General.SSL.Using.html
- SSM Session Manager 원격 호스트 포워딩: https://docs.aws.amazon.com/systems-manager/latest/userguide/session-manager-working-with-sessions-start.html
- AWS 가격 계산기: https://calculator.aws/

## 10. P3 준비 현황 — 2026-10-23

사용자는 서울 리전 **견적·IaC 초안 준비(자원 생성 없음)**를 선택했다. [견적·미정 결정 목록](aws-cost-estimate.md), [Terraform 초안 안내](../infra/terraform/README.md), [검증 결과](project-status.md)에 반영했다. 이 선택은 실제 계정 조회·plan/apply·IAM/Budget 알림·DB 변경·배포 승인이 아니다.

- 공식 공개 가격표로 기본 비용 시나리오만 산출했다. 월 예산/사양/프로파일/보존·삭제·알림 수신자는 미정이다.
- Terraform 로컬 init/fmt/validate와 mock provider plan-only 검증을 수행했다. 실제 AWS provider plan, 리소스 생성·변경·삭제, DB 접속·Migration 적용은 미실행이다.
- SQL Server `rds.force_ssl`은 **static**이므로 parameter group의 `apply_method = "pending-reboot"`를 사용한다. 생성/변경 뒤 실제 TLS 적용과 필요 재부팅을 별도 승인·검증해야 한다. [공식 SSL 지침](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/SQLServer.Concepts.General.SSL.Using.html).
- 실제 생성 전 해당 계정의 정확한 SQL Server 2022 build/class/gp3/암호화/AZ 주문 가능성, 선택 AMI의 x86_64·SSM Agent·EBS 최소 크기와 네트워크·쿼터를 확인한다. 가격표와 모의 테스트는 이를 보장하지 않는다.
- Terraform state/plan 보호·보관/암호화/잠금, 백업 기간·최종 snapshot/EBS/secret 정리·종료 시점을 확정한 뒤 계획을 검토하고 별도 실행 승인을 받는다.

## 11. 후속 실환경 검증

사용자가 확정한 검증 범위에서 EC2 Docker/private RDS/SSM·TLS·JWT·SQL 제약/동시성/SP·실행 계획·장애 복구를 실제 확인했다. [실환경 보고서](aws-validation.md)에 소스 버전과 결과를 기록했다. 위 초안/준비 섹션의 미실행 표시는 당시 상태이며, 실행 결과와 구분해서 읽는다. 실제 계정·주소·자원 식별자·키·원본 상태/계획/접속 로그는 공개하지 않는다.
