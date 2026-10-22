# 개발계획서 — ShooterRecordsLab

> 버전 1.0 · 작성일 2026-10-22 · 개발 주체: Codex + 사용자 검토 · 프론트엔드 구현 제외

## 1. 배경과 목표

넥슨 슈터플랫폼실 웹 애플리케이션 포지션에 대비해 .NET/C# 웹 백엔드 및 MSSQL 적응 역량을 직접 구현으로 검증한다. 실제 넥슨 서비스/코드/데이터와는 **전혀 관계없는 가상 게임 기록실**을 만든다. Spring Boot 경험의 재사용과 .NET 특유의 차이(DI 생명주기, Middleware, Task/async, LINQ/EF Core, DB 트랜잭션)를 중점 학습한다.

### 결과물

- .NET 10 컨트롤러 기반 REST API (Swagger UI 필수 아님, 개발용 OpenAPI 문서)
- 플레이어/경기/참가자·결과의 MSSQL 모델, EF Core Migration 및 데이터 접근
- 랭킹 조회용 MSSQL Stored Procedure 최소 1개
- 중복 경기 업로드를 방지하는 멱등성 및 트랜잭션
- 안전한 인증·인가, 입력 검증, 통일된 오류 응답, 구조화된 로그
- 단위 테스트, 실제 SQL Server 통합 테스트, 기본 성능 측정 근거
- Mac 로컬 개발 + AWS RDS for SQL Server 2022 Express + AWS EC2 Docker 실행
- 프로젝트 README, 아키텍처, 실행·검증·배포 문서, 최종 성과 요약

### 제외 범위

- 실제 게임 클라이언트 연동, 넥슨 로그인/라이브 데이터, 아이템 결제·거래, 대규모 랭킹 서비스, 프론트엔드, MSA, Kafka, Redis, Kubernetes, .NET Framework.
- GitHub Actions 자동 배포는 마지막에 **선택 사항**. 도입하더라도 OIDC/승인 게이트를 우선한다.

## 2. 권장 기술 스택

| 영역 | 선택 | 이유 |
|---|---|---|
| Runtime | .NET 10 LTS / C# | 현재 장기지원 릴리스, Mac/Linux 지원 |
| HTTP API | ASP.NET Core Controllers | Spring MVC와 비교 학습 용이 |
| 데이터 접근 | EF Core 10 SQL Server provider | ORM/LINQ/트랜잭션 학습 |
| RDBMS | AWS RDS SQL Server 2022 Express | MSSQL 실습, 관리형 DB, AWS 활용 |
| DB 추가 기술 | T-SQL Stored Procedure + 적정 인덱스 | 넥슨 공고 우대 역량 대응 |
| 단위 테스트 | xUnit | 생태계 표준 중 하나 |
| API 통합 | `WebApplicationFactory` | ASP.NET Core HTTP 통합 테스트 |
| DB 통합 | 전용 RDS 개발/테스트 DB(격리) | MSSQL 고유 동작 확인 |
| 배포 | Docker + EC2 + ECR(후반부) | 최소 운영 경로 익히기 |
| 네트워크 | Private RDS + SSM 포트 포워딩 | DB 퍼블릭 노출 방지 |
| IaC | Terraform (실행은 별도 승인) | 인프라 재현성 및 검토 가능성 |
| 로그 | `ILogger` JSON 구조화, trace/correlation id | 운영 장애 분석 기초 |

**가정/미결:** AWS 계정 및 리전 최종 확인, 비용 상한, 실제 인스턴스 크기, 도메인, 인증 issuer. Codex는 임의 결정 후 유료 생성하지 않는다. 리전 초기 제안은 `ap-northeast-2`.

## 3. 기본 기능 요구사항

| ID | 기능 | 중요도 | 완료 기준 |
|---|---|---|---|
| F01 | 플레이어 상세 조회 | 필수 | 존재/미존재/검증/응답 계약 테스트 |
| F02 | 플레이어 경기 목록 | 필수 | 안정적 정렬·페이지·최대 크기 제한 |
| F03 | 통계(총경기, 승, 패, 킬, 데스, 승률, K/D) | 필수 | 0경기/0데스 처리 정의와 테스트 |
| F04 | 경기 결과 등록 | 필수 | 권한 검증, 모든 참가자 원자적 기록 |
| F10 | 경기 상세 조회 | 필수 | 생성 후 Location의 대상, 정렬된 참가자/404 검증 |
| F05 | 중복 경기 등록(멱등성) | 필수 | 같은 외부 ID·동일 내용 재시도는 중복 삽입 안 함; 다른 내용은 409 |
| F06 | 상위 랭킹 조회 | 필수 | 성능 고려한 T-SQL Stored Procedure, 안정적 순서 |
| F07 | OpenAPI 및 기본 health endpoints | 필수 | 개발에서 명세 확인, liveness/readiness 분리 |
| F08 | JWT Bearer 쓰기 권한 | 필수(배포 전) | 유효/만료/서명 오류/권한 없음 테스트 |
| F09 | 인덱스/쿼리 성능 기초 점검 | 필수 | 대표 쿼리 실제 실행 계획/측정 기록 |

세부 API 요청·응답·에러 코드는 `docs/api-contract.md`가 유일한 계약이다.

## 4. 단계별 계획 (각 단계마다 검증 게이트)

### P0 — 문서 배치, 저장소·솔루션 부트스트랩 (AWS 비용 0)

- `docs/init/START_HERE.md`에 따라 원본 문서를 대상에 복사. Git 저장소가 없으면 초기화하고 루트 `AGENTS.md`를 적용.
- `ShooterRecordsLab.slnx`, `src/{Api,Application,Domain,Infrastructure}`, `tests/{UnitTests,IntegrationTests}` 프로젝트 생성.
- 프로젝트 참조 방향, `.gitignore`, `.editorconfig`, nullable/analyzer, `global.json`을 구성; 불필요한 패키지 회피.
- 컨트롤러 Web API, 개발 환경의 OpenAPI, 상태 체크 기본 틀 설정.
- `README.md`에 macOS 개발 사전 조건과 실행 방법 기록.
- **Gate:** fresh clone 기준 `dotnet restore` / `dotnet build -warnaserror` / DB 미사용 단위 테스트 / format 확인이 성공해야 한다. 실제 실행 로그를 status에 기록.

### P1 — Domain/Application/HTTP API 기본 구현 (AWS 비용 0)

- Player, Match, MatchResult 도메인 모델과 API DTO, 예외/검증 정책 설계.
- Controller → Application Service → 인메모리 개발용 Fake Repository의 학습용 구조로 F01~F04/F10의 happy path 우선 구성. Fake는 **P2에서 실DB로 교체**하며 최종 프로덕션 경로에 남기지 않는다.
- 입력 검증, CancellationToken, ProblemDetails, 구조화된 로그, 간단한 in-process 예외 처리.
- 단위 테스트, HTTP 통합 테스트(가짜 저장소) 작성. 사용 예와 curl 샘플 정리.
- **Gate:** 해당 endpoint/요청응답 계약, 400/404/500 사례, 정상 데이터 없음/0데스 등 경계 조건 확인.

### P2 — EF Core + MSSQL 모델 및 마이그레이션 작성 (리소스 생성 전)

- EF Core 10 SQL Server provider, DbContext Scoped 등록, 엔티티 Fluent API 매핑, 명확한 데이터 타입·제약·인덱스.
- `Player`, `Match`, `MatchResult` 및 ExternalMatchId 고유 제약; 모든 쓰기 로직에 트랜잭션과 중복 예외 처리 설계.
- Migration 스크립트를 생성하고 SQL 변경을 사람이 리뷰할 수 있게 출력. 자동으로 AWS DB에 적용하지 않는다.
- Repository/SQL 의존 구현 작성, 비동기 쿼리/DTO projection/AsNoTracking/페이지네이션.
- `dbo.GetTopPlayerRankings` 프로시저의 versioned migration/script와 호출 코드 작성, 파라미터 안전성 확인.
- **Gate:** 컴파일/정적 분석/비DB 테스트 성공. RDS 연결 전의 DB 통합 테스트는 **미실행**으로 명시한다(성공으로 표기 금지).

### P3 — AWS DB 생성·연결·통합 검증 (**사용자 승인 필수**)

- 우선 `docs/aws-deployment.md` 기준으로 리전·예산·인스턴스 옵션·예상 비용·네트워크 구성을 사용자에게 제시.
- 승인 전에는 Terraform **계획/코드만** 작성. 승인 후 private RDS SQL Server Express + 보안 그룹 + SSM 관리 EC2를 최소 구성으로 만들기.
- 로컬 Mac은 `AWS-StartPortForwardingSessionToRemoteHost`를 통해 RDS에 연결. 비밀번호는 user-secrets/보안 저장소로 구성.
- 분리된 dev/test DB(또는 별도 접속 계정/격리 전략)에서 Migration, seed, DB 통합 테스트를 수행.
- 실패/롤백/정리·중지 가이드를 작성; 작업 종료 후 리소스 중지/삭제 여부를 승인받는다.
- **Gate:** 실제 SQL Server에서 CRUD, 중복 경기 동시 등록, FK/고유 제약, Stored Procedure, 인덱스 검증의 실행 결과 기록.

### P4 — 보안·안정성·운영 품질 강화

- `POST /api/matches`에 JWT Bearer 검증 및 writer 권한 적용. 발급 로직을 임의의 프로덕션 인증 서버인 것처럼 만들지 않는다.
- DB 트랜잭션, 고유 키 충돌, 동시성, 재시도 정책을 검증. SQL 실행 결과가 이미 커밋되었을 수 있는 상황에 무제한 재시도 금지.
- `ExceptionHandler`/ProblemDetails, 요청 취소, 타임아웃, rate limiting(공개 단계 필요 시), liveness/readiness, 로그/traceId 구성.
- 기본 성능 점검(예: 1만 개 경기 결과의 조회/랭킹; 데이터 양은 비용/성능에 따라 조정). 측정 환경·SQL 실행 계획을 함께 보존.
- **Gate:** 인증 401/403, 입력 400, 중복 200/409, 404, DB 오류 5xx, 동시성, 결과 일관성, 보안 체크리스트를 검증.

### P5 — AWS EC2 Docker 배포 (**별도 승인 필수**)

- Linux x86_64 API Dockerfile 작성, 비root 사용자, healthcheck, 버전 고정 이미지, 최소 포트 노출.
- 배포 대상 EC2(앞 단계 SSM 관리 EC2 재사용 가능)에서 ECR image pull 후 컨테이너 실행. RDS는 SG를 통한 private endpoint 연결.
- 공개 HTTP 대신 첫 배포 검증은 **SSM 로컬 포트 포워딩**으로 실시. HTTPS 공개 배포가 필요하면 도메인/인증서/리버스 프록시 또는 ALB/보안 설정을 별도 승인받는다.
- 로컬/운영 환경변수 분리, 시크릿 안전한 주입, 장애 시 로그/컨테이너 상태 확인, 롤백 절차 작성.
- **Gate:** EC2 실제 API 응답, RDS 연결, auth/health, 실패 배포 복구, 공개 포트/비밀 유출 점검 증거 확보.

### P6 — 최종 검증 및 채용 대비 정리

- 전체 unit + HTTP integration + 실제 MSSQL integration, release build, format/analyzers, 의존성/설정/로그 점검.
- README에 Mac에서 시작해 AWS까지 실행하는 재현 절차와 **실제 성공/실패 결과** 수록.
- `docs/review-guidelines.md` 기준으로 별도 검증 패스, 미해결 이슈 및 위험 기록.
- 면접용으로 Spring Boot와의 비교(DI/Middleware/async/EF/MSSQL/배포), 발생한 장애 및 해결 과정 정리.
- **Gate:** 모든 필수 기능·테스트·배포 검증의 증거가 있고 미검증 항목이 명시되어야 완료.

## 5. 기술적으로 반드시 검증할 실패 시나리오

1. 같은 `externalMatchId`로 여러 요청이 동시에 들어온다 → DB 유니크 제약에 의해 1개만 저장.
2. 동일 ID로 동일 페이로드 재요청 → 성공 재응답, **중복 경기 저장 없음**.
3. 동일 ID로 다른 페이로드 재요청 → 409 Conflict.
4. 경기 중 한 참가자 ID가 없거나 중복이면 전체 저장 롤백.
5. 권한 없는 POST는 401/403, GET 정책은 문서와 일치.
6. 패배/무승부/0데스/0경기 처리 수학적으로 안정적이고 문서화.
7. 페이지 경계, 대량 데이터 조회의 정렬 결정성, 쿼리 실행 계획 확인.
8. DB 연결이 끊겨도 민감 정보를 노출하지 않고 적절한 에러를 반환.
9. 테스트 DB 초기화 명령이 운영 DB에 적용되지 않도록 가드.
10. AWS DB는 공개 접근하지 않으며 1433 인바운드는 EC2의 SG에서만 허용.

## 6. 종료/산출 기준

완료 여부는 단순 소스 작성이 아니라 **실행 증거**로 결정한다. 다음이 존재해야 한다.

- 작동하는 .NET 10 API 코드와 NuGet·SDK 구성
- SQL Server EF Core Migration + SP + 인덱스와 실행 증거
- 유닛·HTTP·DB 테스트 실행 명령과 통과 건수/실패 내역
- 승인된 AWS 리소스 목록·비용/정리 계획과 배포/접근 확인
- 변경과 결정 기록, 테스트 한계, README의 재현 절차
- 허위로 적지 않은 학습/면접용 요약

일정은 AWS 계정 상태, RDS 과금, 인증/네트워크 문제에 따라 달라진다. 일정에 맞추기 위해 실패 검증을 생략하지 않는다.
