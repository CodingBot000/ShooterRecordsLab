# AGENTS.md — ShooterRecordsLab 프로젝트 상시 지침

## 프로젝트 목적과 우선순위

이 프로젝트는 Java/Spring Boot 경험자가 현대적인 **.NET 10 ASP.NET Core + SQL Server**의 실무 패턴을 학습하기 위한 소규모 기록실 REST API다. 가상의 게임 데이터만 사용한다. 중요도 순서는 **정합성/보안 > 테스트 가능성 > 명확성 > 학습 가치 > 구현 속도**다.

## 시작할 때 반드시 읽을 문서

1. `docs/development-plan.md` — 현재 단계/완료 조건
2. `docs/architecture.md` 및 `docs/api-contract.md` — 시스템 경계 및 API 계약
3. `docs/dotnet-guidelines.md` — 구현 패턴
4. 관련 작업 시 `docs/testing-guidelines.md`, `docs/security-checklist.md`, `docs/aws-deployment.md`
5. `docs/project-status.md` — 완료 증거와 남은 작업
6. 변경 이유는 `docs/decisions.md`에 추가

`docs/init/`는 원본 초기 문서 보관소이며 **수정하거나 삭제하지 않는다**. 수정은 복사된 운영 문서에만 한다. 원본 템플릿과 충돌 시 현재 사용자 지시와 운영 문서가 우선하고, 충돌 내역을 기록한다.

## 구현 규칙

- 기술: .NET 10 LTS / ASP.NET Core Controller API / EF Core 10 / SQL Server 2022, AWS RDS SQL Server Express. API 런타임과 데이터베이스를 혼동하지 않는다.
- `src/ShooterRecordsLab.Api`, `.Application`, `.Domain`, `.Infrastructure`의 경계를 유지한다. 구체적인 의존 관계는 `docs/architecture.md`를 따른다.
- HTTP/API DTO와 EF Entity/Domain 모델을 그대로 공유하지 않는다. Controller는 HTTP 계층만, Use Case(Service)는 규칙과 트랜잭션 조정을 책임진다.
- DI는 생성자 주입. `DbContext` 기본 Scoped. Singleton이 Scoped 서비스를 캡처하지 않게 한다.
- 모든 I/O는 실제 async API와 `CancellationToken`을 사용한다. `.Result`/`.Wait()`/무의미한 `Task.Run` 금지.
- EF Core 조회 기본값 `AsNoTracking()`, 필요한 컬럼만 projection, 페이지네이션 및 안정적 정렬, N+1 방지. DB 쿼리의 트랜잭션·인덱스 비용 검토.
- 명시적 에러 계약(`ProblemDetails`), 입력검증, 구조화된 `ILogger` 로그 사용. 내부 예외나 비밀번호를 응답/로그에 포함하지 않는다.
- 기본 인증은 쓰기 API에 JWT Bearer 검증. 개발용 토큰/키를 프로덕션에서 사용하지 않는다.
- DB 스키마는 EF Core Migration으로 버전 관리하고 승인 없이 운영 DB에 자동 적용하지 않는다. Stored Procedure는 검증된 스크립트/Migration으로 관리한다.
- 사용자 제공 정보가 없으면 임의의 AWS 리소스 ID, 시크릿, 비용 허용, DNS를 만들거나 가정하지 않는다.
- 필요 없는 Generic Repository, 불필요한 CQRS/Mediator/Microservices, 과도한 추상화와 새 패키지를 피한다.
- C# nullable reference types 활성화, analyzer/format 적용. 외부 라이브러리 추가 시 이유·유지보수/라이선스·대안을 간단히 기록한다.

## 작업 사이클

1. 현 단계와 관련 코드/문서를 읽고 변경 영향 및 실패 케이스를 확인한다.
2. 기능 구현 전/동시에 실패를 입증하는 테스트를 작성한다(가능한 경우).
3. 한 번에 작은 변경 단위를 완료한다. 실행한 명령과 오류/수정 내역을 남긴다.
4. 최소 검증: `dotnet restore`, `dotnet build -warnaserror`, DB 없는 테스트 `dotnet test --filter 'Category!=Database'`, `dotnet format --verify-no-changes` (프로젝트 구성 후).
5. DB 연동 관련 변경은 전용 테스트 DB에서 `dotnet test --filter 'Category=Database'` 수행. DB 연결이 없으면 **미검증**으로 명시하고 성공으로 처리하지 않는다.
6. `docs/review-guidelines.md`에 따라 구현자와 분리된 시각으로 위험/누락을 재검토한다. 도구가 없으면 동일 Codex 세션에서 별도 리뷰 패스를 수행하고 독립성 한계를 표기한다.
7. `docs/project-status.md`에 변경 파일, 명령, 성공/실패 증거, 다음 작업 기록. 계약/설계 변경은 문서 갱신.

## 승인 없이 금지하는 작업 (중요)

- AWS EC2/RDS/ECR/IAM/VPC/Secrets 등 **유료 자원 생성·변경·삭제**, Terraform `apply`/`destroy`, 실제 배포·퍼블릭 노출.
- 데이터베이스 DROP/파괴적 Migration/운영 데이터 리셋, 테스트 명목의 운영 DB 접근.
- `.env`, 개인 토큰, AWS 키, DB 비밀번호 등을 커밋하거나 로그/출력에 노출.
- 방화벽/보안 그룹의 1433을 인터넷(`0.0.0.0/0`, `::/0`)에 개방.
- 검증 실패를 무시하거나 테스트를 삭제/skip하여 통과로 위장.
- 기존 사용자 파일 덮어쓰기, 강제 push, 파괴적인 Git 명령.

**승인 없는 작업으로 허용:** 문서/코드 작성, 로컬 빌드·테스트, IaC 작성·정적 검토, 예상 AWS 비용 표/명령 준비, 읽기 전용 정보 조회(자격 정보가 이미 허용된 경우). 외부 접속/요금이 발생할 수 있는 테스트는 먼저 범위를 확인한다.

## 출력 스타일 및 완료 정의

- 응답은 한국어, 짧고 정확하게. 구현의 장점뿐 아니라 한계/실패/미검증 항목을 함께 보고한다.
- 매 단계 완료 보고에는 **변경 요약 / 실행한 검증 명령과 결과 / 남은 위험 / 다음 단계**를 포함한다.
- 성공하지 않은 빌드·테스트·AWS 작업은 완료로 표시하지 않는다.
- 가능한 경우 Java/Spring 대응 개념을 코드 주석 대신 문서에 짧게 기록한다.
