# P0~P2 별도 자기 리뷰

- 검토일: 2026-10-23 (KST)
- 범위: 루트 설정, 4계층 API/서비스/EF 코드, DTO·오류·로깅, SQL/Migration, 테스트·실행 문서
- 결론: **PASS_WITH_RISKS — P0~P2 비DB 게이트 기준**
- 독립성: 구현자와 동일 세션에서 별도 요구사항·실패 관점으로 수행했다. 별도 사람/에이전트의 독립 검증은 아니다.

## 수정한 발견 사항

| 증거/조건 | 문제 | 최소 수정과 확인 |
|---|---|---|
| 외부 ID `ſ`를 대문자로 변환 | ASCII 외 문자가 S로 접혀 승인될 수 있음 | 원문 trim 후 ASCII 검증 → 대문자 정규화, HTTP 400 회귀 테스트 |
| 예외가 요청 로그 래퍼 밖에서 처리됨 | 500 요청이 로그에 200으로 기록될 수 있음 | 로그 래퍼 안에 예외 처리 배치, 캡처 로그에서 최종 500과 원문 비밀 미노출 확인 |
| 기본 enum converter | 이름 대소문자/숫자 허용이 계약과 달라질 수 있음 | 정확한 문자열 converter, 잘못된 outcome 400 테스트 |
| EF Design 복원 | Pkcs 9.0.11 전이 취약점 경고 | 10.0.13 직접 고정, warn-as-error 빌드와 전이 포함 취약점 조회 통과 |
| 생성 Migration의 block namespace / xUnit Assert.Single(Where) | 프로젝트 analyzer 게이트 실패 | dotnet format 수정, 경고 억제 없이 재검증 |
| 큰 int page × pageSize | int 오버플로 가능 | long 계산과 매개변수 SQL OFFSET, 경계 테스트 |
| 메모리 저장소/환경 설정 | Production Fake 노출 또는 테스트의 외부 DB 접속 위험 | Fake는 tests로 이동, 쓰기는 Development 한정, 비DB WebHost에서 연결과 DbContext 옵션 명시적으로 제거 |
| Domain Match의 기본 collection expression | EF materialization 시 고정 크기 collection이 될 수 있음 | mutable List로 기본 navigation 초기화. 실제 Include/materialization은 실DB 검증이 남음 |

## 확인 결과

- HTTP/Domain/EF 모델을 직접 공용 직렬화하지 않는다. Controller는 서비스 호출과 HTTP 변환, 도메인 규칙은 Application/Domain, DB 작업은 Infrastructure에 있다.
- Domain/Application은 ASP.NET Core/EF 패키지를 참조하지 않는다. DI의 서비스·DbContext는 Scoped, Singleton은 TimeProvider와 테스트 저장소에 한정된다.
- DB/HTTP의 async·CancellationToken 전달, projection/no-tracking, 매개변수 SQL, bigint 집계를 확인했다. .Result/.Wait/Task.Run/자동 migrate/drop은 없다.
- external ID UNIQUE와 참가자 UNIQUE/FK/CHECK가 SQL 생성물에 있다. 알려진 SQL 2601/2627 외의 장애는 conflict로 바꾸지 않는다. 트랜잭션 dispose 후 재조회한다.
- fingerprint는 정규화 ID, UTC ticks, 정렬된 참가자와 불변 정수 형식으로 구성한다. POST Location·replay·conflict·빈 데이터·0데스·반올림·잘못된 입력·500을 비DB 테스트로 확인했다.
- live/ready/OpenAPI 환경 경계, 처리된 예외 원문·인증 헤더·payload 미기록을 확인했다. 운영 JWT는 아직 없다.
- `docs/init` 12개 Markdown SHA-256 일치, 설정 무시 규칙 및 제한적인 credential 패턴 검사 통과. 전면적인 비밀/보안 감사라고 주장하지 않는다.
- 실행 증거: `docs/validation/p0`, `p1`, `p2`, 특히 `p2/clean-snapshot.log`.

## 남은 위험과 승인 필요 사항

- **실DB NOT_RUN**: SQL 문법/자료형 mapping·collation·트랜잭션 rollback·고유 키 경쟁·SP 집계/정렬·인덱스 플랜을 SQL Server 2022에서 확인하지 않았다. 비DB 게이트 통과가 이를 입증하지 않는다.
- P2의 Database 테스트 1개 시나리오를 작성했고 필터 discovery만 확인했다. 10개 경쟁 요청과 제약·원자성 등을 포함하지만 아직 실행하지 않았다.
- page의 totalCount와 items는 별도 조회여서 동시 쓰기의 순간 시점 차이가 가능하다. 요구되는 snapshot 일관성/SLA는 정해지지 않았다.
- JWT issuer/audience/권한 mapping·토큰 실패, 공개 API 크기/rate limits·성능·실행 계획, 운영 관측성/연결 장애·commit 유실은 P4 검증 대상이다. Development의 미인증 쓰기는 외부 공개하면 안 된다.
- AWS 계정/리전·월 예산·RDS/EC2 사양·보존/정리와 실행 승인은 미정이다. 이 세션에서 AWS/DB 변경·실배포를 수행하지 않았다.
- 최초 커밋이 없어 실제 fresh clone/커밋 SHA 검증은 없다. 소스만 복사한 깨끗한 디렉터리에서 locked restore/build/test/format을 재현했다.
