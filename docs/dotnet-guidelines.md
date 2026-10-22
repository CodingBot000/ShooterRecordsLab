# .NET / ASP.NET Core / EF Core 구현 지침

> Java/Spring Boot 경험자가 이해하기 쉬운 방향으로 작성. 이 문서는 .NET 10 및 EF Core 10을 기준으로 한다.

## 1. Spring Boot 대응 관계(완전한 1:1 대응은 아님)

| Spring | ASP.NET Core / EF Core | 주의할 점 |
|---|---|---|
| `@RestController` | `[ApiController] : ControllerBase` | attribute routing / model validation |
| `@Service`, constructor DI | `services.AddScoped<I..., ...>()` | Scoped/Singleton/Transient 수명주기 |
| servlet Filter / Interceptor | Middleware | 등록 순서가 요청/응답 동작에 영향 |
| Spring Data JPA / Hibernate | EF Core `DbContext` | lifetime, tracking, migrations |
| `@Transactional` | `BeginTransactionAsync` / SaveChanges atomicity | 복수 작업·재시도 시 명시적 경계 |
| `@ControllerAdvice` | `IExceptionHandler`, `ProblemDetails` | 응답 계약 고정 |
| JUnit/MockMvc | xUnit / `WebApplicationFactory` | 실제 DB 통합 테스트 별도 |
| application.yml | `appsettings.*.json`, env, user-secrets | 비밀번호 저장소 관리 |
| `CompletableFuture` / async | `Task`, `async/await` | 비동기 I/O와 취소 전달 |

## 2. 프로젝트 생성 및 설계

- SDK: `.NET 10` LTS를 사용하고 `global.json`으로 기능 밴드를 관리. 지원 버전은 출시 당시 문서가 아니라 실제 `dotnet --info`와 공식 지원 정책 확인 후 고정.
- API 생성 권장: `dotnet new webapi --use-controllers -f net10.0 -n ShooterRecordsLab.Api -o src/ShooterRecordsLab.Api`.
- 도메인/애플리케이션/인프라/HTTP 계층으로 나누되, 각 계층의 목적이 분명할 때만 새로운 abstraction 추가.
- `Nullable` 및 implicit usings 허용, Analyzer 경고 활성화, code format 일관성. 모든 public API는 프로젝트 문서/테스트에 의해 동작을 설명.
- NuGet 버전은 호환되는 메이저 버전(예: EF Core 10 계열)을 사용하고, 실제 빌드한 정확한 버전은 lock/프로젝트 파일로 기록. `latest`에 무조건 의존하지 않는다.

## 3. API / DI / Middleware

- `ControllerBase`에서 입력 DTO 및 결과를 명확하게 처리하고 DB 쿼리를 직접 작성하지 않는다.
- `builder.Services.AddScoped<...>()`로 application service/repository 등록, EF Core `DbContext`도 request scoped 사용.
- `DbContext`는 **thread-safe하지 않으므로** 하나의 인스턴스를 여러 병렬 Task가 동시에 사용하지 않는다.
- DI singleton이 scoped DbContext를 직접/간접으로 보유하지 않도록 검증한다. options snapshot 사용 시 수명주기 확인.
- 순서가 중요한 Middleware는 `Program.cs`에 명시하고 `UseAuthentication`이 `UseAuthorization`보다 먼저 적용되게 한다.
- 개발에서만 OpenAPI 엔드포인트 공개. 배포 환경에서 인증되지 않은 문서/내부 진단 노출 금지.
- 오류는 ProblemDetails로 중앙 처리. 예외를 일일이 catch하고 200 응답으로 감추지 않는다.
- `IOptions<T>`와 startup validation으로 필수 설정(DB endpoint, Auth issuer 등)을 검사한다.

## 4. 비동기와 취소

- DB I/O: `ToListAsync`, `SingleOrDefaultAsync`, `SaveChangesAsync` 등 사용. HTTP action, 서비스, 저장소 메서드까지 `CancellationToken` 전달.
- `.Result` / `.Wait()` / `async void`(이벤트 핸들러 제외), sync-over-async 금지.
- 단순 동기 CPU 계산을 `Task.Run`으로 감싸 비동기처럼 보이게 하지 않는다.
- HTTP timeout, DB command timeout은 목적에 맞게 명시; 임의로 무한정 늘리지 않는다.
- 재시도는 순수 GET/읽기와 쓰기 사이의 차이를 고려. 특히 commit 응답이 유실된 경우 유니크 키 기반 멱등성 확인 없이 재시도 금지.

## 5. EF Core / SQL Server

- `Microsoft.EntityFrameworkCore.SqlServer`와 해당 EF 10의 tool/design 패키지를 호환 버전으로 사용.
- Model Configurations (`IEntityTypeConfiguration<T>`)에 키/길이/인덱스/제약을 둔다. DB에서 검증되는 규칙은 앱 검증만으로 대체하지 않는다.
- 조회 기본은 `AsNoTracking()`, DTO projection, `CancellationToken`, 안정적 ORDER BY + page 제한. query plan과 비용을 보고 `Include` 사용 결정.
- `DbContext`를 request 밖으로 보관하거나 static/singleton으로 저장하지 않는다. 공용 connection 한 개를 공유하여 병렬 실행하지 않는다.
- `SaveChangesAsync` 내부 기본 트랜잭션과 별도 명시적 트랜잭션이 필요한 범위를 구분한다. 2개 이상 독립 호출·멱등성 분기에서는 명시적 원자성 설계.
- SQL 및 Stored Procedure 호출은 parameterized 사용. Raw SQL에 사용자의 식별자/필드명을 문자열로 이어붙이지 않는다.
- 예상된 `DbUpdateException`이라도 SQL Server 고유 번호를 검사해 UNIQUE 위반인지 확인한다. 모든 에러를 409로 변환하지 않는다.
- Stored Procedure/제약/인덱스를 수정했다면 해당 DB migration/관리 SQL 스크립트, 대응 롤백, 테스트도 함께 갱신한다.

### Migration 예시(프로젝트 생성 후 실제 경로 확인)

```bash
# dotnet-ef 도구는 프로젝트 EF Core 메이저 버전과 호환되게 설치
# 이 명령은 스키마 변경 파일을 생성하며 DB에 자동 적용하지 않음
dotnet ef migrations add InitialCreate \
  --project src/ShooterRecordsLab.Infrastructure \
  --startup-project src/ShooterRecordsLab.Api \
  --output-dir Persistence/Migrations

dotnet ef migrations script --idempotent \
  --project src/ShooterRecordsLab.Infrastructure \
  --startup-project src/ShooterRecordsLab.Api \
  --output infra/scripts/migrations.sql
```

실제 DB에 `database update` / SQL script 적용 전에는 반드시 **대상 환경·DB 이름·백업/롤백·승인**을 확인한다. 앱 startup에서 자동 Migration 호출 금지.

## 6. 오류·검증·로그

- 전역 예외 핸들러: 알려진 비즈니스 예외만 400/404/409 등으로 매핑, 미지의 DB/코드 예외는 일반적인 5xx.
- API 공통 에러는 `ProblemDetails` + `traceId`; 비밀, SQL 텍스트, 내부 stack을 응답에 포함하지 않음.
- `ILogger<T>` 메시지 템플릿 구조화 사용(`logger.LogInformation("Match {MatchId} created", matchId)`). 대량 payload 원문 로그 금지.
- DTO validation(범위, 필수, Guid.Empty, 중복 Participant)과 DB 제약을 둘 다 적용.
- 개발 user-secrets, 배포 시 AWS 관리형 비밀 사용. 절대 `appsettings.json`에 실제 자격 정보 커밋 금지.

## 7. 품질 명령(프로젝트 생성 이후)

```bash
dotnet --info
dotnet restore
dotnet build -warnaserror
dotnet test --filter 'Category!=Database'
dotnet format --verify-no-changes
# AWS Test DB 연결을 승인받고 준비한 뒤에만:
dotnet test --filter 'Category=Database'
```

실패한 명령과 원인을 숨기지 않고 `docs/project-status.md`에 기록한다. DB 미연결로 테스트를 실행하지 않은 상태를 통과로 인정하지 않는다.

## 8. 참고

- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/ef/core/providers/sql-server/
- https://learn.microsoft.com/en-us/ef/core/saving/transactions
- https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
