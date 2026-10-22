# Architecture — ShooterRecordsLab

> 설계 유형: 단일 배포 모놀리스(계층 분리) · 목표: 명시적 도메인 규칙, 정합성, 테스트 가능성

## 1. 논리 구성

```text
Mac (개발자 / Codex)
  ├─ .NET 10 SDK / API 프로세스 / unit + HTTP tests
  └─ AWS CLI + Session Manager plugin
        │ local TCP 11433 → AWS SSM 암호화 채널
        ▼
AWS ap-northeast-2 VPC (최종 리전은 승인 시 확인)
  ├─ EC2 Linux (SSM managed, 인바운드 기본 차단)
  │    ├─ SSM 원격 호스트 포트 포워딩 중계
  │    └─ [P5] ASP.NET Core Docker API (127.0.0.1:8080)
  │                    │
  │                    └─ private VPC TCP 1433
  └─ RDS SQL Server 2022 Express (private, Publicly accessible=No, Single-AZ)
         └─ ShooterRecordsLabDev / ShooterRecordsLabTest (격리 DB)
```

첫 배포 단계까지 API는 Mac에서 실행하고 RDS만 AWS에 둔다. P5에 동일 API 이미지를 EC2에 배포한다. **데이터베이스는 EC2 Docker가 아닌 RDS**에 존재한다.

## 2. 요청 흐름과 책임

```text
HTTP request
  → ASP.NET Core middleware (exception handling, tracing, auth)
  → Controller (HTTP DTO 유효성 검사, 상태 코드)
  → Application use case/service (규칙, 권한/트랜잭션 조정)
  → Infrastructure repository (EF Core DbContext / T-SQL proc)
  → RDS SQL Server
  → Response DTO / ProblemDetails
```

- Controller: 라우트 바인딩, 기본 검증, Response mapping 외 도메인 규칙 최소화.
- Application: 경기 참가자 규칙, 멱등성 판단, 트랜잭션 경계, `CancellationToken` 전달, 결과 매핑.
- Domain: Player, Match, MatchResult 및 값/규칙 정의. API/EF 기술에 의존하지 않음.
- Infrastructure: EF Core 모델/DbContext/Migrations, 쿼리, unique/FK/인덱스, Stored Procedure 호출.
- API는 Domain Entity를 직접 직렬화하지 않음. DTO에는 PII를 넣지 않는다.

## 3. 저장소/프로젝트 구조

```text
ShooterRecordsLab.slnx
AGENTS.md
README.md
src/
  ShooterRecordsLab.Api/
    Controllers/
    Contracts/                  # API request/response DTO
    Middleware/                 # 공통 오류 처리 등
    Program.cs
  ShooterRecordsLab.Application/
    Abstractions/               # 필요한 저장소/시계/외부 의존 인터페이스
    Players/
    Matches/
    Rankings/
  ShooterRecordsLab.Domain/
    Entities/
    Enums/
    Rules/
  ShooterRecordsLab.Infrastructure/
    Persistence/
      ShooterRecordsDbContext.cs
      Configurations/
      Migrations/
      Sql/                     # 검토 가능한 T-SQL 원본
    Repositories/
tests/
  ShooterRecordsLab.UnitTests/
  ShooterRecordsLab.IntegrationTests/
infra/
  terraform/                   # P3/P5, 승인 전 apply 금지
  docker/                      # P5
  scripts/                     # 재현 가능/멱등적 로컬·운영 스크립트
docs/
  init/                        # 변경 금지 원본
  architecture.md
  development-plan.md
  api-contract.md
  dotnet-guidelines.md
  testing-guidelines.md
  review-guidelines.md
  aws-deployment.md
  security-checklist.md
  decisions.md
  project-status.md
```

의존성 방향: `Domain → (none)`; `Application → Domain`; `Infrastructure → Application + Domain`; `Api → Application + Infrastructure`. 테스트 프로젝트는 해당 대상 프로젝트를 참조한다. Domain/Application에서 ASP.NET Core/EF 타입을 직접 노출하지 않는 것이 원칙.

## 4. 개념 데이터 모델

```text
Player (Id PK, DisplayName, CreatedAtUtc)
  1 ─────── N MatchResult (Id PK, PlayerId FK, MatchId FK,
              Kills, Deaths, Outcome)
                      N ─────── 1 Match (Id PK, ExternalMatchId UNIQUE,
                                        PlayedAtUtc, CreatedAtUtc,
                                        PayloadFingerprint)
```

### 데이터 제약

- ID: `uniqueidentifier` (C# `Guid`); 실제 비즈니스/가짜 Player 데이터 구분.
- 시간: C# `DateTimeOffset`, SQL Server `datetimeoffset(7)`. UTC 값만 유효하도록 서비스 검증.
- `ExternalMatchId`: 제한된 `nvarchar(64)` + 정규화 후 DB UNIQUE 제약. 앱 단의 조회만으로 중복을 막지 않는다.
- `PayloadFingerprint`: canonicalized request의 해시(예: SHA-256)로 멱등성을 판별. 참가자 배열 순서가 달라도 동등할지에 대한 규칙을 API 계약에서 고정.
- `MatchResult`: UNIQUE(MatchId, PlayerId), 양의 범위를 포함한 정수 값 및 CHECK(Kills >= 0, Deaths >= 0).
- `Player.DisplayName`: 필수·길이 제한. 실명 등 개인정보는 저장하지 않는다.
- 인덱스 후보: `(PlayerId, MatchId)`(UNIQUE/Multi-column), `Match(PlayedAtUtc DESC, Id DESC)`, `ExternalMatchId UNIQUE`; 실제 쿼리 플랜을 보고 조정.
- 초기에는 통계 캐시/누적 랭킹 테이블을 만들지 않고 원본 MatchResult를 집계한다.
- 결과는 `Win`, `Loss`, `Draw`; 서로 모순되는 경기 참가자 결과를 유효성 규칙으로 제한한다. 복잡한 팀 구조는 MVP 제외.

## 5. 트랜잭션 / 멱등성

`POST /api/matches` (작성 권한 필요):

1. DTO와 참가자 유일성·수치·UTC 유효성 검사. canonical payload를 생성하고 fingerprint 계산.
2. DB 트랜잭션 안에서 `Match` + 모든 `MatchResult`를 저장한다.
3. `ExternalMatchId` UNIQUE가 최종 정합성 방어선이다. 같은 ID의 경쟁 요청 중 하나만 insert 성공해야 한다.
4. unique 예외가 나면 **해당 키로 기존 DB 데이터를 조회**해 fingerprint를 비교한다. 동일하면 기존 리소스 반환(200), 다르면 409.
5. 최초 생성 201 + `Location` 응답. 같은 ID의 같은 내용 재시도는 200. 원인 불명 DB 실패는 안전하게 오류를 반환하고 결과를 확인한다.
6. `DbUpdateException`을 무조건 conflict로 바꾸지 않는다. SQL Server unique 오류 번호 등 특정 제약 위반을 식별해야 한다.
7. EF Core execution strategy의 자동 재시도와 사용자 정의 트랜잭션은 함께 사용할 때 공식 가이드를 따른다. 원인이 불명확한 POST 재실행은 unique 키를 통해 멱등성을 보장한 뒤에만 허용한다.

**운영과제:** SaveChanges 한 번에 원자성이 확보되더라도, 연관된 비즈니스 검증/예외/재시도 흐름에서 실제 SQL 트랜잭션의 범위를 테스트한다.

## 6. 조회 및 성능

- 매치 목록은 `PlayedAtUtc DESC, MatchId DESC`의 결정적 정렬 + `page`, `pageSize`(최대 100). 1 기반 번호.
- `AsNoTracking`, DTO projection, 필요한 인덱스, 쿼리 실행 계획의 logical reads/duration 확인. 무분별한 Include 금지.
- 랭킹은 `dbo.GetTopPlayerRankings(@Top)`를 사용. `@Top` 유효 범위 1~100, 결과 정렬 `Wins DESC, Kills DESC, PlayerId ASC` (동률 규칙 포함).
- 통계는 `TotalMatches, Wins, Losses, Draws, Kills, Deaths`, `WinRate`, `KdRatio`. 0 경기이면 WinRate null, 0 deaths이면 KdRatio null로 정의한다. 반올림 정책은 `api-contract.md`.
- 프로시저의 출력이 EF Core keyless model 등을 통해 안전하게 매핑되도록 구성하고 DB 테스트로 검증.

## 7. 인증·오류·관측

- 공개적으로 읽을 수 있는 **가상 데이터 GET** (추후 변경 가능). 경기 등록은 JWT Bearer의 `matches.write` 권한 필요. 토큰 발급 시스템은 별도의 신뢰된 발급자 또는 개발 환경 테스트 전용.
- middleware 순서(P1): 라우팅(프레임워크) → 요청 로깅 래퍼 → 예외 처리 → 상태 코드 ProblemDetails → Controller. 로깅 래퍼가 예외 처리 이후의 최종 status를 기록한다. P4에서 인증 → 인가 순서를 추가한다.
- HTTP 오류는 `application/problem+json`(ProblemDetails). 내부 SQL/예외 내용 미노출.
- `GET /health/live`: 앱 프로세스 liveness. `GET /health/ready`: DB 연결 기반 readiness, 민감 내용 미노출, 짧은 timeout. 공개 시 접근 제어/노출 최소화 검토.
- 구조화 로깅에 trace/correlation ID, route, status, duration. 토큰, 인증 헤더, DB 연결문자열, 사용자 데이터 원문 로그 금지.
- 운영 DB Migration은 앱 부팅에서 자동 수행하지 않음. 승인을 거친 별도 배포 과정으로 실행.

## 8. 의도적으로 하지 않는 것

- Generic Repository 프레임워크, 모든 도메인에 대한 CQRS, MediatR 등의 추가 추상화.
- 다른 SQL dialect로 대체하여 MSSQL 적합성 검증했다고 간주하는 것.
- 로컬 Mac에서 unsupported SQL Server ARM 컨테이너를 기본 DB로 강제하는 것.
- 운영 데이터를 대상으로 한 테스트/Seed/DB 초기화.

모호한 기술 선택은 `docs/decisions.md`에 근거·대안을 기록하고, 계약을 변경했다면 API 문서와 테스트도 동시에 수정한다.

## P2 구현 상태

- API는 항상 Scoped SQL Server DbContext/Repository를 등록한다. P1 Fake는 `tests/.../Fakes`에만 남겼다. 읽기 API는 DB 연결이 필요하며 JWT 전 쓰기는 Development에서만 처리한다.
- SQL 매핑: dbo 스키마, 외부 ID `nvarchar(64)`/BIN2 UNIQUE, SHA-256 `char(64)`, bigint 집계, datetimeoffset(7)/UTC CHECK, 수치 범위·Outcome CHECK, 참가자 복합 고유 키와 covering index.
- design-time factory는 연결 문자열 없이 Migration/SQL만 생성한다. 앱은 migrate/seed하지 않는다. 상세 적용·롤백·DB 테스트 안전장치는 `docs/database-validation.md`에 기록한다.
- API에서 EF 상세 로그를 억제하고 중앙 오류 핸들러에 예외 타입/trace만 기록한다. SQL·연결·예외 원문을 공개하지 않으므로 P4 운영 관측성 점검에서 진단 수준을 재검토해야 한다.
- EF command timeout 30초와 health timeout 3초는 기술적 초기값이며 용량·성능·SLA의 사용자 승인 기준이 아니다. P4 실측 후 조정한다.
