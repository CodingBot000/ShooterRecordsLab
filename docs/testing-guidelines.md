# 테스트 지침 — ShooterRecordsLab

## 1. 기본 원칙

- **테스트가 통과한다는 말은 실제 실행 결과를 확인한 경우에만 사용한다.** 코드를 작성했다는 것과 테스트 실행은 다르다.
- DB 없이 가능한 단위/HTTP 통합 테스트와 **실제 SQL Server가 반드시 필요한** 데이터 테스트를 구분한다.
- DB 쿼리, 인덱스, FK/UNIQUE, Stored Procedure, collation, 트랜잭션을 SQLite/EF InMemory만으로 검증했다고 주장하지 않는다.
- 외부 유료 AWS DB나 네트워크에 접근하는 검증은 미리 계획·비용 범위·대상 DB를 확인한다.
- 테스트가 실패하면 기능을 임의로 줄이거나 assertion을 삭제하여 가짜 성공을 만들지 않는다.

## 2. 테스트 종류와 실행 환경

| 분류 | 프레임워크 | DB | 언제 |
|---|---|---|---|
| Unit | xUnit + test doubles | 없음 | 매 변경 |
| HTTP integration | `WebApplicationFactory<Program>` + Fake Repository/Auth | 없음 | 매 변경 |
| SQL integration | xUnit + 실제 SQL Server 전용 테스트 DB | AWS RDS(Test DB) | P3 후 DB 변경마다 |
| Performance smoke | 실행 계획/측정 SQL + 명시된 표본 | RDS 개발 DB | P4/P6 |
| Cloud smoke | SSM tunnel + 배포된 API | RDS/EC2 | 승인된 P5 |

`WebApplicationFactory` 사용 시 테스트 환경에서 DI를 갈아 끼워 fake repository/auth handler를 주입한다. 필요하면 `public partial class Program { }`로 entry point를 노출한다. SQL 통합 테스트는 실제 EF Core provider와 진짜 DB를 사용한다.

## 3. 필수 테스트 매트릭스

| 대상 | 정상 시나리오 | 실패/경계 시나리오 |
|---|---|---|
| Player 조회 | 200, 응답 필드 정확 | 미존재 404, Empty Guid/형식 불일치 |
| Match 목록 | 시간 역순, 페이지 | 존재하지만 0건, 잘못된 page/pageSize, 동시각 tie break |
| Stats | 1승1패 통계/비율 | 0경기 => null winRate, 0 deaths => null kdRatio, 반올림 |
| Get Match | 참가자 정렬, 응답 필드 | 매치 없음 404 |
| 경기 등록 | 201, 모든 참가자 저장 | 참가자 중복/미존재, 음수, 불일치 결과, 로컬 타임존 값 |
| 멱등성 | 동일 요청 2회 -> 201/200, 1건 | 동일 ID 다른 payload 409, 참가자 순서만 다른 payload 동일 취급 |
| 경쟁 상황 | 같은 external ID 동시 10개 -> 1건 | DB unique 충돌에서 응답 안정화, 일부 데이터만 남지 않음 |
| 랭킹 | 상위 TOP-N / 저장 프로시저 실행 | 랭킹 0건, 1/100/101, 정렬 동률, SQL injection 방지 |
| 인증 | writer 토큰으로 POST | 없음·만료·서명 오염 401, 권한 없음 403 |
| 건강 체크 | live 성공, ready 성공 | DB 끊으면 ready 실패, 민감 정보 미노출 |
| 로그 | 추적 ID와 수준 | 비밀번호·Bearer 토큰·요청원문 미출력 |

테스트 데이터는 고정된 fake UUID와 격리 SQL Server Test DB 사용. 타임존과 난수는 deterministic test fixture로 고정한다.

## 4. SQL 통합 테스트 환경 안전장치

- 개발 DB와 테스트 DB를 구분하고 이름에 `Test`를 명시. 예: `ShooterRecordsLabTest`.
- **테스트 전용 SQL 로그인과 최소 권한**. 비밀번호는 user-secrets/AWS 관리형 시크릿으로만 제공한다.
- RDS endpoint/DB 이름이 허가된 목록과 다르면 즉시 실패. DB 이름 prefix만으로 안전성을 보장한다고 가정하지 말고 SQL 연결 대상도 확인한다.
- 테스트는 서로의 데이터를 지우지 않도록 유니크한 externalMatchId 및 Player UUID를 생성한다.
- `EnsureDeleted`/`DROP DATABASE`/전체 테이블 truncate 자동 실행 금지. 테스트 후 자신이 생성한 데이터만 관계 순서대로 정리한다.
- `database update`는 사용자가 승인한 Test DB에만 실행. 연결이 없을 경우 tests를 **skip으로 통과처럼 포장하지 말고** 미실행 상태로 별도 보고한다.
- 테스트 DB가 준비되지 않았다면 DB 테스트는 환경 오류로 실패해도 괜찮다. 소스 코드 검증과 구분하여 기록한다.

## 5. 실행 명령

```bash
# 빠른 게이트: 로컬 및 일반 CI
 dotnet restore
 dotnet build -warnaserror
 dotnet test --filter 'Category!=Database'
 dotnet format --verify-no-changes

# RDS 전용 테스트 DB / SSM 연결이 준비된 개발자 환경
 dotnet test --filter 'Category=Database'
```

DB 테스트 메서드/클래스에는 xUnit `[Trait("Category", "Database")]`를 명시한다. 런너 필터 동작은 최초 테스트 생성 시 실증한다. CI의 기본 job은 DB 테스트를 포함하지 않고, DB integration job이 실행되지 않았음을 표시한다.

### GitHub Actions CI

- 정의: [ci.yml](../.github/workflows/ci.yml). `main` 대상 `pull_request`, `main`의 `push`, `workflow_dispatch`에서 실행한다. 필수 검사로 지정할 job 이름은 `verify`다. 경로 필터를 두지 않아 문서 변경 PR에서도 필수 검사가 생성된다.
- GitHub-hosted Ubuntu runner에 `global.json`의 SDK를 준비한 뒤 `bash infra/scripts/verify.sh artifacts/validation/ci`를 실행한다. 비DB 게이트와 실패 시 중단 동작은 로컬과 공유한다.
- 성공/실패에 관계없이 생성된 `*.log`만 `verification-logs` artifact로 업로드한다. SDK 준비 전에 실패하면 검증 로그가 없을 수 있으므로 Actions step 로그도 확인한다. 보존 기간·실행 시간·동시 실행 제한은 별도 정책을 추가하지 않고 기존 GitHub 설정을 따른다.
- 실제 SQL Server 테스트는 실행하지 않으며 job summary에 **Database tests: NOT_RUN**을 표시한다. 이 결과를 SQL Server 정합성·AWS 배포 검증으로 확대하지 않는다.
- 권한은 `contents: read`, checkout 자격증명 지속 저장은 비활성이다. 공식 액션은 확인한 전체 commit SHA로 고정하며, SHA 갱신 시 해당 공식 저장소와 변경 내용을 확인한다. 비밀·DB 연결·클라우드 배포는 추가하지 않는다.
- 완료 절차: Actions의 실행 URL·대상 SHA·성공/실패·실제 테스트 수·artifact를 확인 → `review-guidelines.md`에 따른 별도 리뷰 → 필요한 수정과 재검증 → `project-status.md`에 결과·한계·다음 단계 기록. 결정이나 계약이 바뀌면 해당 문서도 갱신한다. CI 성공 자체가 결과 리뷰·문서 반영 완료를 뜻하지 않는다.
- `main` 보호는 실제 `verify` 실행에서 확인한 GitHub Actions 앱(ID `15368`)의 검사만 필수로 지정한다. 사용자가 소유자 적용을 선택했으므로 `enforce_admins=true`이며 소유자도 필수 검사를 통과해야 한다. 새 변경은 작업 브랜치와 PR에서 검사를 통과시킨 뒤 반영한다. 별도의 리뷰 승인 수나 브랜치 최신화 강제는 추가하지 않았다. 실제 적용/조회 결과는 의사결정·진행 기록에 남기며, 필수 검사 이름 변경 시 보호 설정도 함께 검토한다.

## 6. P3 이전·이후의 통과 의미

- **P0~P2:** build, unit, fake HTTP integration 성공 가능. DB 통합 테스트는 `미실행 — 아직 AWS 승인/DB 없음`으로 기록.
- **P3 이후:** RDS Test DB에 Migration 후 DB 테스트 수행. SQL Server 관련 구현을 변경했다면 DB 테스트를 통과해야 해당 단계 완료.
- **P4~P6:** 공개/배포 범위의 보안 smoke + SQL 동시성 테스트 + 쿼리 실행 계획. 부하 임계치는 환경·표본 수·비용을 함께 기록.

## 7. 검증 보고 형식

```text
Test run date: YYYY-MM-DD HH:mm (KST)
Commit: <SHA>
SDK: dotnet --version
Environment: local Mac / RDS Test DB / EC2
Command: <실행한 실제 명령>
Result: PASS|FAIL|NOT_RUN
Count: <passed>/<failed>/<skipped> (실제 측정치만)
Logs/artifacts: <경로 또는 요약>
Known gaps: <검증하지 못한 부분>
```

원인 분석이 필요한 실패는 재현 절차, 기대 결과, 실제 결과, 수정, 회귀 테스트를 함께 기록한다.

## 8. 참고

- https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy
- https://learn.microsoft.com/en-us/ef/core/saving/transactions
