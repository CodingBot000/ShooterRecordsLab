# ShooterRecordsLab

**경기 기록의 중복 저장과 동시 요청을 처리하는 ASP.NET Core REST API**

플레이어별 경기 이력·승패·킬/데스 통계와 랭킹을 제공하는 개인 백엔드 프로젝트입니다. 경기 등록의 멱등성, SQL Server 데이터 정합성, JWT 인가를 구현하고, **AWS EC2와 private RDS에 실제 배포해 동시성·인증·장애 복구까지 검증**했습니다.

### Spring Boot 경험에서 ASP.NET Core로

Java·Spring Boot에서 익힌 API 계층 분리, 생성자 주입, 트랜잭션과 테스트 원리를 ASP.NET Core·EF Core·SQL Server에 적용했습니다. DI 수명, 비동기 I/O·취소, 변경 추적과 트랜잭션 경계의 차이를 실제 코드·테스트·검증 기록과 연결해 정리했습니다.

**[Spring Boot 개발자를 위한 ASP.NET Core 이해와 구현 근거 →](docs/spring-boot-to-aspnet-core.md)**

[실환경 검증 보고서](docs/aws-validation.md) · [검증 결과 JSON](docs/validation/aws-validation/report.json) · [API 계약](docs/api-contract.md) · [설계와 의사결정](docs/decisions.md)

## 실제 AWS 배포·검증

2026-10-23 KST, 서울 리전의 **EC2 Linux x86_64 + Docker / RDS SQL Server 2022 Express** 환경에서 실행했습니다. 검증 대상 소스는 [`71c8a47`](https://github.com/CodingBot000/ShooterRecordsLab/tree/71c8a4784115b82905b7bcd02fcf841e6c2be183)이며, 보고서에 이미지 내용 digest와 실행 결과를 함께 기록했습니다.

| 검증 항목 | 실제 결과 |
|---|---|
| Linux Release 빌드 | 경고·오류 0 |
| 단위·HTTP·실제 DB 통합 테스트 | **105개 통과 / 실패 0 / 건너뜀 0** |
| 위 105개에 포함된 실제 SQL Server 테스트 | **12개 통과** — 동시성·원자성·FK/UNIQUE/CHECK·저장 프로시저·문자 경계 |
| EC2 API 대상 별도 HTTP 검증 | **18개 사례 통과** — 인증·권한·등록·재시도·충돌·조회·페이지네이션 |
| 동일 경기 ID로 동시 요청 10건 | **신규 생성 1건 + 재응답 9건**, 모두 동일한 리소스 확인 |
| DB 연결 실패 주입 | liveness **200**, readiness **503**, 데이터 API는 내부정보 없는 **500** |
| 잘못된 인증 설정으로 배포 | 시작 거부 확인 후 검증된 버전으로 복구, readiness **200** |

전체 테스트는 단위 10개 + 통합 95개이며, 통합 테스트에 실제 DB 테스트 12개가 포함됩니다. EC2 HTTP 검증 18개는 이와 별도로 실행했습니다.

검증은 SSM 경유의 비공개 Staging 환경에서 수행했습니다. 공개 HTTPS 서비스·외부 인증 발급자 연동·장기 가용성은 검증 범위에 포함하지 않습니다. 공개 증거는 소스 버전·이미지 내용 hash·검증 항목·수치로 제한하며, 접속 주소·계정/자원 식별자·자격증명·원본 클라우드 로그는 포함하지 않습니다.

## 핵심 구현

### 1. 재시도와 동시 요청에도 한 번만 저장하는 경기 등록

외부 경기 ID를 정규화하고, 경기 시각과 참가자 정보를 일관된 순서로 변환한 뒤 SHA-256 fingerprint로 요청 내용의 동등성을 비교합니다. 참가자 입력 순서가 바뀌어도 같은 경기로 판단합니다.

| 요청 상황 | 응답 |
|---|---|
| 처음 등록한 경기 | `201 Created` + `Location` |
| 같은 외부 ID·같은 내용으로 재시도 | `200 OK`, 기존 리소스 반환 |
| 같은 외부 ID·다른 내용 | `409 Conflict` |

동시성의 최종 보장은 DB의 UNIQUE 제약으로 처리합니다. UNIQUE 충돌 시 실패한 트랜잭션을 정리한 뒤 저장된 요청을 다시 조회해 비교하며, 다른 DB 오류를 중복 충돌로 잘못 변환하지 않습니다. 경기와 참가자 결과는 하나의 트랜잭션으로 저장해 부분 저장을 방지합니다.

코드: [MatchService](src/ShooterRecordsLab.Application/Matches/MatchService.cs) · [SQL 저장소](src/ShooterRecordsLab.Infrastructure/Repositories/SqlRecordsRepository.cs) · [실제 DB 통합 테스트](tests/ShooterRecordsLab.IntegrationTests/SqlServerTests.cs)

### 2. 애플리케이션과 DB 양쪽에서 보장하는 데이터 정합성

참가자 수·중복·존재 여부, 킬/데스 범위, 승패 조합과 UTC 시각을 검증합니다. DB에는 외부 경기 ID UNIQUE, 경기별 참가자 UNIQUE, FK와 CHECK 제약을 두고, EF Core Migration으로 스키마와 저장 프로시저를 버전 관리합니다. 앱 시작 시 Migration이나 seed를 자동 실행하지 않습니다.

**실제 RDS에서 발견한 문제도 후속 Migration으로 수정했습니다.** 초기 CHECK의 문자 집합 표현식이 정상적인 하이픈 포함 ID를 거부하는 현상을 재현했습니다. 기존 InitialCreate는 유지하고 `FixExternalMatchIdCharacterSet` Migration을 추가했으며, 기존 동시성·원자성 테스트에 정상·금지 문자 경계 사례 11개를 추가해 실제 SQL Server에서 검증했습니다.

근거: [수정 Migration](src/ShooterRecordsLab.Infrastructure/Persistence/Migrations/20261022162547_FixExternalMatchIdCharacterSet.cs) · [문자 경계 DB 테스트](tests/ShooterRecordsLab.IntegrationTests/SqlServerExternalIdTests.cs) · [문제 재현·수정 기록](docs/aws-validation.md#실제-db-검증에서-발견하고-수정한-문제)

### 3. 조회 특성에 맞춘 EF Core와 T-SQL 사용

경기 목록은 `AsNoTracking`과 필요한 컬럼의 projection을 사용하고, 경기 시각과 ID를 함께 정렬해 페이지 간 순서를 고정합니다. 통계는 DB에서 집계하며, 경기 없음·데스 0일 때의 비율을 명시적으로 처리합니다. 랭킹은 매개변수화한 `dbo.GetTopPlayerRankings(@Top)` 저장 프로시저로 조회합니다.

가상 경기 **5,000개 / 참가자 결과 10,000개**로 실제 실행 계획의 runtime counter와 인덱스 접근을 확인했습니다.

| 조회 | 반환 행 | Logical reads |
|---|---:|---:|
| 플레이어 경기 목록 | 20 | 59 |
| 플레이어 통계 | 1 | 43 |
| 랭킹 저장 프로시저 | 2 | 88 |

위 수치는 해당 데이터 분포에서의 단일 측정 결과입니다. 처리량 상한이나 부하 테스트 결과를 뜻하지 않습니다. 측정 조건과 실행 계획 요약은 [실환경 보고서](docs/aws-validation.md#대표-sql-실행-계획측정)에 기록했습니다.

코드: [SQL 저장소](src/ShooterRecordsLab.Infrastructure/Repositories/SqlRecordsRepository.cs) · [랭킹 저장 프로시저](src/ShooterRecordsLab.Infrastructure/Persistence/Sql/V001_GetTopPlayerRankings.sql)

### 4. 인증·오류 처리와 배포 검증

- **JWT Bearer 인가:** 서명·발급자·대상·유효기간을 검증하고, 경기 등록에는 정확한 `matches.write` scope를 요구합니다. 인증 실패 `401`과 권한 부족 `403`을 구분합니다.
- **오류와 관측:** 공통 `ProblemDetails`와 `traceId`, 구조화된 요청 로그를 사용합니다. 토큰·연결 문자열·내부 예외는 응답과 요청 로그에 노출하지 않습니다.
- **헬스체크:** 앱 생존과 DB 준비 상태를 분리하고, DB 연결 실패 및 잘못된 배포 설정을 주입해 장애 응답과 복구를 확인했습니다.
- **인프라 재현:** Terraform, 버전·digest를 고정한 Docker 이미지, 비root 컨테이너를 구성했습니다. RDS는 비공개로 두고 SSM 경유로 검증했으며, DB 연결의 TLS 인증서 검증을 유지했습니다.

코드: [JWT 검증·인가](src/ShooterRecordsLab.Api/Authentication/JwtAuthentication.cs) · [인증 테스트](tests/ShooterRecordsLab.IntegrationTests/AuthenticationTests.cs) · [Dockerfile](infra/docker/Dockerfile) · [Terraform](infra/terraform/README.md)

## 기술과 구조

| 영역 | 사용 기술 |
|---|---|
| API | .NET 10, ASP.NET Core Controllers, C# |
| 데이터 | EF Core 10, SQL Server 2022 Express, T-SQL |
| 테스트 | xUnit, WebApplicationFactory, 실제 SQL Server 통합 테스트 |
| 배포 | AWS EC2·RDS·ECR·Secrets Manager·SSM, Docker, Terraform |
| 재현성 | .NET SDK 고정, NuGet lock 파일, EF 도구 버전·컨테이너 digest 고정 |

```text
src/
  ShooterRecordsLab.Api             HTTP 계약, 인증, 오류 처리, 헬스체크
  ShooterRecordsLab.Application     유스케이스, 입력 규칙, 멱등성 판단
  ShooterRecordsLab.Domain          도메인 모델
  ShooterRecordsLab.Infrastructure  EF Core, 트랜잭션, Migration, T-SQL
tests/
  ShooterRecordsLab.UnitTests
  ShooterRecordsLab.IntegrationTests
tools/ShooterRecordsLab.Validation 명시적 DB 준비·배포 검증 도구
infra/                             Docker, Terraform, 검증 스크립트
```

Controller는 HTTP 처리를, Application은 규칙을, Infrastructure는 DB 접근을 담당합니다. API DTO와 EF Entity를 분리하고, I/O에는 async API와 `CancellationToken`을 사용합니다. 의존 관계와 설계 근거는 [아키텍처](docs/architecture.md)에 정리했습니다.

## 주요 API

| 메서드 | 경로 | 기능 |
|---|---|---|
| GET | `/api/players/{playerId}` | 플레이어 조회 |
| GET | `/api/players/{playerId}/matches` | 경기 이력·페이지네이션 |
| GET | `/api/players/{playerId}/stats` | 승패·킬/데스·승률·K/D 집계 |
| GET | `/api/matches/{matchId}` | 경기와 참가자 결과 조회 |
| POST | `/api/matches` | JWT 인가를 적용한 멱등 경기 등록 |
| GET | `/api/rankings?top=20` | 승수·킬 수 기준 랭킹 |
| GET | `/health/live`, `/health/ready` | 앱 생존·DB 준비 상태 |

요청·응답 예제, 입력 범위, 정렬과 오류 코드는 [API 계약](docs/api-contract.md)에서 확인할 수 있습니다.

## 로컬 빌드·실행

필수 환경은 Git과 **.NET SDK 10.0.402**입니다. `global.json`에 따라 같은 기능 밴드의 최신 패치를 허용합니다. SQL Server·Docker·AWS 계정 없이 빌드와 비DB 테스트를 실행할 수 있습니다.

```bash
git clone https://github.com/CodingBot000/ShooterRecordsLab.git
cd ShooterRecordsLab
dotnet tool restore
./infra/scripts/verify.sh
```

검증 스크립트는 locked restore → 경고를 오류로 처리하는 build → 비DB 테스트 → format 검사를 순서대로 실행하고, 첫 실패에서 종료합니다. 로그는 Git에서 제외한 `artifacts/validation/`에 저장합니다.

```bash
dotnet run --project src/ShooterRecordsLab.Api --launch-profile http
# 별도 터미널
curl -i http://127.0.0.1:5080/health/live
curl -i http://127.0.0.1:5080/health/ready
```

DB 설정이 없으면 live는 `200`, ready는 `503`입니다. Development의 OpenAPI는 `http://127.0.0.1:5080/openapi/v1.json`에서 확인할 수 있습니다. 데이터 API에는 별도 SQL Server 설정이, 경기 등록에는 JWT 설정과 쓰기 권한이 필요합니다. 실제 접속 정보와 키는 저장소에 넣지 않고 외부 설정으로 제공합니다.

실제 DB 테스트는 전용 Test DB와 대상 설정이 필요하며, 위 비DB 검증에 포함되지 않습니다. 준비·Migration·테스트 절차는 [DB 검증 안내](docs/database-validation.md), 클라우드 재현 절차와 비용 조건은 [AWS 배포 지침](docs/aws-deployment.md)과 [Terraform 안내](infra/terraform/README.md)를 따릅니다.

## GitHub Actions CI

[CI workflow](.github/workflows/ci.yml)는 `main` 대상 PR의 생성·갱신, `main` push, 수동 실행에서 기존 `infra/scripts/verify.sh`를 호출합니다. `global.json`으로 SDK를 선택하고 locked restore → 경고를 오류로 처리하는 build → 비DB 테스트 → format을 검사합니다. 실패하면 해당 실행이 실패하며, 생성된 로그는 Actions 실행 화면의 `verification-logs`에서 다운로드할 수 있습니다.

실제 DB 테스트는 이 CI에서 **NOT_RUN**이며 실행 요약에도 표시합니다. AWS·DB 자격증명이나 배포 작업은 포함하지 않습니다. 필수 검사 이름은 `verify`입니다. `main` 보호 설정과 실제 실행 근거는 [진행 기록](docs/project-status.md), 결과 리뷰·문서 반영 절차는 [테스트 지침](docs/testing-guidelines.md)을 따릅니다.

## 추가 문서

| 문서 | 내용 |
|---|---|
| [AWS 실환경 보고서](docs/aws-validation.md) | 배포 소스·실행 결과·SQL 측정·장애 복구·검증 한계 |
| [검증 결과 JSON](docs/validation/aws-validation/report.json) | 소스 commit·이미지 digest·검증 항목·수치 |
| [의사결정 기록](docs/decisions.md) | 기술 선택과 변경 이유, 실제 DB 오류 수정 |
| [개발계획](docs/development-plan.md) / [진행·검증 기록](docs/project-status.md) | 단계별 목표와 구현·리뷰·검증 이력 |

가상의 게임·플레이어 데이터만 사용한 개인 프로젝트이며, 실제 게임 서비스의 계정·기록과 연동하지 않습니다. 구현 범위는 백엔드 API와 배포·검증 도구이며 프론트엔드는 포함하지 않습니다.
