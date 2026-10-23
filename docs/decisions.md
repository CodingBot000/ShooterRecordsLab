# 기술 의사결정 기록 (ADR Lite)

> 확정 결정은 아래 형식으로 작성하고 바뀐 이유를 남긴다. 적용 범위·작성 날짜·결정자·대안을 기록한다.

## ADR-001 — ASP.NET Core Controller 기반 .NET 10 모놀리스

- 날짜: 2026-10-22 / 상태: 채택(학습용 기본설계)
- 결정: .NET 10 LTS + ASP.NET Core 10의 Controller Web API, 4계층(HTTP/Application/Domain/Infrastructure)으로 구성.
- 근거: Spring Boot 개발 경험과 비교가 쉽고 DI/Middleware/HTTP 테스트를 실습하기 좋음. .NET 10 LTS 지원 기간이 길고 Mac 개발 가능.
- 대안: Minimal API(작은 API에 간결), .NET Framework(레거시 Windows 의존). 이번 목표는 현대적 .NET 웹서비스이므로 채택하지 않음.
- 유의: 기술 스택은 이 프로젝트의 학습 목표에 맞춰 선정했으며, 특정 기업의 내부 구성을 전제하지 않는다.

## ADR-002 — 데이터베이스는 AWS RDS SQL Server 2022 Express

- 날짜: 2026-10-22 / 상태: 채택(인프라 실행은 별도 승인)
- 결정: RDS Private SQL Server 2022 Express; Mac은 SSM managed EC2 원격 호스트 포트 포워딩. EC2는 이후 API 배포에 재사용 가능.
- 근거: MSSQL T-SQL/Stored Procedure/EF Core 학습, Mac Apple Silicon의 SQL Server x86 Docker 에뮬레이션 리스크 제거, 관리형 DB.
- 대안: EC2의 SQL Server Docker(비용/DB 백업 운영 부담); Mac ARM Docker(비공식 호환 이슈); 퍼블릭 RDS(보안 위험). 비용이 예상보다 높으면 EC2 DB 대안으로 **사용자와 재결정**.
- 위험: RDS + EC2의 상시 비용, SSM 터널 설정 복잡도, Express 기능 제한. 승인 전 견적 필수.

## ADR-003 — EF Core 중심 + 순수 T-SQL Stored Procedure 1개

- 날짜: 2026-10-22 / 상태: 채택
- 결정: CRUD/통계는 EF Core, 랭킹은 별도 T-SQL `dbo.GetTopPlayerRankings(@Top)`.
- 근거: .NET ORM/LINQ와 MSSQL 저장 프로시저를 함께 익히고 각 선택의 성능·검증 차이 비교 가능.
- 대안: 전부 EF Core(Stored Procedure 학습 부족), 전부 Dapper/ADO.NET(EF Core 학습 부족).
- 위험: ORM 쿼리 비용/EF mapping과 SP 결과 타입 불일치. 실제 RDS integration + 쿼리 계획으로 검증.

## ADR-004 — private-first AWS 배포

- 날짜: 2026-10-22 / 상태: 채택
- 결정: AWS EC2 Docker API는 처음에 public HTTP 서비스로 열지 않고 SSM 포트 포워딩으로 검사.
- 근거: 실습 서비스의 외부 공개 요구 없음. HTTPS/DNS/ALB/NAT 등 비용·공격면을 줄임.
- 대안: ALB+ACM+도메인 또는 EC2 reverse proxy 통한 HTTPS. 공개 요구가 확정되면 재검토.
- 제약: 외부 웹 클라이언트가 접속하는 완전한 서비스 운영 연습은 범위에 포함하지 않음.

## 새 ADR 작성 템플릿

```text
## ADR-XXX — 제목
- 날짜: YYYY-MM-DD / 상태: proposed | adopted | superseded
- 맥락/문제:
- 결정:
- 근거:
- 대안/기각 이유:
- 단점/위험:
- 영향을 받는 파일/테스트:
- 사용자 승인 여부(비용·보안·외부 노출 등일 때):
```

## ADR-005 — P0 SDK 고정과 DB 미구성 상태

- 날짜: 2026-10-22 / 상태: 채택(로컬 기술 구성)
- 결정: 공식 .NET 10 SDK 10.0.402(macOS Arm64, runtime 10.0.13)를 사용하고 `global.json`은 같은 기능 밴드의 최신 패치만 허용한다. NuGet 정확 버전과 `packages.lock.json`을 보존한다.
- 근거: 최초 PATH에 SDK가 없었고 Microsoft 공식 release metadata와 다운로드 SHA-512를 검증해 사용자 경로에 설치했다. 시스템/셸 설정은 변경하지 않았다.
- 대안: 전역 Homebrew/시스템 설치 대신 프로젝트 검증에 필요한 독립 사용자 경로를 사용한다.
- 패키지: Microsoft.AspNetCore.OpenApi 및 Mvc.Testing 10.0.13(Microsoft/.NET, MIT), xUnit 2.9.3(Apache-2.0), runner 3.1.4 및 Microsoft.NET.Test.Sdk 17.14.1(MIT). SDK 템플릿의 coverage collector는 현재 불필요해 제외했다. 런타임 제3자 프레임워크 대신 ASP.NET Core 내장 기능을 사용한다.
- 상태 체크: P0 readiness는 DB 검증을 수행할 수 없으므로 503을 반환한다. liveness 200과 분리한다. P2에서 실제 접속 검사로 교체한다.
- 검증 환경: 도구 호출 수명을 넘겨 재사용되는 MSBuild 자식 노드로 추정되는 MSB4166을 관찰했다. 원인은 확정하지 않았으며 `MSBUILDDISABLENODEREUSE=1`로 노드 재사용을 끈 후 기본 병렬 빌드가 성공했다. 검증 스크립트에 이 환경변수를 포함했다.
- 승인: 로컬 구성·테스트만 수행. AWS 조회·유료 변경·배포·DB 접근 없음.
- 공식 근거: https://dotnet.microsoft.com/en-us/download/dotnet/10.0 및 https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json, https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0

## ADR-006 — P1 Fake와 HTTP 계약 경계

- 날짜: 2026-10-23 / 상태: 채택(P1 한정, P2에서 소스 Fake 제거)
- 결정: Application의 구체적 기록 저장소 인터페이스 1개와 Scoped 서비스, 개발 전용 메모리 Fake로 HTTP 흐름을 검증한다. Generic Repository를 도입하지 않는다.
- Fake의 공유 저장소는 단일 lock으로 원자적 삽입/재시도를 모사한다. 이는 DB 동시성 증거가 아니다. Production에는 Fake/무인증 Controller 경로를 매핑하지 않는다.
- 참가자 정렬·UTC ticks·정규화 ID·불변 정수 형식의 SHA-256으로 payload를 비교한다. 웹 DTO/도메인/조회 모델은 분리한다.
- 오류는 IExceptionHandler와 ProblemDetails로 중앙 처리한다. .NET 10 handled diagnostics는 억제하고 예외 타입/trace만 안전하게 기록한다. 요청 로그 래퍼는 예외 처리 바깥에서 최종 status를 기록하며 본문·Authorization·쿼리 문자열을 기록하지 않는다.
- 패키지 추가 없음. 인가 전 Production 쓰기 비노출은 기존 API 계약의 Development 한정 조건을 구현한 것으로 새로운 보안 정책 결정은 아니다.
- Spring 비교: Controller → Scoped Service → Repository 흐름은 MVC/Service와 대응한다. 예외는 ControllerAdvice 대신 IExceptionHandler, 현재 시각은 주입된 TimeProvider로 경계 테스트를 고정한다.

## ADR-007 — P2 SQL Server 구현과 검증 경계

- 날짜: 2026-10-23 / 상태: 채택(코드·정적 검증, DB 실증 전)
- 결정: EF Core SqlServer/Design/dotnet-ef 10.0.13(Microsoft, MIT), Scoped DbContext와 구체 Repository를 사용한다. 별도 DB 추상 프레임워크/SQLite/EF InMemory는 추가하지 않는다.
- EF SQL Server의 타입·제약·covering index 및 명시적 등록 트랜잭션을 구성했다. 자동 재시도는 사용하지 않는다. SQL 2601/2627에서만 rollback/dispose 후 해당 external ID를 재조회하여 fingerprint 비교. 일반 DB 오류를 409로 숨기지 않는다.
- P1 runtime Fake는 테스트 프로젝트로 이동했다. Production에 무인증 쓰기가 처리되지 않도록 환경 가드를 유지한다. JWT 자체는 P4 미완료다.
- 조회 모델 projection/no-tracking, bigint 집계, 매개변수 SQL의 long OFFSET과 `SqlQuery<T>`로 저장 프로시저를 호출한다. raw SQL의 값은 FormattableString으로 분리되며 사용자 텍스트를 SQL에 연결하지 않는다.
- Migration용 factory는 실제 연결 문자열 없이 모델/SQL 생성만 한다. 프로시저는 V001 embedded SQL에서 별도 dynamic batch로 생성한다. DB 적용·seed·DDL은 실행하지 않았다.
- 메모리와 SQL Server의 UUID 비교 순서 차이를 명시했다. 참가자는 Application canonicalization과 같은 .NET Guid 순서, DB 경기/랭킹의 tie break는 uniqueidentifier 순서다. 실DB에서 확인해야 한다.
- 보안 패키지 수정: EF Design 전이 의존성 `System.Security.Cryptography.Pkcs` 9.0.11에서 NU1902/NU1903. 수정 버전 10.0.13(Microsoft, MIT)을 Infrastructure에 직접 참조해 전체 그래프를 고정했다. 경고를 억제하지 않았다. 대안은 Design 제거/수정된 상위 의존성 대기이나 Migration 학습에 필요해 최소 override를 채택했다.
- 공식 권고: https://github.com/advisories/GHSA-2fmv-rv2c-59x7 및 https://github.com/advisories/GHSA-846f-f764-76mx (둘 다 10.0.13을 patched로 명시).
- 자기 리뷰 수정: 외부 ID ASCII 검증을 ToUpperInvariant 이전으로 이동해 long-s(ſ)의 ASCII 접힘을 차단. JSON outcome은 정확한 Win/Loss/Draw만 허용. default 시각을 거부하던 불필요한 하한을 제거하고 필수 여부는 JSON required로 처리한다. API 계약의 기존 조건을 구현한 수정이다.
- 승인 경계: AWS/DB 접근 없음. 실제 DB 통합·인프라 생성·보존/정리 정책은 사용자 승인 전 미완료.

## ADR-008 — 서울 리전 P3 견적·IaC 준비만 진행

- 날짜: 2026-10-23 / 상태: 사용자 선택 반영(준비 범위), 실제 실행 미승인
- 사용자 선택: “서울(ap-northeast-2) 기준 견적·IaC 초안 준비 (권장, 자원 생성 없음)”. 이를 AWS 계정 조회·유료 생성·IAM/Budget 알림 설정·DB 적용·배포 승인으로 확대하지 않는다.
- 결정된 범위: public 공식 가격/문서 조회, 로컬 Terraform 코드·fmt/validate·mock provider plan-only 테스트. 계정·프로파일·월 예산·인스턴스/버전·보존/삭제·수신자·실행은 미정이다.
- 견적: 실제 서울 RDS/EC2 공개 offer의 SKU/게시 시각/원본 SHA-256을 보존하고 Decimal로 재계산. 비교용 730시간 micro $39.52 / small $62.15, 60시간 small 예시 $8.55. 추가 CPU·백업·API·KMS·전송·세금 등 제외. 가격표 SKU를 주문 가능성으로 간주하지 않는다.
- 초안: 전용 VPC, public IPv4 EC2 relay(인바운드 0), private RDS/2개 AZ subnet group, 같은 AZ EC2/RDS, SG→SG 1433, TLS·암호화, IMDSv2, RDS 관리형 secret(EC2에 master 권한 없음), 보호/최종 snapshot·월 Budget 알림 제안.
- 미정값 처리: 비용/계정/사양/용량/백업 기간을 필수 입력으로 두고 example은 빈 값/null. 테스트의 가짜 계정/AMI/엔진/예산은 명시적인 offline fixture이며 실행 구성으로 사용하지 않는다. `configuration_review_reference`는 검토 기록 가드일 뿐 실행 승인이나 강제 보안 경계가 아니다.
- 도구: Terraform 1.17.0(HashiCorp BSL 1.1) 공식 macOS Arm64 archive와 SHA-256 확인 후 사용자 전용 경로 설치. AWS provider 6.70.0(MPL 2.0, HashiCorp 서명) 정확 버전/lock 고정. 외부 Terraform 모듈 없이 작은 자원 구성을 직접 작성했다. 대안은 수동 콘솔이나 CloudFormation이지만 기존 문서의 Terraform 재현 목표를 따른다.
- 리뷰 수정: SQL Server의 `rds.force_ssl`은 static이므로 즉시 적용 초안에서 `pending-reboot`로 수정했다. 모의 plan의 computed 값 unknown 실패는 plan 단계 mock 값으로 해결했고 실제 apply로 전환하지 않았다.
- 비용/정리 제안은 미승인: EC2 root EBS 종료 시 삭제, RDS 백업 보존·최종 snapshot, 자동 minor patch 비활성·패치 관리, 계정 범위 Budget 이메일 등은 실행 전 사용자 검토가 필요하다. 코드 상의 숫자를 승인 정책으로 보지 않는다.
- 상세: `docs/aws-cost-estimate.md`, `infra/terraform/README.md`, `docs/validation/p3-preparation/`.

## 2026-10-23 — 예산 우선, 사양 선택 보류

- 사용자 선택: “예산을 먼저 정하고 사양 선택 보류”. A/B 사양을 확정하거나 추천안을 승인된 설정으로 사용하지 않는다. 월 예산·프로파일·생성/실행 승인은 여전히 미정이다.
- 추가 비용 질문의 가정: 배포 후 월 약 30회 요청. 이는 가동 시간 30시간이나 자동 중지 요구가 아니다. 현재 RDS/EC2의 상시 시간 과금과 요청 횟수를 구분해 설명한다.
- 구성·스케줄·데이터 보존/삭제 정책 변경 없이 비용만 비교한다.

## ADR-009 — GitHub 공개 저장소와 작업 기록 보존

- 날짜: 2026-10-23 / 상태: 사용자 승인
- 사용자 요청: 프로젝트명 `ShooterRecordsLab`로 신규 GitHub 저장소 생성, 커밋·푸시. 공개 범위 질문에 Public을 선택했다.
- `/docs/init/`은 `.gitignore`에 포함하고 로컬 원본은 보존한다. 이 폴더 외 에이전트 작업 흔적을 담은 Markdown(AGENTS/의사결정/리뷰/상태/운영 문서 등)은 모두 포함한다.
- 소스·테스트·IaC·허용된 검증 로그도 포함한다. 자격증명·개인 설정·Terraform state/plan·빌드 산출물은 기존 무시 규칙에 따라 제외한다.
- GitHub 공개는 AWS 생성/DB 적용/배포 승인이 아니다. 현재 실제 AWS/실DB 검증은 미실행 상태로 공개한다.

## ADR-010 — 승인된 실환경 검증과 공개 증거의 범위

- 날짜: 2026-10-23 / 상태: 사용자 승인 및 구현 진행
- 사용자 승인: 서울 리전에서 실제 배포·테스트, RDS small + EC2 micro, 1회 비용 관리 기준 $3·최대 4시간. 이는 강제 청구 상한이 아니다. 이번 검증에서 새로 만든 데이터/자원만 대상으로 정리하며 기존 자원은 변경하지 않는다.
- 계정·자격증명·접속 주소·AWS 식별자·원본 state/plan/로그·토큰은 공개하지 않는다. 공개 증거는 검증 일자·소스 커밋·이미지 내용 digest·고정된 검증 항목·개수·수치로 제한한다. 원본은 Git 밖의 권한 제한 저장소에서 처리한다.
- 공개 문서는 실제 배포·검증 사실을 기술한다. 상시 운영·공개 서비스·장기 안정성을 검증하지 않고 그런 표현을 사용하지 않는다.
- 인증: Microsoft.AspNetCore.Authentication.JwtBearer 10.0.13(Microsoft, MIT). HS256 서명·issuer/audience/expiry 검증, 정확한 `scope`의 `matches.write` 토큰에만 POST 허용. 자체 토큰 발급 API는 만들지 않는다. 실환경 검증용 key/issuer는 폐쇄된 Staging 테스트 전용이며 ValidationOnly 설정은 Production에서 거부한다. 실제 공개 서비스에는 신뢰할 발급자 통합이 별도 필요하다.
- 컨테이너: 공식 SDK/runtime 패치와 manifest digest 고정, Linux x86_64, 비root/읽기 전용·권한 최소화, AWS 공개 CA bundle의 checksum 고정. Mac 시스템 신뢰 저장소를 변경하지 않고 컨테이너 안에서 RDS TLS를 검증한다.
- 단기 검증의 Terraform 모드는 사용자 선택에 따라 데이터/백업/스냅샷/이미지/시크릿의 보존을 하지 않는다. 일반 모드 기본값은 보호/보존을 유지한다. 예산 이메일 수신자가 미지정인 단기 검증에서는 이메일 Budget 생성 대신 명시적인 로컬 시간·비용 관리 기준을 사용한다.
- ECR와 애플리케이션 전용 secret 접근만 EC2 역할에 추가한다. DB master secret 권한을 EC2에 주지 않으며 secret 값은 Terraform state에 넣지 않는다.
- 생성 전에 JWT/컨테이너/모의 IaC 검증을 마쳐 유료 자원의 대기 시간을 줄인다. P3 실제 SQL 검증과 P5 배포 검증 결과는 실제 실행한 뒤 기록한다.

## ADR-011 — 실제 SQL Server 검증으로 확인한 CHECK·실행 계획 차이

- 날짜: 2026-10-23 / 상태: 실DB 재현 후 수정·재검증 진행
- 실제 SQL Server에서 정상적인 하이픈 포함 외부 ID의 삽입이 CHECK에 거부되었다. 비DB 테스트 통과를 SQL 적합성으로 간주하지 않고, 상수 샘플 `TEST-ABC123-RACE`로 직접 LIKE 동작을 재현했다.
- 기존 `[^A-Z0-9_-]` 패턴은 정상 ID를 거부했고, 하이픈을 집합 앞에 둔 `[^-A-Z0-9_]`는 동일 입력을 허용했다. 이미 공개한 InitialCreate를 바꾸지 않고 후속 FixExternalMatchIdCharacterSet Migration으로 수정한다.
- 기존 동시성/원자성 검증을 유지하고 실제 DB에 대해 정상 문자·하이픈/밑줄 단독·소문자·공백·Unicode·금지 문자를 확인하는 11개 사례를 추가했다.
- 실제 STATISTICS XML 결과는 nvarchar 문자열로 반환되는 것을 확인했다. 검증 도구는 XML 타입 및 ShowPlanXML 문자열을 모두 처리하고 실제 runtime counters가 존재할 때만 실행 계획 검증 성공으로 처리한다.
- 재현 입력·코드만 공개하고 실제 연결 정보·AWS 식별자·원본 DB/계정 로그는 공개하지 않는다. 실제 최종 테스트 결과는 실행 후 별도로 기록한다.

## 2026-10-23 — 프로젝트 설명의 기업명 제거

- 사용자 요청에 따라 README와 개발계획·의사결정·초기화 안내의 기업명을 제거하고, 개인 학습 목표와 가상 데이터 사용 범위가 드러나도록 문장을 정리했다.
- 이번 요청의 “프로젝트 내 표현 전부 제거”를 원본 보존 원칙의 명시적 예외로 적용해 `docs/init/`의 해당 문구도 수정했다. 그 밖의 초기화 지침과 Git 제외 설정은 유지한다.
- 소스·API 계약·인프라 설정·검증 수치는 변경하지 않으며, 커밋·푸시는 사용자 검토 후 별도 요청에 따른다.

## ADR-012 — Spring Boot 전환 설명서와 README 상단 안내

- 날짜: 2026-10-23 / 상태: 사용자 승인에 따라 채택
- 사용자 요청: 검토한 방향으로 별도 문서를 작성하고 README 상단에 요약·링크를 추가한다. 실제 코드·테스트·검증 기록을 근거로 작성하고 변경 기록도 갱신한다.
- 결정: [spring-boot-to-aspnet-core.md](spring-boot-to-aspnet-core.md)에 기본 개념, 공통 설계 원리와 차이, 경기 등록 흐름, 실제 DB 문제 해결 및 검증 범위를 설명한다. README에는 소개 직후 짧은 요약과 대표 링크를 두어 첫 화면에서 학습 맥락과 구현 근거로 이동할 수 있게 한다.
- 근거·대안: 비교 설명 전체를 README에 추가하면 기존 핵심 구현·검증 설명과 중복된다. 링크만 추가하면 문서를 열기 전 목적을 알기 어려우므로 요약과 링크를 함께 제공한다. 기존 dotnet-guidelines는 구현 지침, 새 문서는 이해를 돕는 설명서, aws-validation은 실행 결과의 근거로 역할을 나눈다.
- 정확성 기준: Spring의 기본 singleton과 현재 Scoped 등록, 비동기 실행과 스레드 생성, 단일 SaveChanges의 원자성과 현재 Repository의 명시적 트랜잭션, LINQ와 매개변수 SQL 사용을 구분한다. 취소 테스트는 서비스 직접 호출 범위이며 HTTP 연결 중단부터 DB 명령 취소까지의 검증으로 확대하지 않는다.
- 증거 해석: 과거 검증 소스 `71c8a47`의 전체 105개에 실DB 12개가 포함되고 EC2 HTTP 18개는 별도라는 구분을 유지한다. SQL Server CHECK 수정은 실제 DB 검증의 필요성을 보여주는 사례로 설명한다. Spring 비교 구현의 동등성·상용 운영 경력·독립 감사의 근거로 사용하지 않는다.
- 한계·유지보수: 설명은 현재 코드와 연결되므로 해당 구현이나 테스트가 바뀌면 함께 갱신한다. 코딩 에이전트 활용과 동일 세션 검토의 독립성 한계를 명시한다. 이번 변경은 문서에 한정하며 원본 docs/init, API·DB·배포 설정과 기존 실환경 보고서는 수정하지 않는다.
- 변경 파일: README, 새 설명서, 이 의사결정 기록, project-status. 검증 명령과 실제 결과는 [진행 기록](project-status.md)에 남긴다.
