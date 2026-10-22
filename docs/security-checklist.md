# 보안·운영 체크리스트 — ShooterRecordsLab

> 모든 항목은 코드/명령/실행 로그 등 **증거**를 남긴 뒤 체크한다. 체크하지 않았다고 실패가 되는 것은 아니지만, 완료인 척하지 않는다.

## A. 자격증명과 로깅

- [ ] DB connection string, DB 비밀번호, API JWT 서명 키, AWS 토큰은 Git에 없다.
- [ ] `.gitignore`에 `.env*`(샘플 제외), `appsettings.Development.local.json`, user-secrets 관련 사적 산출물, Terraform state, 인증 파일이 적절히 포함된다.
- [ ] 개발 비밀은 `.NET user-secrets` / 사용자 소유 환경 설정; AWS 배포 비밀은 관리형 저장소와 instance role.
- [ ] CLI `history`, CI 로그, 에러 응답, `ILogger`에 인증 헤더/SQL 비밀번호/개인 데이터 원문이 노출되지 않는다.
- [ ] JWT는 issuer/audience/signature/expiry를 실제 검증; 권한 `matches.write` 없으면 403.
- [ ] dev/test 인증 키를 prod에 복사하지 않는다. 실제 공개 서비스에는 신뢰할 수 있는 issuer가 필요하다.

## B. API/애플리케이션

- [ ] Controller DTO validation 및 명시적 status code.
- [ ] ProblemDetails에 SQL/스택 트레이스/내부 호스트명 비노출.
- [ ] Middleware 순서, authenticated/unauthenticated 경로 테스트.
- [ ] JSON 크기/참가자 수/page 제한, 과도한 body 및 rate limiting 검토(공개 시 필수).
- [ ] POST idempotency, UNIQUE index, transaction rollback, 취소/DB timeout.
- [ ] 400/401/403/404/409/5xx 사례 전부 테스트.
- [ ] `health/ready`의 실패는 내부 정보를 공개하지 않음.
- [ ] 로그와 추적 ID가 사고 분석에 충분하되 사용자 데이터를 과도하게 저장하지 않음.

## C. SQL Server

- [ ] RDS public access = No, SG 1433은 EC2 SG에만 허용.
- [ ] DB API 계정은 최소 권한, master 계정 애플리케이션 사용 금지.
- [ ] SQL/T-SQL은 모든 사용자 값 parameterized, stored proc도 파라미터 검증.
- [ ] UNIQUE/FK/CHECK와 migration SQL을 사람이 리뷰했고 Test DB에서 검증했다.
- [ ] SQL Server 인증서 검증과 연결 암호화, RDS CA 갱신 방법 확인.
- [ ] Dev/Test DB와 계정 격리; 파괴적 테스트/auto drop/운영 migration 차단.
- [ ] 백업/복원 및 schema 변경 롤백 절차가 문서화됨.

## D. AWS 비용 및 권한

- [ ] 리전/계정/예산/작업 대상 사용자 승인 전 `terraform apply`/`aws create-*` 실행 안 함.
- [ ] AWS Budgets 알림 설정 및 종료 시점 합의.
- [ ] EC2 인바운드 기본 거부, SSH/DB/public API 포트 자동 개방 안 함.
- [ ] IAM은 최소 권한 + SSM managed node, static AWS key 사용 안 함.
- [ ] NAT Gateway, Load Balancer, EIP 등의 예상 외 상시 비용 자원 생성 확인.
- [ ] 외부 HTTPS 공개는 별도의 명시적 승인 이후 진행.
- [ ] 종료 시 RDS/EC2/EBS/ECR/IPv4/로그 등 남은 요금 원천 점검.

## E. 재현/배포

- [ ] Clean restore/build/test 성공 로그; 실패 및 skip 개별 표시.
- [ ] 배포 이미지가 non-root, 확정 base version, 최소 노출 포트를 사용.
- [ ] OpenAPI/internal admin 기능 개발 환경 외 비활성 또는 접근 제어.
- [ ] SQL DB Migrations는 앱 부팅 시 자동 적용하지 않음.
- [ ] rollback 시 DB 스키마 호환성 검토.
- [ ] README에 재현 가능한 실행/검증 명령과 필요한 환경변수 이름만 기록.

체크리스트 자체를 수정한 경우 관련 `docs/project-status.md`의 테스트/배포 기록과 설계 문서를 함께 갱신한다.
