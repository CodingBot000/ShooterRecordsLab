# 실환경 검증 후 별도 자기 리뷰

- 검토일: 2026-10-23 KST
- 소스: `71c8a47` 및 실제 실행 결과 보고서
- 결론: **PASS_WITH_RISKS — 승인된 private SSM/Staging 학습 검증 범위**
- 독립성: 구현·실행과 동일 에이전트의 별도 검토 패스이며 독립된 제3자 감사는 아니다.

## 확인한 근거

- Linux 전체 105개 통과, 실패/skip 0. 실제 SQL Server 12개와 EC2 HTTP 검증 18개를 구분했다. mock provider 6개를 실환경 결과로 계산하지 않는다.
- 실DB에서 발견한 CHECK 문자 집합 문제는 입력을 줄여 재현하고 forward Migration으로 수정했다. 기존 assertion을 제거하지 않고 DB 문자 경계 11개를 추가해 통과했다.
- SQL unique 경쟁에서 실제 동시 API 요청 10개에 리소스 1개만 생성됨을 확인했다. FK 실패의 parent/child rollback, CHECK·unique·통계·SP도 실DB에서 확인했다.
- 저장 프로시저의 top, 목록의 offset/조건 값은 매개변수화되어 있다. DB 조회는 projection/no-tracking·bigint 합계와 결정적 정렬을 사용한다.
- 실제 runtime counter가 있는 SQL 실행 계획 3종을 확인했다. 10,000개 결과에서 인덱스 접근/집계/정렬을 검토하고 읽기/시간 수치를 보존했다. SLA·처리량 상한으로 확대 해석하지 않는다.
- JWT issuer/audience/signature/expiry·scope 오류와 안전한 응답, 실제 TLS 인증서 검증, private RDS/SG·비root/읽기 전용·image revision을 확인했다.
- DB 연결 장애 주입 및 잘못된 배포 설정에서 안전한 실패와 정상 버전 복구를 확인했다.
- GitHub에는 allowlist 필드의 검증 자료만 공개한다. 실제 계정/주소/ID/키/토큰/연결 문자열/원본 state·plan·로그·SQL XML을 넣지 않는다. 실제 민감값 대조·기본 credential 패턴 검사 결과를 별도 기록한다.

## 남은 한계

- 검증용 Staging 자격을 사용했다. 공개 HTTPS·신뢰할 외부 OAuth/OIDC 발급자·운영 키 회전·공개 서비스 rate limit/용량/SLA 정책은 별도 설계/승인이 필요하다.
- DB 통계/목록은 별도 조회이므로 동시 쓰기 순간의 totalCount/items snapshot 일치까지 보장하지 않는다.
- 최종 NuGet 조회에서 알려진 취약 패키지는 없었지만 OS/전체 의존성/아키텍처에 대한 독립 보안 감사는 수행하지 않았다.
- 원본 인프라/접속 증거를 공개하지 않으므로 공개 보고서만으로 독립적인 진위 보증을 제공하지 않는다. 코드·내용 hash·검증 일자·실행 결과의 연결과 재현 절차를 제공한다.
