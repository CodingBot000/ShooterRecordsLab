# ShooterRecordsLab

가상의 플레이어·경기 데이터를 다루는 개인 학습용 REST API입니다. 넥슨 공식 프로젝트 및 실제 게임 계정·기록과 무관합니다. 프론트엔드는 구현하지 않습니다.

.NET 10 / ASP.NET Core Controllers / EF Core 10 / SQL Server 2022를 사용합니다. P0~P2의 로컬 코드·검증을 완료했고, 실제 private AWS RDS SQL Server Express 연결은 P3의 비용·구성 승인 후 진행합니다. API의 EC2 Docker 배포는 P5입니다.

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

개발 프로필은 `127.0.0.1:5080`에 바인딩합니다. DB 없이 live는 200, ready는 503입니다. OpenAPI는 Development에서만 노출합니다. 실제 데이터 API는 SQL Server 연결이 필요하며 P1의 메모리 Fake는 현재 **테스트 프로젝트에만** 있습니다. 쓰기 API는 JWT 완성 전까지 Development에서만 허용하며 다른 환경에서는 404를 반환합니다.

DB 연결 문자열 키는 `ConnectionStrings:ShooterRecords`입니다. P3 승인 후 .NET user-secrets 또는 외부 환경 설정으로 제공하며 실제 값을 파일/터미널 기록/로그에 노출하지 않습니다. 서버·DB, TLS 암호화·서버 인증서 검증, MARS 비활성화를 검사합니다. SSM 터널은 `HostNameInCertificate`에 실제 승인된 RDS 인증서 호스트명을 사용해야 합니다. [접속·승인 지침](docs/aws-deployment.md)을 먼저 확인합니다.

## API 사용 예

P3에서 승인된 Dev/Test DB에 Migration과 `infra/scripts/seed-demo.sql`을 적용한 뒤 사용할 예입니다. **이 명령의 성공은 현재 실DB에서 검증되지 않았습니다.** 앱 기동 중 Migration/seed는 실행하지 않습니다.

```bash
curl -i http://127.0.0.1:5080/api/players/11111111-1111-1111-1111-111111111111
curl -i http://127.0.0.1:5080/api/players/11111111-1111-1111-1111-111111111111/stats
curl -i 'http://127.0.0.1:5080/api/players/11111111-1111-1111-1111-111111111111/matches?page=1&pageSize=20'
curl -i 'http://127.0.0.1:5080/api/rankings?top=20'
curl -i http://127.0.0.1:5080/api/matches \
  -H 'Content-Type: application/json' \
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

## P3 견적·인프라 초안 (생성 전)

사용자가 서울 리전의 비용·IaC 준비를 선택했습니다. [견적·미정 결정 목록](docs/aws-cost-estimate.md)과 [Terraform 초안](infra/terraform/README.md)을 확인합니다. 월 예산·계정/프로파일·사양·보존/정리와 실제 실행은 아직 확정하지 않았습니다.

```bash
export PATH="$HOME/.local/share/shooterrecords-terraform:$PATH"
./infra/scripts/verify-infra.sh
python3 infra/scripts/estimate-aws-cost.py
```

이 검증은 로컬 schema/format·AWS mock의 plan-only 테스트입니다. 실제 계정 plan/apply/destroy·DB 연결·IAM/Budget 설정은 실행하지 않습니다. Terraform 입력 example은 의도적으로 미완성입니다.
