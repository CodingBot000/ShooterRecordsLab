# Spring Boot 개발자를 위한 ASP.NET Core 이해와 구현 근거

이 프로젝트는 Java·Spring Boot에서 익힌 HTTP API, 의존성 주입, 트랜잭션, 동시성 제어와 테스트 원리를 ASP.NET Core·EF Core·SQL Server에 적용한 개인 학습 프로젝트입니다. 프레임워크별 구현과 검증 도구 작성에는 코딩 에이전트를 활용했습니다. 이 문서는 재사용한 설계 원리와 별도로 확인한 차이를 실제 코드·테스트·검증 기록에 연결합니다.

[프로젝트 README](../README.md) · [기본 개념](#2-spring-개발자를-위한-기본-지도) · [공통점과 차이](#3-공통-원리와-실제-구현의-차이) · [경기 등록 흐름](#4-경기-등록-한-요청으로-보는-전체-흐름) · [문제 해결과 검증 범위](#5-실제-db에서-발견한-문제와-검증-범위)

## 1. 목적과 핵심 결론

재사용할 수 있는 것은 책임 분리, 계약, 원자성, 인증·인가 구분 같은 설계 원리입니다. 객체 수명, 비동기 실행, ORM의 저장 시점과 SQL 번역은 사용하는 프레임워크와 DB에 맞춰 다시 확인해야 합니다.

| 확인할 역량 | 이 프로젝트에서 읽을 근거 |
|---|---|
| HTTP 계약과 업무 규칙 분리 | [MatchesController](../src/ShooterRecordsLab.Api/Controllers/MatchesController.cs) → [MatchService](../src/ShooterRecordsLab.Application/Matches/MatchService.cs), [계층 참조 테스트](../tests/ShooterRecordsLab.UnitTests/ArchitectureTests.cs) |
| 재시도·동시 요청의 정합성 | [SQL 저장소의 등록·충돌 처리](../src/ShooterRecordsLab.Infrastructure/Repositories/SqlRecordsRepository.cs), [실DB 동시성·원자성 테스트](../tests/ShooterRecordsLab.IntegrationTests/SqlServerTests.cs) |
| 환경 차이를 발견하고 수정하는 과정 | [후속 Migration](../src/ShooterRecordsLab.Infrastructure/Persistence/Migrations/20261022162547_FixExternalMatchIdCharacterSet.cs), [문자 경계 테스트](../tests/ShooterRecordsLab.IntegrationTests/SqlServerExternalIdTests.cs), [실환경 보고서](aws-validation.md) |

Spring 비교 구현은 이 저장소에 없습니다. 아래 대응은 개념과 현재 구현을 설명하며, 두 프레임워크로 만든 API의 동등성을 검증했다는 뜻은 아닙니다. 개인 프로젝트의 학습·검증 경험과 상용 .NET 서비스 운영 경력도 구분합니다.

## 2. Spring 개발자를 위한 기본 지도

### 기술과 프로젝트를 구분하기

| 익숙한 Java 생태계의 관점 | 이 저장소에서의 대응과 읽을 위치 |
|---|---|
| Java 언어와 JDK·JVM | C#은 언어, .NET은 런타임·라이브러리·SDK를 제공하는 플랫폼입니다. [global.json](../global.json)이 SDK 10.0.402와 패치 선택 범위를 고정합니다. |
| Spring Boot로 시작한 Spring MVC API | ASP.NET Core는 웹 프레임워크입니다. 여기서는 Controllers를 사용하고 [Program.cs](../src/ShooterRecordsLab.Api/Program.cs)에서 서비스와 HTTP 파이프라인을 구성합니다. |
| JPA·Hibernate와 JDBC 드라이버 | EF Core가 ORM이고 SQL Server provider가 DB별 연동을 담당합니다. SQL Server 2022는 별도의 DB 엔진입니다. .NET 버전과 DB 버전은 서로 다른 대상입니다. |
| Maven·Gradle의 모듈·의존성 구성 | `.csproj`는 각 프로젝트의 대상 프레임워크·패키지·프로젝트 참조를 선언합니다. [.slnx](../ShooterRecordsLab.slnx)는 프로젝트들을 묶고, NuGet이 패키지를 복원합니다. [API 프로젝트](../src/ShooterRecordsLab.Api/ShooterRecordsLab.Api.csproj), `packages.lock.json`, [공통 빌드 설정](../Directory.Build.props)을 함께 읽으면 됩니다. |
| `application.yml`, profile, 외부 설정 | `appsettings.json`, 환경별 JSON, 환경변수, 개발용 user-secrets 등을 사용합니다. 현재 코드는 `WebApplication.CreateBuilder`와 `IOptions<T>` 바인딩을 사용합니다. |

기본 앱 구성에서는 환경변수가 JSON 설정을 덮어쓸 수 있고, 중첩 키에는 `__`를 사용할 수 있습니다. 예를 들어 `ConnectionStrings__ShooterRecords`는 현재 코드의 `GetConnectionString("ShooterRecords")`와 연결됩니다. 실제 값은 문서·저장소에 넣지 않습니다. 개발용 [launchSettings.json](../src/ShooterRecordsLab.Api/Properties/launchSettings.json)의 실행 프로필과 배포 설정도 구분합니다. 설정 소스와 우선순위는 [ASP.NET Core 공식 문서](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0)를 참고합니다.

빌드·비DB 테스트는 .NET SDK만으로 실행할 수 있습니다. [README의 로컬 절차](../README.md#로컬-빌드실행)와 [verify.sh](../infra/scripts/verify.sh)는 restore → build → test → format 순서를 제공합니다. 현재 프로젝트는 Mac에서 개발하고 Linux 컨테이너에서 실행한 현대적 .NET 프로젝트입니다.

### 코드를 읽을 때 필요한 C# 표현

| 현재 코드의 표현 | 읽는 방법 |
|---|---|
| `[ApiController]`, `[HttpPost]`, `[Authorize]` | Java annotation과 비슷하게 메타데이터를 표현하는 attribute입니다. 각각의 런타임 동작은 프레임워크가 정의합니다. |
| `class MatchService(IRecordsRepository repository, TimeProvider clock)` | 기본 생성자(primary constructor)의 매개변수입니다. 이 프로젝트에서는 DI가 의존성을 전달하며, 클래스 선언 자체가 서비스 등록을 수행하지는 않습니다. |
| `record RegisterMatchInput(...)` | 데이터 전달 타입을 간결하게 선언합니다. 웹 요청 record, Application 입력 record, Domain Entity는 별도 타입입니다. |
| `string?`, `p!`, `[JsonRequired]` | `?`는 참조 타입의 null 허용 표시, `!`는 컴파일러 경고 억제입니다. 둘 다 런타임 입력 검증을 대신하지 않습니다. 현재 요청 DTO는 `[JsonRequired]`로 JSON 필드 존재를 요구하고 별도 규칙으로 값을 검사합니다. |
| `using`, `await using` | 범위를 벗어날 때 `IDisposable`·`IAsyncDisposable` 자원을 정리합니다. 등록 코드의 트랜잭션 정리 시점을 이해하는 데 필요합니다. |
| `Where`, `Select`, `OrderBy` | LINQ입니다. 메모리 컬렉션을 처리하는지, DB 쿼리를 구성하는지 먼저 구분해야 합니다. |

예를 들어 `MatchService`의 참가자 정렬은 메모리에서 수행하고, 저장소의 `db.Players.Where(...).Select(...)`는 EF Core가 SQL로 번역합니다. Java Stream과 문법 모양이 비슷하더라도 실행 위치가 같다고 가정하면 안 됩니다. DB 쿼리의 실행은 `ToListAsync`·`SingleOrDefaultAsync` 같은 호출에서 확인합니다. [EF Core 쿼리 공식 문서](https://learn.microsoft.com/en-us/ef/core/querying/)

## 3. 공통 원리와 실제 구현의 차이

### HTTP 계약과 계층별 책임

공통 원리는 HTTP 표현과 업무 규칙을 분리하는 것입니다. Spring의 Controller·Service 구분을 재사용하되, 이 프로젝트에서는 API → Application → Domain의 흐름과 Infrastructure의 DB 구현을 분리했습니다. 구체적인 의존 방향은 [아키텍처](architecture.md)에 있습니다.

`MatchesController.Register`는 웹 요청을 `RegisterMatchInput`으로 변환하고, 결과를 `201 Created` 또는 `200 OK`로 매핑합니다. `MatchService.RegisterAsync`는 ASCII ID·UTC 시각·참가자·승패 조합을 검증하고 요청 내용을 정규화해 fingerprint를 계산합니다. DB 참가자 존재 확인과 실제 저장은 `SqlRecordsRepository`가 담당합니다. Domain Entity를 HTTP 응답으로 직접 직렬화하지 않습니다.

**코드:** [Controller](../src/ShooterRecordsLab.Api/Controllers/MatchesController.cs), [웹 DTO](../src/ShooterRecordsLab.Api/Contracts/ApiModels.cs), [Application 모델](../src/ShooterRecordsLab.Application/Matches/MatchModels.cs), [Service](../src/ShooterRecordsLab.Application/Matches/MatchService.cs).

**테스트 근거:** [RecordsApiTests](../tests/ShooterRecordsLab.IntegrationTests/RecordsApiTests.cs)의 `Registration_location_replay_conflict_and_statistics_follow_contract`가 Location·재응답·충돌을, `Malformed_missing_and_null_fields_are_400`이 입력 오류를 확인합니다. [ArchitectureTests](../tests/ShooterRecordsLab.UnitTests/ArchitectureTests.cs)는 프로젝트 참조와 Domain/Application의 패키지·프레임워크 참조 부재를 확인합니다. 이 구조 테스트가 모든 메서드의 책임 분리까지 자동 증명하지는 않습니다.

### 생성자 주입과 DI 수명

공통 원리는 필요한 의존성을 생성자로 받고 생성·교체를 컨테이너에 맡기는 것입니다. 하지만 Spring의 `@Service`와 .NET의 `AddScoped`는 같은 의미가 아닙니다. Spring bean의 기본 수명은 컨테이너 내 singleton이고, ASP.NET Core에서는 등록 시 Singleton·Scoped·Transient를 선택합니다. [Spring bean scope](https://docs.spring.io/spring-framework/reference/core/beans/factory-scopes.html)

현재 [Program.cs](../src/ShooterRecordsLab.Api/Program.cs)는 Service·Repository를 `AddScoped`로, DbContext를 기본 Scoped인 `AddDbContext`로 등록합니다. 일반적인 HTTP 요청에서 같은 scope의 DbContext를 사용하고 요청 종료 시 정리하는 구성입니다. `TimeProvider.System`은 Singleton으로 등록해 시간 의존성을 분리합니다. Spring의 singleton Service처럼 DbContext를 장기간 공유하면 안 되며, 한 DbContext에서 동시에 여러 DB 작업을 실행해서도 안 됩니다. [DbContext 수명·스레드 안전성](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)

**테스트 근거:** [RecordsApiTests](../tests/ShooterRecordsLab.IntegrationTests/RecordsApiTests.cs)는 DI의 저장소와 `TimeProvider`를 교체해 HTTP 계약과 미래 5분 경계를 검사합니다. [SqlServerTests](../tests/ShooterRecordsLab.IntegrationTests/SqlServerTests.cs)는 동시 등록 작업마다 별도 DbContext를 생성합니다. 이 테스트는 잘못된 Singleton 등록의 모든 경우를 탐지하는 전용 수명 테스트는 아닙니다.

### 비동기 I/O와 취소

공통 원리는 DB·네트워크 대기와 CPU 계산을 구분하고, 더 이상 필요하지 않은 작업에 취소를 전달하는 것입니다. C#의 `Task<T>`는 비동기 작업 결과를 나타내고, `await`는 완료를 기다리는 동안 호출자에게 제어를 돌려줄 수 있습니다. `async`를 붙였다고 새 스레드가 생성되거나, 동기 I/O가 비동기로 바뀌는 것은 아닙니다. `CompletableFuture`·Spring `@Async`와 실행 모델을 동일시하지 않습니다. [C# 비동기 공식 문서](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios)

현재 Controller → Service → Repository는 `CancellationToken`을 전달하고, 저장소는 `SingleOrDefaultAsync`·`SaveChangesAsync`·`CommitAsync` 등에 전달합니다. 해시 계산은 동기 CPU 작업으로 수행하며 `Task.Run`으로 감싸지 않습니다. 같은 DbContext의 작업은 순서대로 await합니다.

**코드·테스트 근거:** [MatchesController](../src/ShooterRecordsLab.Api/Controllers/MatchesController.cs), [MatchService](../src/ShooterRecordsLab.Application/Matches/MatchService.cs), [SqlRecordsRepository](../src/ShooterRecordsLab.Infrastructure/Repositories/SqlRecordsRepository.cs). [RecordsApiTests](../tests/ShooterRecordsLab.IntegrationTests/RecordsApiTests.cs)의 `Cancellation_is_propagated_to_application`은 취소된 토큰으로 `PlayerService.GetAsync`를 직접 호출해 `OperationCanceledException`을 확인합니다. **HTTP 연결 중단부터 실제 DB 명령 중단까지 검증한 테스트는 아닙니다.**

### 영속성·원자성·멱등성과 동시성

공통 원리는 여러 행의 부분 저장을 막고, 요청 재전송에도 같은 업무 결과를 유지하는 것입니다. JPA 영속성 컨텍스트와 EF Core DbContext 모두 변경을 추적하지만, JPA의 변경 감지·flush·트랜잭션 완료 방식을 그대로 대입하지 않습니다. 현재 EF 구현은 `db.Matches.Add(match)`로 그래프를 추적한 뒤 `SaveChangesAsync`에서 DB에 씁니다. [JPA 트랜잭션 설명](https://docs.spring.io/spring-data/jpa/reference/jpa/transactions.html), [EF Core 변경 저장 과정](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)

Spring의 `@Transactional`은 기본 proxy 모드에서 메서드 호출에 트랜잭션 동작을 적용합니다. 현재 C# Service에는 이에 해당하는 선언적 트랜잭션 attribute가 없고, **실제 트랜잭션 구현은 Repository의 `RegisterMatchAsync`에 있습니다.** Application은 입력 규칙과 정규화를 담당하며, Repository가 기존 요청 비교·DB 작업·SQL 충돌 후 비교를 수행합니다. [Spring 선언적 트랜잭션](https://docs.spring.io/spring-framework/reference/data-access/transaction/declarative/annotations.html)

현재 저장소는 참가자 존재 조회와 그래프 저장·commit 범위를 `BeginTransactionAsync`로 명시합니다. 단, 관계형 DB에서 **한 번의 `SaveChangesAsync` 자체도 기본적으로 원자적**입니다. 이 프로젝트의 명시적 트랜잭션이 있어야만 경기와 결과 행의 부분 저장을 막을 수 있다고 설명하면 부정확합니다. 존재 조회만으로 동시 삭제를 막는다고 가정하지 않으며 최종 FK·UNIQUE 제약도 사용합니다. [EF Core 기본·명시적 트랜잭션](https://learn.microsoft.com/en-us/ef/core/saving/transactions)

멱등성의 핵심은 트랜잭션만이 아니라 **정규화한 외부 ID + fingerprint 비교 + DB UNIQUE 제약**의 조합입니다. 사전 조회만으로 경쟁 요청을 막을 수 없습니다. 현재 코드는 SQL Server 오류 2601/2627인 `DbUpdateException`만 잡고, 트랜잭션 정리 후 추적 상태를 비우고 기존 외부 ID를 재조회합니다. 기존 결과가 있으면 fingerprint를 비교하고, 없으면 예외를 다시 던집니다. 일반 DB 장애를 일괄 `409`로 바꾸지 않습니다. 이는 중복 생성 경쟁 처리이며, 모든 업데이트 충돌에 대한 낙관적 동시성 구현을 뜻하지는 않습니다.

**코드·테스트 근거:** [SQL 저장소](../src/ShooterRecordsLab.Infrastructure/Repositories/SqlRecordsRepository.cs), [고유 키·제약 설정](../src/ShooterRecordsLab.Infrastructure/Persistence/Configurations/MatchConfiguration.cs). [SqlServerTests](../tests/ShooterRecordsLab.IntegrationTests/SqlServerTests.cs)의 `SqlServer_concurrency_atomicity_constraints_and_rankings`는 동시 등록 10건 중 신규 생성 1건·동일 ID, FK 오류 시 그래프의 부분 저장 부재, UNIQUE·CHECK와 저장 프로시저를 검사합니다. HTTP 상태 코드는 Fake를 사용하는 계약 테스트와 [별도 EC2 HTTP 검증 기록](aws-validation.md)으로 구분해 확인합니다.

### 요청 파이프라인·오류·인증과 인가

공통 원리는 공통 관심사를 요청 경계에서 처리하고 인증과 권한 판단을 구분하는 것입니다. Spring의 Filter·Interceptor·ControllerAdvice·Security 설정을 하나의 Middleware와 일대일로 대응시키지는 않습니다.

현재 [Program.cs](../src/ShooterRecordsLab.Api/Program.cs)의 명시적 순서는 다음과 같습니다. 프레임워크의 라우팅 등 내부 단계를 모두 나열한 그림은 아닙니다.

```text
요청 로그 래퍼 → 예외 처리 → 상태 코드 페이지 → 인증 → 인가 → Controller
               ← 처리된 응답을 거슬러 반환, 로그 래퍼 finally에서 최종 상태 기록
```

`RecordsExceptionHandler`는 알려진 업무 예외를 계약에 맞는 상태 코드로 바꾸고 알 수 없는 실패는 내부정보 없는 500으로 처리합니다. JWT는 서명·issuer·audience·기간을 확인하고, `MatchesWrite` 정책이 정확한 `matches.write` scope를 요구합니다. 인증 실패는 401, 인증되었지만 권한이 부족하면 403입니다. 유효한 권한이 있어도 잘못된 본문은 이후 입력 검증에서 400이 됩니다.

**코드:** [예외 처리](../src/ShooterRecordsLab.Api/Middleware/RecordsExceptionHandler.cs), [JWT·정책 설정](../src/ShooterRecordsLab.Api/Authentication/JwtAuthentication.cs), [쓰기 Action](../src/ShooterRecordsLab.Api/Controllers/MatchesController.cs).

**테스트 근거:** [AuthenticationTests](../tests/ShooterRecordsLab.IntegrationTests/AuthenticationTests.cs)의 `Bearer_validation_and_scope_are_enforced_before_model_validation`은 401·403이 본문 검증보다 먼저 적용되는지 확인합니다. [RecordsApiTests](../tests/ShooterRecordsLab.IntegrationTests/RecordsApiTests.cs)의 `Unknown_failure_returns_safe_500`은 내부 문자열이 응답·캡처 로그에 없고 최종 `status 500`이 기록되는지 검사합니다.

### 조회 비용과 SQL Server 연동

공통 원리는 필요한 데이터만 읽고, 집계·정렬·페이지 처리를 DB와 애플리케이션 중 적절한 위치에서 수행하는 것입니다. EF Core의 LINQ는 provider가 번역할 수 있는 식을 사용해야 하며, 메모리 Fake 통과만으로 SQL 번역과 비용을 확인할 수 없습니다.

현재 [SqlRecordsRepository](../src/ShooterRecordsLab.Infrastructure/Repositories/SqlRecordsRepository.cs)는 읽기에 `AsNoTracking`·projection을 사용하고 통계는 DB에서 `long`으로 집계합니다. `AsNoTracking`은 EF의 Entity 추적을 생략하는 선택이며 DB에 읽기 전용 트랜잭션을 설정하는 기능은 아닙니다. 경기 목록의 count는 LINQ로, 페이지 행은 매개변수화한 `SqlQuery`로 읽습니다. 후자는 `int` 범위를 넘는 offset을 `long`으로 처리하며 `PlayedAtUtc DESC, Id DESC`로 동률 순서를 고정합니다. 정렬은 동일한 데이터에서 결정적 순서를 제공하지만, 동시 삽입·삭제 중 offset 페이지의 스냅샷 일관성을 보장하지는 않습니다.

랭킹은 [SqlRankingRepository](../src/ShooterRecordsLab.Infrastructure/Repositories/SqlRankingRepository.cs)에서 매개변수 `@Top`으로 [T-SQL 저장 프로시저](../src/ShooterRecordsLab.Infrastructure/Persistence/Sql/V001_GetTopPlayerRankings.sql)를 호출합니다. 이는 ORM을 사용하더라도 SQL·인덱스·실행 계획을 함께 읽어야 하는 지점입니다.

**테스트·측정 근거:** [RecordsApiTests](../tests/ShooterRecordsLab.IntegrationTests/RecordsApiTests.cs)는 Fake로 페이지·통계의 HTTP 계약을, [SqlServerTests](../tests/ShooterRecordsLab.IntegrationTests/SqlServerTests.cs)는 실제 SQL 결과·랭킹 순서를 확인합니다. 후자는 랭킹 동률 정렬에 `SqlGuid`를 사용해 .NET `Guid`의 메모리 정렬과 구분합니다. 가상 경기 5,000개·결과 10,000개에서 측정한 실행 계획과 logical reads는 [실환경 보고서](aws-validation.md#대표-sql-실행-계획측정)에 있습니다. 단일 데이터 분포의 측정이며 대규모 처리량이나 부하 한계를 입증한 결과는 아닙니다.

## 4. 경기 등록 한 요청으로 보는 전체 흐름

`POST /api/matches`를 읽으면 위 개념이 한 경로로 연결됩니다. 요청 예제와 정확한 입력 범위는 [API 계약](api-contract.md#5-post-apimatches)을 따릅니다.

| 단계 | 실제 처리와 책임 |
|---|---|
| 1. 인증·인가 | JWT를 검증하고 `matches.write` 정책을 검사합니다. 실패 시 401/403으로 종료합니다. |
| 2. HTTP 입력 | `[ApiController]`, JSON converter와 DTO 필수 필드 설정으로 바인딩·형식 오류를 처리합니다. Controller가 Application 입력 모델로 변환합니다. |
| 3. 업무 규칙 | Service가 ID를 trim → ASCII 검사 → 대문자 변환하고 UTC·미래 시각·참가자·승패 조합을 검증합니다. 주입된 `TimeProvider`로 현재 시각을 얻습니다. |
| 4. 같은 요청의 기준 | 참가자를 Guid 순으로 정렬하고 정규화 ID·UTC ticks·참가자 수치·결과로 SHA-256 fingerprint를 계산합니다. 입력 배열 순서가 달라도 같은 내용으로 판단합니다. |
| 5. 조회·저장 | Repository가 기존 외부 ID를 조회합니다. 없으면 명시적 트랜잭션 안에서 참가자 존재를 확인하고 경기·결과 그래프를 한 번의 `SaveChangesAsync`로 저장·commit합니다. |
| 6. 경쟁 처리 | UNIQUE 충돌이면 실패한 트랜잭션이 정리된 뒤 기존 요청을 재조회합니다. 같은 fingerprint는 기존 결과, 다른 fingerprint는 `MATCH_ID_CONFLICT`입니다. |
| 7. HTTP 응답 | Controller는 신규 생성에 201 + Location, 같은 내용 재응답에 200을 반환합니다. 충돌 예외는 중앙 핸들러가 409 ProblemDetails로 매핑합니다. 요청 로그 래퍼가 최종 상태를 기록합니다. |

빠르게 코드를 따라가려면 [MatchesController.Register](../src/ShooterRecordsLab.Api/Controllers/MatchesController.cs) → [MatchService.RegisterAsync](../src/ShooterRecordsLab.Application/Matches/MatchService.cs) → [SqlRecordsRepository.RegisterMatchAsync](../src/ShooterRecordsLab.Infrastructure/Repositories/SqlRecordsRepository.cs) 순서로 읽으면 됩니다. 요청 내용 비교는 Service가 만든 fingerprint를 Repository가 비교하는 협력으로 구현되어 있습니다.

## 5. 실제 DB에서 발견한 문제와 검증 범위

### 정상 하이픈 ID를 거부한 CHECK 제약

초기 구현에서는 앱 검증을 통과한 정상 ID가 실제 SQL Server CHECK 제약에 거부됐습니다. [ADR-011](decisions.md#adr-011--실제-sql-server-검증으로-확인한-check실행-계획-차이)에 기록된 상수 입력은 `TEST-ABC123-RACE`입니다.

1. 실제 SQL Server에서 `LIKE` 문자 집합의 동작을 재현했습니다.
2. 초기 `[^A-Z0-9_-]` 패턴을 하이픈이 집합 앞에 오는 `[^-A-Z0-9_]`로 바꿨습니다. 이는 T-SQL LIKE 패턴이며 C# 정규식과 구분해야 합니다.
3. 공개된 InitialCreate를 다시 쓰지 않고 [FixExternalMatchIdCharacterSet Migration](../src/ShooterRecordsLab.Infrastructure/Persistence/Migrations/20261022162547_FixExternalMatchIdCharacterSet.cs)을 추가했습니다.
4. [SqlServerExternalIdTests](../tests/ShooterRecordsLab.IntegrationTests/SqlServerExternalIdTests.cs)에 정상·금지 문자 11개 사례를 추가하고 기존 동시성·원자성 검증을 유지했습니다. 최종 성공 결과는 [실환경 보고서](aws-validation.md#실제-db-검증에서-발견하고-수정한-문제)에 기록되어 있습니다.

이 사례는 Spring과 .NET의 우열이나 구현 차이를 입증하는 사례가 아닙니다. **앱의 입력 규칙, ORM 매핑과 실제 DB 제약을 함께 검증해야 한다는 근거**입니다. DB 직접 쓰기 테스트는 정규화된 대문자 계약을 검사하므로 소문자를 거부하고, API는 ASCII 소문자 입력을 대문자로 정규화한 뒤 저장합니다.

### 테스트가 확인하는 경계

| 검증 방법 | 확인하는 것 | 이 결과만으로 확인할 수 없는 것 |
|---|---|---|
| 단위 테스트 | 통계 계산·프로젝트 참조 규칙 | HTTP·실제 DB 동작 |
| `WebApplicationFactory` + Fake/Stub | HTTP 계약, 입력·인증·오류, DI 대체, JSON 응답 | 실제 Kestrel 네트워크, SQL 번역·DB 제약·DB 동시성 |
| 실제 SQL Server 테스트 | 쿼리·SP 결과, FK/UNIQUE/CHECK, 원자성·동시 생성 | 공개 HTTPS·외부 인증 발급자·장기 운영 |
| EC2 API HTTP 검증 | 배포된 API의 인증·등록·조회·재시도·동시 요청 | 모든 장애 상황·성능 상한·장기 SLA |

2026-10-23 KST의 [검증 보고서](aws-validation.md)와 [결과 JSON](validation/aws-validation/report.json)은 검증 소스 `71c8a47`에 대한 다음 결과를 기록합니다. 이 수치는 당시 실행 기록이며 이 문서 추가로 새 실DB 검증을 수행했다는 뜻은 아닙니다.

- **전체 105개 통과 = 단위 10개 + 통합 95개.** 통합 95개에는 비DB 83개와 실DB 12개가 포함됩니다. 실DB 12개를 전체에 다시 더하지 않습니다.
- **EC2 API HTTP 18개 사례는 별도**입니다. 동시 요청 10건의 신규 생성 1건·기존 결과 재응답 9건은 그중 동시성 검증의 세부 결과입니다.
- DB 연결 실패의 live 200·ready 503·안전한 500, 잘못된 인증 설정의 시작 거부와 정상 버전 복구를 확인했습니다.

### 에이전트 활용과 해석의 한계

개발계획의 개발 주체는 **Codex + 사용자 검토**입니다. 구현·테스트·배포 검증 도구와 문서 작성에 에이전트를 활용했고, 선택 이유와 수정 과정은 [의사결정 기록](decisions.md), 실행 명령·실패·후속 성공은 [프로젝트 진행 기록](project-status.md)에 남겼습니다. 구현량만으로 이해도를 판단하기보다 위 요청 경로와 실패 처리의 이유를 코드·테스트로 확인할 수 있도록 구성했습니다.

기존 실환경 검증과 별도 리뷰는 동일 에이전트 세션이 수행했습니다. [리뷰 기록](reviews/aws-validation-review.md)에 독립성 한계가 있으며, 독립된 제3자 감사로 표현하지 않습니다. 비공개 Staging·SSM 경로와 검증용 JWT 자격을 사용했으므로 공개 HTTPS, 신뢰할 외부 인증 발급자 연동, 장기 가용성은 검증 범위 밖입니다.

이 문서는 구현을 이해하기 위한 설명서입니다. 실제 구현 규칙은 [dotnet-guidelines.md](dotnet-guidelines.md), HTTP 계약은 [api-contract.md](api-contract.md), DB 재현 절차는 [database-validation.md](database-validation.md), 배포 결과는 [aws-validation.md](aws-validation.md)를 각각 기준으로 확인합니다.
