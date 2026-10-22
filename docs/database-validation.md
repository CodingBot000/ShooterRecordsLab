# P2 SQL Server 준비와 P3 검증 경계

## 현재 상태

EF Core 10.0.13 SQL Server 모델·조회·등록·랭킹 호출과 Migration SQL을 작성했다. 실제 SQL Server에 접속하거나 스키마를 적용하지 않았다. SQL 문법·실행 결과·제약·동시성·실행 계획은 **미검증**이다. SQLite/EF InMemory로 대체 검증하지 않는다.

## 검토 대상

- `Persistence/Migrations/*InitialCreate*`: dbo.Players/Matches/MatchResults, FK/UNIQUE/CHECK, UTC·길이·수치 제약.
- 외부 ID: 대문자 ASCII를 앱에서 검증 후 정규화하고 DB BIN2 collation 및 고유 인덱스로 비교한다.
- `Persistence/Sql/V001_GetTopPlayerRankings.sql`: immutable migration 리소스. 새 프로시저 변경은 V002와 새 Migration으로 추가한다. `EXEC(N'...')`로 CREATE PROCEDURE가 idempotent IF 안에서도 별도 batch의 첫 문장이 되게 한다.
- `infra/scripts/migrations.sql`: 재현 가능한 idempotent 검토용 SQL. 실행하지 않음.
- 등록: 명시적 트랜잭션 안에서 참가자 존재 확인·Match/Results 저장·commit. 자동 execution-strategy 재시도 비활성. 취소/commit 응답 유실 후 클라이언트는 같은 external ID/payload로 재확인한다.
- SQL unique 2601/2627만 별도 처리하고, 트랜잭션 dispose/추적 해제 후 해당 external ID의 승자 레코드를 재조회한다. 기존 fingerprint가 있을 때만 200/409를 판정한다. 다른 DB 예외는 안전한 5xx다.
- 플레이어 목록 offset은 long 매개변수로 전달한다. 큰 int page에서 오버플로가 없고 모든 정렬에는 2차 키가 있다. 개수와 목록은 별도 read-committed 조회이므로 동시 쓰기 도중 totalCount와 items의 순간 시점은 다를 수 있다.
- 참가자는 최대 20개를 projection 후 .NET Guid 순서로 정렬한다. 경기 목록/랭킹의 DB UUID 정렬은 SQL Server uniqueidentifier 순서이며 Fake의 Guid.CompareTo와 일반적으로 다를 수 있다. SQL 검증에서 SQL 순서를 확인해야 한다.
- 통계와 랭킹 합계는 bigint/long을 사용해 int SUM 오버플로를 피한다.
- 제한: 개별 결과 값·FK/unique는 DB 제약, 참가자 2..20·상호 결과 규칙·미래 5분은 Application 검증이다. DB 직접 쓰기는 별도 권한으로 제한해야 한다.

## P3 전제와 순서

1. AWS 계정/리전·견적·월 예산·RDS/EC2/IAM/네트워크·백업/종료 계획을 정하고 실행 승인을 받는다.
2. Private RDS, SSM 터널, dev/test DB·전용 계정·최소 권한을 준비한다. RDS 인증서 검증을 유지한다.
3. 승인된 DB 이름과 서버를 확인하고 생성 SQL을 검토한 다음 별도 관리 경로로 적용한다. 실제 적용 절차는 승인된 접속 도구와 자격 방식에 맞춰 확정한다. 앱은 기동 시 migrate/seed하지 않는다.
4. 가상 seed가 필요하면 `infra/scripts/seed-demo.sql`을 별도로 승인받아 실행한다. DB 이름 제한은 보조 가드이며 승인·정확한 서버 확인을 대신하지 않는다.
5. 아래 환경변수를 로컬 보안 저장소/환경에 설정하고 Database 필터만 실행한다. 연결 정보가 없으면 테스트는 skip 대신 명시적으로 실패한다.

| 이름 | 의미 |
|---|---|
| `SHOOTER_RECORDS_DATABASE_TESTS_APPROVED` | 사용자가 대상·수정/정리 범위를 승인한 뒤 `1` |
| `SHOOTER_RECORDS_TEST_CONNECTION` | TLS 검증을 유지한 전용 Test DB 연결 문자열(비밀) |
| `SHOOTER_RECORDS_TEST_SERVER` | 연결 문자열 DataSource와 정확히 일치하는 승인된 서버/로컬 터널 주소 |
| `SHOOTER_RECORDS_TEST_DATABASE` | InitialCatalog와 정확히 일치하고 Test로 끝나는 승인 DB |

```bash
# 승인된 Test DB/SSM 연결이 준비된 뒤에만 실제 실행
dotnet test --filter 'Category=Database'
# 테스트를 실행하지 않고 필터 분리만 확인 가능
dotnet test --no-build --list-tests --filter 'Category=Database'
```

실DB 테스트는 10개 동시 등록을 각기 다른 Scoped DbContext에서 실행하고 1개 신규 생성·참가자 2건을 확인한다. 409, 참가자 미존재 원자성, FK/unique/check, 강제 FK 실패의 부모/자식 rollback, 통계·목록·랭킹/SP top 검증을 준비했다. 각 fixture가 만든 UUID/외부 ID 범위만 정리한다. DB drop/reset/truncate/Migration 자동 적용을 하지 않는다. 이 테스트는 현재 **NOT_RUN**이다.

## 실패와 롤백

- 적용 전: 스크립트, Migration 목록, 대상 DB, 백업/스냅샷·권한을 기록한다. 이 단계에서 실제 백업을 만들지 않았다.
- 적용 오류: EF history와 실제 schema를 조회하고 열린 트랜잭션 여부를 확인한다. 불명확한 commit 결과에 무조건 재실행하지 않는다.
- 프로시저만 롤백: AddRankingsProcedure의 Down은 프로시저를 제거하므로 랭킹 API를 함께 차단/버전 조정해야 한다.
- InitialCreate Down은 테이블/데이터를 삭제하는 **파괴적 작업**이다. 실행하지 않았으며 별도 사용자 승인·백업 없이는 금지한다. 기존 데이터가 있으면 forward fix를 우선 검토한다.
- 데이터 보존 기간·삭제 범위·AWS 정리 시점은 미정이며 임의로 결정하지 않는다.

## P4 이후 남은 검증

JWT issuer/audience·권한 mapping, 신뢰할 발급자, timeout/연결 장애·알 수 없는 commit, 실제 쿼리 플랜/logical reads, 공개 단계 요청 크기/rate limit과 용량 정책은 아직 미완료다. SQL Server 실증 없이 정합성/성능 완료로 보고하지 않는다.

공식 근거: [EF SQL Server](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), [트랜잭션](https://learn.microsoft.com/en-us/ef/core/saving/transactions), [SQL 매개변수와 SP](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries).
