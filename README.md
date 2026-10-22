# ShooterRecordsLab

가상의 플레이어·경기 데이터를 다루는 개인 학습용 REST API입니다. 넥슨 공식 프로젝트 및 실제 게임 계정·기록과 무관합니다. 프론트엔드는 구현하지 않습니다.

.NET 10 / ASP.NET Core Controllers / EF Core 10 / SQL Server 2022를 사용합니다. AWS EC2 Docker API와 private RDS SQL Server Express에서 실제 배포·DB·인증·동시성·장애 복구 검증을 수행했습니다. [실환경 검증 보고서](docs/aws-validation.md)와 [기계 판독 결과](docs/validation/aws-validation/report.json)에 검증 코드 버전과 결과를 기록했습니다.

## macOS 사전 조건

- `global.json`의 .NET SDK 10.0.402 기능 밴드(10.0.4xx 최신 패치 허용), Git.
- Apple Silicon은 macOS Arm64 SDK, Intel은 x64 SDK를 [Microsoft 공식 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)에서 설치합니다.
- 초기 작업 환경에 SDK가 없어 공식 Arm64 바이너리의 SHA-512를 검증하고 `~/.local/share/shooterrecords-dotnet`에 설치했습니다. 현재 Mac은 아래 PATH를 사용합니다. 셸 설정 파일은 변경하지 않았습니다.

```bash
export PATH="$HOME/.local/share/shooterrecords-dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1
dotnet --version
dotnet tool restore
dotnet restore --locked-mode
dotnet build -warnaserror
```

SQL Server, Docker, AWS 자격증명 없이 빌드·비DB 테스트·Migration SQL 생성을 할 수 있습니다. NuGet lock 파일과 `dotnet-ef` 10.0.13을 저장소에서 관리합니다.

## 실행

```bash
dotnet run --project src/ShooterRecordsLab.Api --launch-profile http
# 별도 터미널
curl -i http://127.0.0.1:5080/health/live
curl -i http://127.0.0.1:5080/health/ready
curl -i http://127.0.0.1:5080/openapi/v1.json
```

개발 프로필은 `127.0.0.1:5080`에 바인딩합니다. DB 없이 live는 200, ready는 503입니다. OpenAPI는 Development에서만 노출합니다. 데이터 API 실행에는 본인의 SQL Server 연결 설정이 필요하며 P1의 메모리 Fake는 **테스트 프로젝트에만** 있습니다. 쓰기 API는 모든 환경에서 JWT와 `scope=matches.write` 권한이 필요합니다.

DB 연결 문자열 키는 `ConnectionStrings:ShooterRecords`입니다. P3 승인 후 .NET user-secrets 또는 외부 환경 설정으로 제공하며 실제 값을 파일/터미널 기록/로그에 노출하지 않습니다. 서버·DB, TLS 암호화·서버 인증서 검증, MARS 비활성화를 검사합니다. SSM 터널은 `HostNameInCertificate`에 실제 승인된 RDS 인증서 호스트명을 사용해야 합니다. [접속·승인 지침](docs/aws-deployment.md)을 먼저 확인합니다.

## API 사용 예

P3에서 승인된 Dev/Test DB에 Migration과 `infra/scripts/seed-demo.sql`을 적용한 뒤 사용할 예입니다. 명령의 요청·응답 동작은 실환경 보고서의 소스 버전에서 검증했습니다. 재현 시 본인의 승인된 DB·인증 구성이 필요합니다. 앱 기동 중 Migration/seed는 실행하지 않습니다.

```bash
curl -i http://127.0.0.1:5080/api/players/11111111-1111-1111-1111-111111111111
curl -i http://127.0.0.1:5080/api/players/11111111-1111-1111-1111-111111111111/stats
curl -i 'http://127.0.0.1:5080/api/players/11111111-1111-1111-1111-111111111111/matches?page=1&pageSize=20'
curl -i 'http://127.0.0.1:5080/api/rankings?top=20'
curl -i http://127.0.0.1:5080/api/matches \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $VALIDATION_TOKEN" \
  -d '{"externalMatchId":"DEMO-MATCH-0001","playedAtUtc":"2026-10-21T12:30:00Z","participants":[{"playerId":"11111111-1111-1111-1111-111111111111","kills":8,"deaths":3,"outcome":"Win"},{"playerId":"22222222-2222-2222-2222-222222222222","kills":3,"deaths":8,"outcome":"Loss"}]}'
```

최초 POST는 201 + Location, 동일 요청 재시도는 200, 같은 external ID의 다른 내용은 409입니다. 쓰기는 인증 전의 로컬 학습 단계로 외부 공개하지 않습니다. 요청·정렬·UTC 규칙과 오류는 [API 계약](docs/api-contract.md)을 따릅니다.

## 검증

```bash
./infra/scripts/verify.sh
# 개별 명령
dotnet restore --locked-mode
dotnet build -warnaserror
dotnet test --filter 'Category!=Database'
dotnet format --verify-no-changes
```

스크립트는 첫 실패에서 종료하고 기본 로그를 `artifacts/validation/`에 저장합니다. 실제 단계별 결과는 [진행 현황](docs/project-status.md)과 `docs/validation/`에 기록합니다. 비DB 테스트는 HTTP Fake 테스트와 SQL Server 모델·SQL 생성의 정적 검사입니다. SQL 실행·동시성·실행 계획의 증거는 실제 SQL Server에서 별도로 확보해야 합니다.

## Migration 검토

다음 명령은 DB 연결 문자열이 없는 design-time factory로 소스/SQL만 생성합니다.

```bash
dotnet tool restore
dotnet ef migrations script --idempotent \
  --project src/ShooterRecordsLab.Infrastructure \
  --startup-project src/ShooterRecordsLab.Api \
  --output infra/scripts/migrations.sql
dotnet ef migrations has-pending-model-changes \
  --project src/ShooterRecordsLab.Infrastructure \
  --startup-project src/ShooterRecordsLab.Api
```

`infra/scripts/migrations.sql`은 검토용이며 아직 DB에 적용하지 않았습니다. 실제 적용·롤백·seed는 대상 서버/DB·권한·백업·명시적 승인을 확인한 후 수행합니다. `database update`도 design-time factory에 연결 정보가 없으므로 그대로 실행하지 않습니다. 자세한 준비/롤백 경계는 [DB 검증 안내](docs/database-validation.md)를 따릅니다.

## 구조와 문서

- `src/ShooterRecordsLab.{Api,Application,Domain,Infrastructure}`: HTTP, 유스케이스, 도메인, EF/T-SQL 계층.
- `tests/ShooterRecordsLab.{UnitTests,IntegrationTests}`: 단위·HTTP·모델 검증 및 별도 Database 테스트.
- [개발계획](docs/development-plan.md), [아키텍처](docs/architecture.md), [의사결정](docs/decisions.md), [AWS 지침](docs/aws-deployment.md).
- `docs/init/`: 로컬에만 보존하는 초기 원본으로 Git에서는 제외합니다. GitHub에는 갱신한 `docs/` 운영 문서와 에이전트 작업·검증 기록을 포함합니다. 저장소 clone 후 빌드/테스트에는 초기 원본이 필요하지 않습니다.

## 인프라 코드와 비용 검토

초기 [비용 비교](docs/aws-cost-estimate.md), [Terraform 구성](infra/terraform/README.md), 후속 [실환경 검증 결과](docs/aws-validation.md)를 구분합니다. 다른 계정에서 재현할 때는 본인의 사양·비용·보존 정책과 실행 범위를 다시 확인해야 합니다. 실제 계정 입력과 비밀은 저장소에 포함하지 않습니다.

```bash
export PATH="$HOME/.local/share/shooterrecords-terraform:$PATH"
./infra/scripts/verify-infra.sh
python3 infra/scripts/estimate-aws-cost.py
```

이 검증은 로컬 schema/format·AWS mock의 plan-only 테스트입니다. 실제 계정 plan/apply/destroy·DB 연결·IAM/Budget 설정은 실행하지 않습니다. Terraform 입력 example은 의도적으로 미완성입니다.

## 배포·인증 검증 준비

`infra/docker/Dockerfile`은 버전/digest를 고정한 Linux x86_64 빌드·비root 런타임과 AWS RDS 인증서 검증을 구성합니다. 키·토큰·DB 연결 문자열은 이미지/소스에 포함하지 않습니다. `Authentication:Jwt` 설정은 외부 비밀 저장소에서 주입하며, 검증 전용 Staging 토큰은 Production 인증 서버를 대신하지 않습니다. 공개 JWT 발급 API는 제공하지 않습니다.

클라우드 검증 보고서는 `infra/scripts/export-validation-evidence.py`의 허용된 필드만 공개합니다. 원본 AWS 출력·계정·주소·자원 ID·비밀·Terraform state/plan은 Git에 넣지 않습니다. 실제 실행 결과는 [실환경 보고서](docs/aws-validation.md)에 기록했습니다.
