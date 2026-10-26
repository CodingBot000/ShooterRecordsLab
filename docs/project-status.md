# 프로젝트 진행 현황 — ShooterRecordsLab

## 현재 상태

- 현재 단계: 승인된 private EC2/RDS 범위의 실환경 검증·공개 보고서, Spring Boot 전환 설명서 완료. GitHub Actions CI·main 보호 설정 진행 중.
- 마지막 확인: 2026-10-26 KST — CI 구성 및 로컬 비DB 검증 갱신(실환경 검증 시각은 별도 보고서 기준).
- 현재 커밋: 저장소의 `git log`에서 확인(문서 자신이 포함된 커밋 SHA는 Git 이력을 기준으로 한다). 최초 소스 커밋 `eee0b6f`을 GitHub에서 clone해 검증했다.
- 환경: macOS 26.5 / Arm64 / SDK 10.0.402 / ASP.NET Core 10.0.13
- AWS: 실제 EC2 Docker·private RDS SQL Server 2022 검증 결과 확보. 공개 자료에는 계정·주소·자원 식별자·비밀·원본 state/로그를 포함하지 않는다.
- AWS 승인/예산: 사용자 승인된 사양과 1회 비용 관리 기준을 적용. 확정 청구액이나 월 과금 상한으로 간주하지 않는다.

## 진행표

| 단계 | 상태 | 근거 | 남은 일 |
|---|---|---|---|
| P0 문서/솔루션 | 완료 | 깨끗한 소스 스냅샷 restore/build/test/format 성공 | GitHub 실제 clone 검증 완료(아래 최초 공개 기록) |
| P1 HTTP/도메인 | 완료 | 단위 10 + HTTP 49, build/format 성공 | SQL 구현은 P2 |
| P2 EF/MSSQL | 완료(코드·비DB 게이트) | 단위 10 + 비DB 통합 71, SQL 생성·모델 drift 없음 | SQL 실행/동시성은 P3에서 실증 |
| P3 AWS RDS | 완료 | 실제 SQL Server 12개 테스트, TLS/FK/UNIQUE/CHECK/원자성/SP | 실환경 보고서 참조 |
| P4 보안/운영 | 완료(승인된 검증 범위) | JWT/권한·동시성·DB 장애·10,000행 실행 계획 | 외부 발급자·공개 서비스 운영은 별도 범위 |
| P5 배포 | 완료 | EC2 Docker·ECR 내용 hash·SSM API 검증·실패 배포 복구 | 실환경 보고서 참조 |
| P6 최종 검증 | 완료(학습 검증 범위) | Linux 전체 105 통과, 실제 HTTP 18개, 별도 자기 리뷰 | 독립된 제3자 감사·장기 SLA 검증은 아님 |

## P0 변경 및 검증 — 2026-10-22 23:56 KST

- `START_HERE.md` 매핑 11개를 새 대상에 복사. 기존 대상 없음. 루트 AGENTS와 운영 지침을 명시적으로 읽음. 원본 SHA-256: [init-sha256.json](validation/p0/init-sha256.json).
- Git 초기화, `.slnx`, 4개 소스/2개 xUnit 프로젝트, 지정된 참조 방향, `.editorconfig`, analyzer/nullable, `.gitignore`, NuGet 소스·정확 버전·lock, SDK 고정.
- Controllers/OpenAPI/ProblemDetails/JSON 로그 설정. 기본 liveness와 DB 미구성 readiness 분리. 샘플 WeatherForecast 제거.
- README 개발 사전 조건·실행·검증 안내 및 `infra/scripts/verify.sh` 추가.
- 변경 이유·패키지 이유/라이선스/대안: ADR-005.

| 직접 실행한 명령 | 결과 | 증거 |
|---|---|---|
| `dotnet --info` | PASS: SDK 10.0.402, runtime 10.0.13, osx-arm64 | [환경](validation/p0/dotnet-info.log) |
| `dotnet restore` | PASS: 6개 복원 | [복원](validation/p0/restore.log) |
| `dotnet build -warnaserror --no-restore` 최초 | FAIL: MSB4166, 자식 노드 종료 | [최초 실패](validation/p0/build-initial.log) |
| `dotnet build -warnaserror --no-restore -m:1 -nr:false` | PASS: 경고/오류 0 | [진단](validation/p0/build-serial.log) |
| `./infra/scripts/verify.sh docs/validation/p0` | PASS: restore/build/test/format | [빌드](validation/p0/build.log), [테스트](validation/p0/test.log), [format](validation/p0/format.log) |
| 깨끗한 소스 복사본에서 `bash infra/scripts/verify.sh artifacts/clean` | PASS: locked restore, 경고/오류 0, 단위 4 + HTTP 6 통과, 실패/skip 0, format 성공 | [전체 로그](validation/p0/clean-snapshot.log) |

- 실행 환경 조치: `MSBUILDDISABLENODEREUSE=1` 후 기본 병렬 빌드 성공. 최초 실패의 원인은 도구 호출 종료 후 남은 노드 재사용으로 추정하며 확정하지 않음.
- TRX 고정 파일명이 두 프로젝트에서 겹치는 경고를 발견해 스크립트에서 제거. 최종 검증은 콘솔 로그에 프로젝트별 개수를 기록한다.
- HTTP 검증: Development/Production liveness 200, DB 미구성 readiness 503 및 안전한 본문, Development OpenAPI 200/Production 404, 없는 경로 ProblemDetails/traceId.
- 구조 검증: 4계층 참조 방향, Domain/Application의 웹·EF 패키지 의존 없음.
- clean 검증은 bin/obj/.git가 없는 소스 복사본이며 실제 fresh clone이라고 주장하지 않는다. 최초 커밋이 없어 SHA/clone 검증은 미실행.
- 별도 자기 리뷰: PASS_WITH_RISKS. 계약/참조/환경 노출/비밀/README와 로그를 재검토했다. 구현자와 같은 세션의 별도 리뷰 패스로 독립 검증 한계가 있다. Critical/High 발견 없음. DB/인증/배포는 단계 범위 밖이며 미검증.
- 다음: P1 도메인·HTTP 계약 및 개발 전용 Fake, 실패·경계 테스트.

## 사용자 결정 필요

- 일회성 실환경 검증의 사양·비용 관리·대상·데이터 기준은 사용자가 확정했다. 상시 운영 월 예산·외부 공개·운영용 자격 정책은 별도 요구 시 결정한다.
- 검증용 JWT의 issuer/audience/scope를 고정해 실제 검증했다. 외부 인증 발급자 연동과 공개 운영 정책은 별도 범위이며 임의의 운영 인증서버를 만들지 않는다.

## 미검증·남은 위험

- 실제 SQL Server의 SQL/FK/UNIQUE/트랜잭션/SP/실행 계획은 후속 검증을 완료했다. 민감한 원본 대신 공개 허용 필드의 보고서를 제공한다.
- 공개 HTTPS·외부 OAuth/OIDC·장기 운영 가용성/SLA는 검증 범위 밖이다.
- 프론트엔드 구현 없음. AWS/IAM/DB 작업은 사용자가 승인한 실환경 검증 범위에 한정했다.

## P1 변경 및 검증 — 2026-10-23 00:02 KST

- Player/Match/MatchResult/Outcome, 통계 비율, PlayerService/MatchService, 저장소 추상화, 웹 DTO/Controllers, UTC converter, 안전한 오류 처리·요청 JSON 로그.
- F01/F02/F03/F04/F10 및 메모리 모사 멱등성 201/200/409 구현. Production에 Fake/미인증 쓰기 노출 없음. 명시적 `--seed-demo`만 메모리 가상 Player를 생성한다.
- 직접 실행: `dotnet build -warnaserror` 최초 FAIL(CS0104: 정규식 Match/도메인 Match 이름 충돌), 명시적 타입 alias 후 PASS. [최초 빌드](validation/p1/build-initial.log), [수정 후](validation/p1/build-fixed.log).
- 주의: 최초 빌드 실패 후 실행된 `dotnet test --no-build`는 P0 바이너리 10건을 실행했으므로 P1 증거에서 제외한다([이전 바이너리 로그](validation/p1/test-initial.log)). 빌드 성공과 연결한 재검증부터 P1 결과로 집계한다.
- `dotnet format --no-restore` 후 `./infra/scripts/verify.sh docs/validation/p1`: PASS. locked restore, build 경고/오류 0, 단위 10 + HTTP 49 = 59 통과, 실패/skip 0, format 확인 성공. [빌드](validation/p1/build.log), [테스트](validation/p1/test.log), [format](validation/p1/format.log).
- 검증: 정상/미존재/잘못된 UUID, 빈 컬렉션, 통계와 반올림, UTC 명시·미래 5분 경계, null/누락/잘못된 JSON, 수치·외부 ID 길이·참가자 1/2/20/21명, 참가자 미존재·중복·결과 모순, 재시도 순서 무관/충돌, Location 조회, 안정 정렬·큰 page, 취소 전달, 안전한 500, Production 쓰기 비노출.
- Kestrel 실제 프로세스 실행: `dotnet run --no-build --project src/ShooterRecordsLab.Api --launch-profile http -- --seed-demo`; loopback live/ready/OpenAPI/seed Player 응답을 확인. [실행 로그](validation/p1/kestrel.log), [스모크 결과](validation/p1/kestrel-smoke.log).
- 별도 자기 리뷰 PASS_WITH_RISKS: 로그 status가 예외 처리보다 먼저 기록되는 문제를 발견해 래퍼 순서를 수정했다. 정규식 ASCII·문화권, UUID/시간 canonicalization, 취소·원자적 모사, 개발 외 경로 노출을 재검토. 같은 세션으로 독립성 한계 있음.
- 남은 위험: Fake는 DB 제약/트랜잭션·동시성·LINQ 번역 증거가 아니다. JWT·SQL·배포 미검증. 실제 DB 통합 테스트 NOT_RUN.
- 다음: P2 EF Core SQL Server 매핑·트랜잭션·조회·랭킹 Stored Procedure·Migration SQL 작성. AWS/DB 작업 없음.

## P2 변경 및 검증 — 2026-10-23 00:18 KST

- EF Core SqlServer/Design/dotnet-ef 10.0.13과 lock, Scoped DbContext·Fluent mapping·고유 키/FK/CHECK·UTC/길이·covering index.
- SQL Repository의 projection/no-tracking/async 조회·bigint 통계, long offset 페이지, 명시적 등록 트랜잭션·SQL unique 경쟁 후 재조회·fingerprint 비교. Fake는 tests로 이동. JWT 전 Development만 쓰기 허용.
- InitialCreate/AddRankingsProcedure, immutable V001 T-SQL embedded 리소스, 검토용 idempotent `infra/scripts/migrations.sql`, 승인 후 수동 적용할 가상 seed SQL. 앱 startup migrate/seed 없음.
- 실제 DB readiness(3초 timeout)/TLS 설정 검사, SQL 예외·연결 원문 로그 억제. 비DB WebHost는 ambient 연결 설정과 DbContext options를 명시적으로 제거한다.
- 검증/롤백·안전장치: [database-validation.md](database-validation.md). API 계약/README/아키텍처/ADR-007 갱신.

| 실제 실행한 명령 | 결과 | 로그 |
|---|---|---|
| `dotnet build -warnaserror` 최초 | FAIL: Pkcs 9.0.11의 NU1902/NU1903, 경고 억제 없이 10.0.13 override로 수정 | [초기 실패](validation/p2/build-initial.log), [수정](validation/p2/dependency-fix.log), [수정 후 빌드](validation/p2/build-fixed.log) |
| Migration 생성 후 build | FAIL: IDE0161 생성 코드 namespace, xUnit2031 Assert.Single 사용 | [모델 빌드](validation/p2/build-model.log), [DB 테스트 빌드](validation/p2/build-database-tests.log) |
| `dotnet format --no-restore` 후 verify script | PASS: 모든 analyzer 수정, 경고 억제 없음 | [format 적용](validation/p2/format-apply.log) |
| `./infra/scripts/verify.sh docs/validation/p2` | PASS: locked restore/build 경고·오류 0/test 81 통과/format 확인 | [복원](validation/p2/restore.log), [빌드](validation/p2/build.log), [테스트](validation/p2/test.log), [format](validation/p2/format.log) |
| 깨끗한 소스 복사본 `bash infra/scripts/verify.sh artifacts/clean` | PASS: 단위 10 + 비DB 통합 71 = 81, 실패/skip 0, restore/build/format 성공 | [전체 로그](validation/p2/clean-snapshot.log) |
| `dotnet ef migrations add InitialCreate ...` / `add AddRankingsProcedure ...` | PASS: DB 없이 소스 생성 | [초기 생성](validation/p2/migration-initial.log), [SP 생성](validation/p2/migration-rankings.log) |
| `dotnet ef migrations script --idempotent --project src/ShooterRecordsLab.Infrastructure --startup-project src/ShooterRecordsLab.Api --output infra/scripts/migrations.sql` | PASS: 검토용 SQL 생성, 적용하지 않음 | [생성 로그](validation/p2/script.log) |
| `dotnet ef migrations has-pending-model-changes --project src/ShooterRecordsLab.Infrastructure --startup-project src/ShooterRecordsLab.Api` | PASS: 마지막 Migration 이후 모델 변경 없음 | [모델 일치](validation/p2/model-drift.log) |
| `dotnet list package --vulnerable --include-transitive --no-restore` | PASS: 조회 시점 NuGet 소스 기준 6개 프로젝트의 알려진 취약 패키지 없음 | [의존성 조회](validation/p2/dependency-audit.log) |
| `dotnet test --no-build --list-tests --filter 'Category=Database'` | PASS(discovery만): SQL 시나리오 1개 선택, Unit 0개 | [필터 확인](validation/p2/database-discovery.log) |
| `dotnet test --filter 'Category=Database'` | **NOT_RUN**: AWS 승인·Test DB 없음. exclude와 skip을 성공으로 집계하지 않음 | 실제 SQL 검증은 P3 |
| `dotnet run --no-build --project src/ShooterRecordsLab.Api --launch-profile http` (깨끗한 소스 빌드에서) | PASS: live 200/ready 503/OpenAPI 200/DB 미구성 조회는 안전한 500 | [Kestrel](validation/p2/kestrel.log), [스모크](validation/p2/kestrel-smoke.log) |
| 원본 SHA-256·gitignore·제한적 credential 패턴 검사 | PASS: 원본 Markdown 12개 불변, 비밀/상태파일 ignore | [소스 확인](validation/p2/source-checks.log) |

- 코드·생성 SQL 별도 자기 리뷰: [P0~P2 리뷰](reviews/p2-review.md), PASS_WITH_RISKS. 실제 SQL·JWT·AWS 검증 전이라는 한계 포함. 같은 세션으로 독립성 한계 있음.
- 추가 회귀: ASCII 변환 전 검증(ſ), 정확한 outcome 이름, 500 로그 status/비밀 미노출, TLS/전용 Test DB allowlist 가드, SP 매개변수화·모델 제약 정적 확인.
- 실제 SQL Server의 FK/UNIQUE/CHECK/rollback/동시성/SP·실행 계획은 **미검증**. 작성된 Database 테스트는 실제 연결·변경을 수행할 수 있으므로 승인 후 별도로 실행해야 한다.
- 다음: P3 비용·리전/계정·예산·RDS/EC2/네트워크·보존/정리 결정을 구체화하고 IaC/견적을 검토한 뒤 사용자 실행 승인. 현재 값은 미정이며 임의 정책/예산을 적용하지 않음.
- 비용/권한 변경: AWS 조회·리소스 생성/변경/삭제·IAM 변경·DB 연결/DDL/seed·실배포 모두 수행하지 않음. 프론트엔드 없음.

## P3 준비 변경 및 검증 — 2026-10-23 00:29 KST

- 사용자 선택 기록: 서울(ap-northeast-2) 기준 견적·IaC 초안, 자원 생성 없음. ADR-008에 승인 범위와 미정 항목을 분리했다.
- 공식 공개 RDS/EC2 offer 및 AWS 요금·운영 문서를 확인했다. 계정 자격정보를 사용하거나 계정 API를 조회하지 않았다.
- [견적](aws-cost-estimate.md): 730시간 기본 micro $39.52 / small $62.15, 60시간 small 비교 예시 $8.55. 추가 CPU·백업/API·KMS·전송·세금·기존 계정 비용은 별도. 모두 미승인 비교값이며 과금 상한이 아니다.
- [단가 근거](validation/p3-preparation/pricing-extract.json): SKU·원본 URL·게시 시각·SHA-256. [Decimal 산출 결과](validation/p3-preparation/cost-scenarios.json), `infra/scripts/estimate-aws-cost.py`로 재현.
- `infra/terraform`: versions/variables/network/compute/database/budget/outputs, provider lock, 빈 입력 example, mock tests 및 README. 전용 VPC·SSM EC2·private RDS·SG·암호화/TLS·보호·Budget 제안. 실제 생성 없음.
- 도구: Terraform 1.17.0 공식 archive SHA-256 확인, 사용자 경로 설치. AWS provider 6.70.0 서명/정확 버전·lock. 기존 AWS CLI는 있지만 프로파일 미선택이며 Session Manager plugin은 PATH에서 찾지 못함(설치하지 않음).

| 실제 실행한 명령 | 결과 | 증거 |
|---|---|---|
| `terraform -chdir=infra/terraform init -backend=false -input=false` | PASS: provider 다운로드/로컬 초기화만, AWS 계정 API 없음 | [init](validation/p3-preparation/init.log) |
| `terraform -chdir=infra/terraform test -no-color` 최초 | FAIL: plan 시 computed ingress/route unknown; 1 실패/4 후속 skip | [최초 실패](validation/p3-preparation/test-initial.log) |
| 모의 생성값 `override_during=plan` 지정 후 같은 테스트 | PASS: 5 통과/실패·skip 0. 실제 apply로 바꾸지 않음 | [최종 테스트](validation/p3-preparation/test.log) |
| `./infra/scripts/verify-infra.sh docs/validation/p3-preparation` | PASS: init/fmt-check/validate/mock plan 5건/비용 재계산 | [전체](validation/p3-preparation/verification.log), [fmt](validation/p3-preparation/fmt.log), [validate](validation/p3-preparation/validate.log), [비용](validation/p3-preparation/cost.log) |
| 앱 회귀 `./infra/scripts/verify.sh docs/validation/p3-preparation/app-regression` | PASS: restore/build 경고·오류 0, 단위 10+비DB 통합 71=81 통과, skip/실패 0, format 성공 | [회귀 로그](validation/p3-preparation/app-regression.log) |
| 실제 AWS provider `terraform plan` / apply / destroy, 계정 API·SSM·실DB | **NOT_RUN**: 계정·예산·정책/실행 미확정 | 모의 plan을 실인프라 검증으로 간주하지 않음 |

- 별도 자기 리뷰: PASS_WITH_RISKS(동일 세션으로 독립성 한계). SG 인바운드는 DB에 relay SG→1433 1개, EC2 인바운드 0·private DB 경로·master 권한 분리·무시 규칙·보호/고정 비용 입력을 검토했다. AWS 공식 static 파라미터 문서에 맞춰 force_ssl을 pending-reboot로 수정하고 회귀 assertion 추가.
- configuration_review_reference는 실제 후보 입력 검토 기록이며 apply 승인/강제 보안 통제가 아니다. empty일 때 VPC와 종속 자원 계획을 막는다. 코드/fixture 숫자는 승인 정책이 아니다.
- 남은 위험: 실제 engine/class/gp3/암호화/AZ 주문 가능성, AMI/쿼터·SSM/TLS·IAM·비용 사용량은 미검증. RDS T3 초과 CPU와 중지 후 잔존/7일 자동 재시작 비용 주의. state 저장·보존/정리·보안 패치 운영 방안도 확정 필요.
- 다음: 사용자 사양/월 예산/프로파일 답변 → 허용된 읽기 전용 조사·입력 확정 → 실제 plan/견적·삭제 정책 검토 → 별도 생성 승인. 실DB 테스트·P3 완료 표시는 그 이후.

- 최종 소스 확인: `source-checks.log`에서 원본 12개 해시 보존, state/tfvars/plan 무시, lock/example 보존, mock plan-only 5건과 비용 재현을 확인했다. 실제 자원/DB 생성·변경 없음.

## 2026-10-23 — 예산 결정 전 사양 보류

- 사용자: “예산을 먼저 정하고 사양 선택 보류”. 실제 사양·예산·프로파일·생성 승인은 확정하지 않았다.
- 월 약 30회 요청일 때의 비용 질문에 대해 상시 실행과 필요한 시간만 실행하는 가정을 분리했다. 월 요청 수는 가동 시간과 다르며 자동 중지/스케줄 승인이 아니다.
- 문서의 선택 상태만 갱신. Terraform/API 설정·AWS/DB·배포 변경 없음.

## 2026-10-23 — GitHub 최초 공개 준비

- 사용자 승인: `ShooterRecordsLab` 신규 저장소의 Public 공개·커밋·푸시. `/docs/init/`만 초기 문서 공개 대상에서 제외하고, 그 밖의 에이전트 Markdown 작업/리뷰/검증 기록은 포함한다.
- 기존 Git은 main 브랜치·커밋 없음·원격 없음이었다. `.gitignore`에 `/docs/init/`을 추가하고 로컬 원본은 유지한다.
- 최초 게시 결과와 실제 clone 검증은 성공을 확인한 후 별도 기록한다. AWS/DB 작업은 수행하지 않는다.

## GitHub 최초 공개 및 실제 clone 검증 — 2026-10-23 00:51 KST

- 공개 저장소: https://github.com/CodingBot000/ShooterRecordsLab (`main`). 사용자의 신규 생성·Public·커밋·푸시 승인에 따라 수행했다.
- 최초 소스 커밋: `eee0b6f5926484398e027cdb02d34063a3270e71`. 원격 생성·main push·tracking 연결 성공을 직접 확인했다.
- `/docs/init/`은 로컬에 보존하고 Git에서는 제외. 그 밖의 비생성 Markdown 16개(AGENTS/README/운영 문서/결정/상태/리뷰 포함)를 모두 커밋했다. 소스·IaC·테스트·허용된 검증 로그도 포함했다.
- 커밋 전: 선택적 자격증명 패턴 검사에서 일치 없음. password 패턴은 관리형 password 설정과 의도적인 `do-not-print` 테스트 입력만 확인했다. 이는 전면적인 보안 감사라고 주장하지 않는다.
- 설정 JSON 2개의 CRLF와 생성 SQL의 마지막 빈 줄을 LF/단일 종료 줄로 정규화했다. 과거 검증 로그는 원문을 보존하며 EOF 빈 줄 경고는 코드 검사 대상과 분리했다.
- 실제 GitHub HTTPS `git clone --depth 1 https://github.com/CodingBot000/ShooterRecordsLab.git` 후 `./infra/scripts/verify.sh artifacts/fresh-clone` 실행: **PASS**. locked restore, warn-as-error build 경고·오류 0, 단위 10 + 비DB 통합 71 = **81 통과**, 실패/skip 0, format 확인 성공.
- 새 clone에는 `docs/init/`이 없고 Markdown 16개가 있는 상태에서 검증했다. SDK 10.0.402/macOS Arm64, 기존 로컬 NuGet 캐시는 사용했다. 실제 SQL/AWS 테스트는 실행하지 않았다.
- 증거: [clone 기록](validation/github-publication/clone.log), [전체 검증](validation/github-publication/fresh-clone-verification.log), [커밋·환경 메타데이터](validation/github-publication/fresh-clone-metadata.json), [공개 대상 확인](validation/github-publication/publication-checks.log).
- 이 기록은 검증된 소스 커밋 뒤에 문서·증거만 추가하는 별도 커밋으로 보존한다. 실제 AWS 배포/DB 검증은 여전히 미실행이다.

## 실제 클라우드 검증을 위한 코드 준비 — 2026-10-23

- JWT Bearer/쓰기 scope와 401/403 ProblemDetails, 안전한 구성 검증을 구현했다. 로컬 및 Linux x86_64 컨테이너에서 단위 10 + HTTP 83 = 93 통과, 실패/skip 0, 빌드 경고/오류 0, format 성공.
- 공식 이미지/CA bundle digest 고정 Docker 빌드, 비root/읽기 전용/health probe와 인증·미구성 readiness 경계를 확인했다. 명시적 검증용 SQL 준비/실행 계획 측정 도구는 소스에 추가하되 자격정보는 포함하지 않는다.
- Terraform 모의 테스트 6개가 통과했다. 일회성 정리 모드의 retained-backup 플래그가 적용되지 않던 실패를 수정한 후 재검증했다. 실제 계정 plan은 신규 자원만 포함하고 기존 자원 변경이 없음을 비공개로 검토했다.
- 새 공개 증거는 허용된 날짜/코드·이미지 content hash/검증 항목·수치만 남긴다. 계정·AWS 식별자·주소·비밀·원본 plan/state/로그를 공개하지 않는다. 준비 결과: [checks.json](validation/pre-deployment/checks.json).
- 이 시점의 실제 AWS 배포·실DB 테스트는 아직 미실행이며 실제 결과는 실행 후 기록한다.

## 실환경 검증 결과 — 2026-10-23

- [실환경 보고서](aws-validation.md), [공개 허용 필드 결과](validation/aws-validation/report.json)에 실제 검증 코드·이미지 내용 hash·일자·수치를 기록했다.
- 검증 소스 `71c8a47`: Linux Release 전체 **105 통과 / 실패 0 / skip 0**. 단위 10 + 통합 95(비DB 83, 실DB 12)이며 실DB 12개는 전체에 포함된다.
- 실제 EC2 API HTTP 18개 사례 통과. 같은 external ID의 동시 요청 10개는 신규 생성 1개/재응답 9개·같은 match 확인. JWT 401/403·201/200/409·404/400·조회/통계/랭킹/페이지 검증.
- 10,000개 결과 데이터의 실제 실행 계획·논리 읽기·시간 확보. 목록/통계/랭킹 Logical reads는 각각 59/43/88이며 측정 환경·한계는 보고서에 명시했다.
- DB 연결 실패 주입으로 live 200/ready 503/안전한 500, 잘못된 인증 설정 startup 거부와 정상 버전 복구·ready 200을 확인했다. 비root/읽기 전용·소스 revision label·private RDS/SG/암호화도 실제 조회했다.
- 초기 실DB 테스트의 CHECK 실패는 상수 입력으로 재현 후 후속 Migration으로 수정했고 테스트를 유지·강화해 재검증했다. 원본 계획/환경·서버/계정·키·주소·비밀·자원 식별자는 공개하지 않는다.
- 최종 NuGet 전이 포함 취약점 조회에서 7개 프로젝트의 알려진 취약 패키지 없음. Migration 모델 drift 없음. 이는 전체 보안 감사를 의미하지 않는다.
- 별도 자기 리뷰: [실환경 리뷰](reviews/aws-validation-review.md). 같은 에이전트 세션의 한계를 명시했다.

### 이전 기록의 미검증 항목 갱신

P0~P2 및 P3 준비 기록의 NOT_RUN/미검증 표시는 해당 시점의 사실로 보존한다. SQL Server·JWT·EC2/SSM/Docker·실행 계획·장애 복구 항목은 위 후속 실제 검증으로 갱신되었다. 공개 HTTPS·외부 인증 발급자 연동·장기 운영/성능 상한·독립 감사는 검증 범위에 포함하지 않는다.

## 2026-10-23 — 프로젝트 설명의 기업명 제거

- 변경: README, 운영 문서의 개발계획·의사결정, `docs/init/`의 개발계획·의사결정·초기화 안내에서 특정 기업명을 제거했다. 채용 공고 중심의 문장은 .NET 게임 웹 백엔드 학습·검증 목표로 정리하고, 가상 데이터 사용 및 외부 게임 서비스 연동 제외 범위는 보존했다.
- 승인 적용: 사용자의 전체 제거 요청에 따라 이번 문구 수정에 한해 초기 원본 보존 원칙의 예외를 적용했다. `/docs/init/`의 Git 제외 설정은 유지한다. 변경 이유는 의사결정 문서에 기록했다.
- 검증: `rg -a -i -l --hidden --no-ignore -g '!.git/**' <대상 표현> .`로 대소문자 구분 없이 재검색한 결과 일치 파일 0개(exit 1, 검색 오류 아님). 숨김·Git 무시 파일·빌드 산출물·초기 원본까지 포함하고 Git 내부 이력은 제외했다. `git diff --check` 통과. 문서 변경이므로 빌드·런타임 테스트는 재실행하지 않았다.
- 별도 자기 리뷰: 수정 문맥과 개발 범위 보존을 확인했다. 동일 세션의 검토이며 독립된 제3자 리뷰는 아니다.
- 남은 범위·다음 단계: 현재 파일 기준 정리를 완료했다. 과거 커밋 이력은 변경하지 않았다. 커밋·푸시 없이 사용자 검토를 기다린다.

## 2026-10-23 — Spring Boot 전환 설명서와 README 연결

- 사용자 요청: 제안한 방향의 별도 설명서 작성, README 상단 요약·링크, 실제 코드·테스트·검증 기록에 근거한 설명과 변경 기록 갱신.
- 변경 파일: [새 설명서](spring-boot-to-aspnet-core.md), [README](../README.md), [의사결정 ADR-012](decisions.md#adr-012--spring-boot-전환-설명서와-readme-상단-안내), 이 진행 기록. README에는 소개 직후 6줄만 추가하고 상세 설명을 별도 문서로 연결했다. API·테스트·Migration·배포 설정과 docs/init은 수정하지 않았다.
- 내용: .NET/ASP.NET Core/EF Core의 역할, 프로젝트·패키지·외부 설정·C# 표현, DI 수명·비동기·취소·영속성·트랜잭션·요청 파이프라인·인증/인가·조회 비용을 설명했다. 경기 등록 흐름과 SQL Server CHECK 수정 사례를 코드·테스트·기존 실환경 보고서로 연결했다.
- 증거 구분: 현재 Repository의 트랜잭션·fingerprint 비교 책임, 단일 SaveChanges의 원자성, LINQ count와 매개변수 SQL 페이지 조회, 서비스 직접 호출인 취소 테스트를 명시했다. 과거 소스 `71c8a47`의 전체 105개에 실DB 12개가 포함되고 EC2 HTTP 18개는 별도라는 집계를 유지했다.
- 도구 환경: 셸 PATH에 dotnet이 없어 기존 `/Users/switch/.local/share/shooterrecords-dotnet` SDK를 검증 명령에만 추가했다. 시스템·셸 설정은 변경하지 않았다. 실행 명령은 `PATH="/Users/switch/.local/share/shooterrecords-dotnet:$PATH" bash infra/scripts/verify.sh artifacts/validation/spring-boot-guide`이며 종료 코드 0이다.

| 실제 실행한 검증 | 결과 |
|---|---|
| `dotnet --version` | PASS: 기존 SDK 10.0.402 |
| `dotnet restore --locked-mode` | PASS: 7개 프로젝트 복원 |
| `dotnet build -warnaserror --no-restore` | PASS: 경고 0·오류 0 |
| `dotnet test --no-build --filter 'Category!=Database'` | PASS: 단위 10 + 비DB 통합 83 = **93개**, 실패 0·skip 0 |
| `dotnet format --verify-no-changes --no-restore` | PASS: 수정 필요 없음 |
| Python 표준 라이브러리로 문서 링크·근거 대조 | PASS: 4개 문서의 상대 링크·앵커, 인용 테스트 메서드 6개, 기존 JSON의 105/12·동시 요청 10, DB 문자 사례 11개 확인 |
| `git diff --check` 및 새 문서 공백 검사 | PASS |

- 로컬 검증 로그는 Git 제외 경로 `artifacts/validation/spring-boot-guide/`의 `sdk.log`·`restore.log`·`build.log`·`test.log`·`format.log`와 `document-checks.json`에 저장했다. 위 93개는 이번 로컬 비DB 재실행 결과이고, 기존 105개 실환경 결과를 대체하거나 실DB 재실행으로 계산하지 않는다.
- 별도 자기 리뷰: **PASS_WITH_RISKS**. 코드·테스트·검증 기록과 설명을 다시 대조하고 링크를 검사했다. Critical/High 발견 없음. DI·비동기·트랜잭션의 일대일 대응 오해, 실제 트랜잭션 구현 위치, 취소 테스트의 범위, 페이지 정렬과 스냅샷 일관성의 차이, 에이전트 활용의 표현을 검토했다. 구현과 동일 세션의 별도 패스로 독립된 제3자 리뷰는 아니다.
- 남은 위험: 비교 대상 Spring 구현은 없으며 두 구현의 동등성을 검증하지 않았다. 과거 실DB/AWS 결과는 기존 기록을 인용했다. 이번 변경에서 `Category=Database` 테스트·AWS 배포·외부 서비스 운영 검증은 재실행하지 않았다. 구현이나 테스트가 바뀌면 새 설명서의 근거도 함께 갱신해야 한다.
- 공개 반영 승인: 사용자의 후속 “커밋 푸시” 요청으로 위 4개 문서를 main에 커밋하고 origin/main에 푸시하는 범위가 승인되었다. 이 문서가 포함된 커밋 SHA와 원격 반영 결과는 Git 이력을 기준으로 확인한다.

## 2026-10-26 — GitHub Actions CI와 main 보호 설정

- 승인 범위: workflow 추가·커밋·푸시, GitHub 첫 실행 확인, `main`의 필수 검사 설정. 소유자 적용 여부는 사용자 답변 대기 중이며 해당 설정은 답변 후 적용한다.
- 변경 파일: `.github/workflows/ci.yml`, README, 테스트 지침, ADR-013, 이 진행 기록. 기존 검증 스크립트를 재사용하고 앱·테스트·DB·AWS 설정은 변경하지 않는다.
- 사전 확인: 작업 트리 깨끗함, 로컬/원격 `main` 일치(`d5a70ce`), 저장소 Public 및 Actions 활성화, 현재 `main` 보호·ruleset 없음, 기존 GitHub CLI의 관리 권한 확인. 비밀 값은 출력하지 않았다.
- 구현: `verify` job에서 기존 SDK 정책과 비DB 검증을 실행하고, 생성된 로그를 artifact로 보존하며 실제 DB 테스트 NOT_RUN을 실행 요약에 표시한다. 공식 액션 3개의 tag를 실제 조회하고 전체 SHA로 고정했다.
- 로컬 자동 검증: `PATH="/Users/switch/.local/share/shooterrecords-dotnet:$PATH" bash infra/scripts/verify.sh artifacts/validation/ci-setup-20261026` 종료 코드 0. SDK 10.0.402, locked restore 성공, warn-as-error build 경고 0/오류 0, 단위 10 + 비DB 통합 83 = **93 통과 / 실패 0 / skip 0**, format 확인 성공. 로그는 Git 제외 경로 `artifacts/validation/ci-setup-20261026/`의 sdk/restore/build/test/format.log에 있다.
- 정적 확인: Ruby 표준 YAML parser로 workflow 구문·트리거·job/step 구조 확인, `bash -n infra/scripts/verify.sh`, `git diff --check` 통과. 전체 SHA 3개는 공식 액션 저장소의 실제 tag를 조회한 값이다. actionlint는 설치되어 있지 않아 실행하지 않았으며 YAML parse를 GitHub 실제 실행 성공으로 해석하지 않는다.
- 별도 리뷰: **PASS_WITH_RISKS**. 권한/자격증명 지속 저장·PR 이벤트·문서 변경 필수 검사·script의 pipefail/첫 실패 중단·continue-on-error 부재·항상 로그 업로드·DB NOT_RUN 표시를 대조했다. 기존 테스트/assert·앱/API/SQL은 변경하지 않았으며 Critical/High 발견 없음. 구현과 동일 세션의 별도 패스로 독립된 제3자 리뷰는 아니다. GitHub 실제 실행·artifact·앱 ID 확인과 보호 설정은 후속 검증 대상이다.
- 문서 링크 확인: Python 표준 라이브러리로 변경 문서 4개의 상대 파일 링크 **96개**, 누락 0개 확인. 결과는 `artifacts/validation/ci-setup-20261026/document-checks.json`에 보존했다.
- GitHub 게시: CI와 관련 문서를 `dd478586f08787f4162bded9487c34f02fca4dda`로 커밋하고 `origin/main`에 정상 push했다. 앱/패키지/기존 테스트 변경 없이 게시했으며 작업 트리가 깨끗함을 확인했다.
- GitHub 첫 실행: [CI run 40337051554](https://github.com/CodingBot000/ShooterRecordsLab/actions/runs/40337051554), event `push`, 대상 SHA `dd478586f08787f4162bded9487c34f02fca4dda`, job `verify` **SUCCESS**. Ubuntu 24.04.5의 실제 SDK는 **10.0.403**이며, 기존 `global.json`의 `latestPatch`가 허용한 같은 기능 밴드 패치다. 로컬 SDK 10.0.402와 구분하며 SDK 정책을 변경하지 않았다.
- GitHub 실행 로그: locked restore, build 경고 0/오류 0, 단위 10 + 비DB 통합 83 = **93 통과 / 실패 0 / skip 0**, format 검사 및 업로드 단계 모두 성공. `verification-logs` artifact **5개 로그**, artifact ID `12656479560`, ZIP 1,320 bytes 생성과 다운로드 성공을 확인했다. 이 93개는 비DB 실행 수이며 실DB 12개를 포함하지 않는다.
- 필수 검사 식별: 실제 check-run `121621329464`의 이름 `verify`, 발행 앱 `github-actions`/ID `15368`을 조회했다. `main` 보호 적용은 소유자 적용 범위 답변 대기 중이며 아직 성공으로 표시하지 않는다.
- 실제 SQL Server/클라우드 배포 검증은 이번 CI 변경에서 **NOT_RUN**. 과거 실환경 검증 결과와 새 비DB 실행 결과를 합산하지 않는다.
