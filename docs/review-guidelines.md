# Codex 독립 검증/코드 리뷰 지침

## 목표

Codex가 기능을 구현했다고 보고하는 것과 **실제로 올바른지 검증하는 것**을 분리한다. 동일 모델/세션의 자기 점검은 독립 검증이 아니므로 한계를 표시하고, 가능한 경우 별도 리뷰 세션/에이전트를 활용한다. 새 도구나 스킬의 사용은 필수가 아니다.

## 각 기능 완료 시 리뷰 순서

1. **요구 계약 확인:** `docs/api-contract.md`의 상태 코드/DTO/입력 제한/정렬/멱등성 규칙과 코드의 불일치를 찾는다.
2. **정합성 리뷰:** FK, unique, 트랜잭션 원자성, concurrent POST, 단위 계산(승률/KD), 타임존, 취소/timeout 확인.
3. **.NET 리뷰:** scoped DbContext 수명주기, Singleton 캡처, middleware/auth 순서, `Task`/`CancellationToken`, sync-over-async 확인.
4. **DB 리뷰:** LINQ 번역 가능성, N+1, 인덱스, Stored Procedure 파라미터 처리, 실행 계획과 실제 MSSQL 테스트.
5. **보안 리뷰:** JWT 검증/인가, 비밀 유출, SQL injection, overly permissive SG, 공개된 내부 엔드포인트, 로그의 토큰/비밀번호.
6. **테스트 부정행위 탐지:** 누락된 실패/경계 테스트, assert 제거, mock만 통과하는지, skipped를 성공으로 포장했는지.
7. **재현성:** clean build/test, 마이그레이션 적용, sample curl/OpenAPI, Docker 빌드와 환경 분리, 승인 기록.

## 리뷰 결과의 기본 형태

```text
검토 범위: 변경 파일/기능/커밋
결론: PASS / PASS_WITH_RISKS / FAIL
Critical (보안/데이터 소실/무단 비용):
High (계약/정합성/테스트 불일치):
Medium (성능/유지보수성):
Low (표기/가독성):
각 항목: 파일·라인/증거 → 재현 조건 → 실제 문제 → 권장 최소 수정 → 추가 테스트
실제로 실행한 검증:
실행하지 못한 검증:
남은 위험과 승인 필요 사항:
```

## 실패를 주입하는 테스트 리뷰 예시

- POST 응답 직전 통신 끊김을 가정한 동일 외부 ID 재시도에서 중복이 생기지 않는가?
- DB UNIQUE violation과 네트워크/timeout을 모두 같은 `409`로 매핑하는 실수가 없는가?
- DbContext를 `Task.WhenAll`로 병렬 사용하면서 간헐적 오류가 발생할 수 있는가?
- DB 연결 문자열을 로거/ProblemDetails에 포함하는가?
- test DB와 dev DB가 혼동되었을 때 DROP 등이 실행될 수 있는가?
- 랭킹 tie-breaker가 없어 페이지마다 순서가 뒤바뀌는가?
- .NET 10/EF Core 10 패키지 경고를 단순 비활성화한 부분이 있는가?

## 수정 루프

Critical/High는 다음 단계로 넘어가기 전에 해결하거나 사용자가 명시적으로 위험을 수용한 결정을 `docs/decisions.md`에 기록한다. Medium/Low는 우선순위·추적 항목으로 남길 수 있다. 리뷰 결과는 `docs/project-status.md`에 링크/요약을 남긴다.

최종 보고에는 **좋은 점과 문제점**을 모두 적는다. 수정 실패/미검증을 성공으로 설명하지 않는다.
