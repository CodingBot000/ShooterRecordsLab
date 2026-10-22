# AWS 실환경 배포·검증 보고서

- 검증일: 2026-10-23 01:40 KST
- 검증 소스: [`71c8a47`](https://github.com/CodingBot000/ShooterRecordsLab/tree/71c8a4784115b82905b7bcd02fcf841e6c2be183)
- 컨테이너 내용 digest: `sha256:f25bcb3fa4677138728db95beb8a95c8dd15aec6aff47ac4acad92a9d6dfd6b8`
- 환경: 서울 리전, EC2 Linux x86_64의 Docker API, private RDS SQL Server 2022 Express, SSM 경유 검증
- 기계 판독 결과: [report.json](validation/aws-validation/report.json)

## 실제 실행한 검증

| 범위 | 결과 |
|---|---|
| Linux Release build | 경고/오류 0 |
| 전체 단위·HTTP·실DB 통합 테스트 | **105 통과 / 실패 0 / skip 0** |
| 위 전체 테스트에 포함된 실제 SQL Server 테스트 | **12 통과**: 동시성/원자성/FK/UNIQUE/CHECK/SP 및 문자 경계 |
| EC2 API HTTP 검증 | **18개 검증 사례 통과**: 인증·권한·등록·재시도·충돌·조회·정렬·페이지 |
| 같은 external ID 동시 HTTP 요청 | **10개 중 1개 신규 생성**, 9개 재응답, 동일 리소스 확인 |
| 실제 DB TLS·네트워크 설정 | 인증서 검증을 유지한 연결, 암호화 저장소·비공개 RDS·EC2 inbound 0·SG 참조 확인 |
| 컨테이너 | Linux x86_64, 검증 소스 revision label 일치, 비root·읽기 전용·health 확인 |
| DB 연결 실패 주입 | live 200 / ready 503 / 내부정보 없는 500 |
| 잘못된 배포 설정 및 복구 | 잘못된 인증 설정 startup 거부 후 검증된 버전으로 복구, ready 200 확인 |

단위 10개와 통합 95개를 각각 실행했다. 통합 95개는 비DB HTTP/모델 테스트 83개와 실제 SQL Server 테스트 12개를 포함한다. 12개를 105개에 다시 더하지 않는다. 실제 환경 HTTP 검증은 별도다.

## 대표 SQL 실행 계획·측정

가상 경기 5,000개와 참가자 결과 **10,000개**를 사용했다. 실제 runtime counter가 있는 실행 계획에서 인덱스 접근과 집계/정렬을 확인했다. 원본 계획의 서버/DB/연결 메타데이터는 공개하지 않고 수치만 보존한다.

| 조회 | 반환 행 | Logical reads | 계획 연산자 | 측정 시간(ms) |
|---|---:|---:|---:|---:|
| 플레이어 경기 목록 | 20 | 59 | 4 | 143.86 |
| 플레이어 통계 | 1 | 43 | 4 | 49.79 |
| 랭킹 저장 프로시저 | 2 | 88 | 9 | 74.11 |

목록은 Index Scan/Seek, 통계는 Index Seek, 랭킹은 집계용 인덱스 접근과 정렬을 확인했다. 시간은 클라이언트·SSM 통신과 결과 읽기를 포함한 단일 실측이며 서버 순수 실행 시간이나 처리량 상한을 뜻하지 않는다. 데이터 분포·캐시·동시성에 따라 달라질 수 있다.

## 실제 DB 검증에서 발견하고 수정한 문제

초기 CHECK 문자 집합이 정상 하이픈 포함 external ID를 거부했다. SQL Server에서 상수 입력으로 재현하고 하이픈의 집합 내 위치를 수정했다. 공개된 InitialCreate는 그대로 두고 `FixExternalMatchIdCharacterSet` 후속 Migration을 추가했다. 기존 동시성/원자성 검증을 유지하면서 정상·금지 문자에 대한 실제 DB 사례 11개를 추가해 검증했다.

STATISTICS XML 결과가 SQL Client에서 nvarchar로 반환되는 점도 확인해 XML 타입과 ShowPlanXML 문자열을 모두 처리했다. 실제 runtime counter를 확인하지 않은 계획은 성공으로 기록하지 않는다. 변경 이유는 ADR-011과 Git 이력에 있다.

## 재현 경로와 공개 범위

- 코드·모델·계약: `src/`, `docs/api-contract.md`, `docs/architecture.md`.
- 로컬/비DB: `./infra/scripts/verify.sh`.
- 실제 SQL Server: 승인된 전용 Test DB와 정확한 대상 allowlist를 준비한 뒤 `dotnet test --filter 'Category=Database'`.
- 컨테이너: `infra/docker/Dockerfile`, 명시적 관리 도구 `tools/ShooterRecordsLab.Validation`.
- IaC: `infra/terraform/`, provider mock 검증은 실제 AWS 검증과 구분한다.

공개 보고서는 검증 일자·소스 commit·이미지 내용 hash·검증 항목·수치만 허용한다. 계정/자원 식별자·접속 주소·키·토큰·비밀번호·연결 문자열·원본 state/plan/CloudTrail/SQL 로그는 포함하지 않는다. 허용 필드만 추출하는 도구는 `infra/scripts/export-validation-evidence.py`에 있다.

## 검증의 한계

동일 에이전트 세션이 구현·실행·별도 리뷰를 수행했다. 독립된 제3자 감사나 장기 가용성/SLA 검증은 아니다. 실제 검증은 private SSM 경로와 Staging용 검증 자격을 사용했으며, 공개 HTTPS 서비스·외부 인증 발급자 연동은 범위에 포함하지 않는다. 신뢰할 외부 발급자와 운영 정책은 공개 서비스 전 별도 검토가 필요하다.
