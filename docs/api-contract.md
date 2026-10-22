# API Contract — ShooterRecordsLab v1

> Base path: `/api` · JSON UTF-8 · 서버/클라이언트 시간은 UTC `DateTimeOffset` ISO 8601 (`Z` 또는 `+00:00`) · 모든 ID는 UUID · 실제 개인/게임 데이터 금지

이 문서는 Controller와 테스트가 일치해야 하는 **단일 계약 문서**다. 코드 변경으로 계약이 달라지면 먼저 문서·테스트를 함께 수정한다.

## 공통 원칙

- API 에러: `application/problem+json` (RFC 7807 형식). `title`, `status`, `detail`(안전한 설명), `traceId`, 선택적 `code`를 포함하되 SQL/토큰/스택트레이스는 비공개.
- 경로 UUID는 `:guid` 라우트 제약을 사용한다. 형식 불일치는 404, `Guid.Empty`는 400(`INVALID_ID`), 유효한 UUID의 미존재 리소스는 404다. P1 HTTP 계약 테스트로 검증한다.
- 데이터 없음: 단일 리소스는 404, 컬렉션은 빈 배열 + 유효한 pagination.
- `page`: 1부터 시작, 기본 1; `pageSize`: 기본 20, 허용 1..100. 0/음수/100 초과는 400(자동 보정 금지).
- 정렬은 항상 안정적(동률용 2차 키 포함). 페이지 수 대신 `totalCount` 제공.
- 모든 HTTP API의 public DTO는 EF Entity를 직접 노출하지 않는다.
- 실습용 Player 데이터는 **개발·테스트 전용 명시적 seed 명령**으로 생성한다. 운영 환경 자동 seed 금지.
- 별도 인증 요건이 없는 조회는 가상 데이터에 한해 허용. 쓰기에는 유효 JWT와 `matches.write` 권한이 필요하다(인가 완성 전 P1/P2는 개발환경 한정).

## 1. GET /api/players/{playerId}

**성공 200**

```json
{
  "playerId": "11111111-1111-1111-1111-111111111111",
  "displayName": "PracticePlayerA",
  "createdAtUtc": "2026-10-20T08:00:00Z"
}
```

- 없음: 404. 데이터에는 게임 내 가상 닉네임 외 실명/메일 등 PII 없음.

## 2. GET /api/players/{playerId}/matches?page=1&pageSize=20

**성공 200**

```json
{
  "items": [
    {
      "matchId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "playedAtUtc": "2026-10-21T12:30:00Z",
      "kills": 8,
      "deaths": 3,
      "outcome": "Win"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

- 정렬: `playedAtUtc DESC`, 동률이면 `matchId DESC`.
- Player 자체가 없으면 404, 존재하지만 경기가 없으면 빈 `items`.

## 3. GET /api/players/{playerId}/stats

**성공 200**

```json
{
  "playerId": "11111111-1111-1111-1111-111111111111",
  "totalMatches": 2,
  "wins": 1,
  "losses": 1,
  "draws": 0,
  "kills": 15,
  "deaths": 10,
  "winRate": 0.5,
  "kdRatio": 1.5
}
```

- `winRate = wins / totalMatches`(0..1 비율), `kdRatio = kills / deaths`; 소수점 **4자리 반올림(MidpointRounding.AwayFromZero)** 후 JSON 숫자로 응답.
- `totalMatches==0`일 때 `winRate=null`, `deaths==0`일 때 `kdRatio=null`(Infinity/NaN 금지).
- Player 자체가 없으면 404.

## 4. GET /api/matches/{matchId}

**성공 200**

```json
{
  "matchId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "externalMatchId": "DEMO-MATCH-0001",
  "playedAtUtc": "2026-10-21T12:30:00Z",
  "participants": [
    { "playerId": "11111111-1111-1111-1111-111111111111", "kills": 8, "deaths": 3, "outcome": "Win" },
    { "playerId": "22222222-2222-2222-2222-222222222222", "kills": 3, "deaths": 8, "outcome": "Loss" }
  ]
}
```

- 참가자 배열은 `playerId` 오름차순으로 반환, 멱등성 응답에도 같은 직렬화 사용.
- 없으면 404.

## 5. POST /api/matches

인증: `Authorization: Bearer <signed_jwt>` + `matches.write` 권한(토큰의 scope/permissions mapping은 구현 시 하나로 고정하여 문서화).

**요청**

```json
{
  "externalMatchId": "DEMO-MATCH-0001",
  "playedAtUtc": "2026-10-21T12:30:00Z",
  "participants": [
    { "playerId": "11111111-1111-1111-1111-111111111111", "kills": 8, "deaths": 3, "outcome": "Win" },
    { "playerId": "22222222-2222-2222-2222-222222222222", "kills": 3, "deaths": 8, "outcome": "Loss" }
  ]
}
```

**검증**

- `externalMatchId`: 공백 아닌 ASCII 영문/숫자/`-`/`_`만 허용, 길이 1~64, trim 후 대문자 불변 정규화. 예: `demo-match-0001`과 `DEMO-MATCH-0001`은 같은 ID.
- `playedAtUtc`: UTC 오프셋 `+00:00` / `Z`만 허용. **서버 현재 UTC 시각보다 5분 초과 미래는 400**. 임의의 로컬 시각 허용 금지.
- `participants`: 2~20명, `playerId` 중복 금지, 존재하는 Player만 가능, 킬/데스 0..10000.
- `outcome`: `Win` / `Loss` / `Draw`. 참가자 전원이 Draw이거나, 한 명 이상 Win + 한 명 이상 Loss이고 Draw은 0명인 경우만 허용(학습용 단순 규칙).
- payload canonicalization: 정규화된 `externalMatchId`, UTC 시각(UTC ticks로 동일화), 참가자를 `playerId` 순으로 정렬한 뒤 정수/결과를 안정된 형식으로 직렬화. 그 결과의 SHA-256 fingerprint를 저장하여 재시도 비교. **참가자 입력 순서는 동등성에 영향을 주지 않는다.**

**최초 저장 시 201 Created** + `Location: /api/matches/{matchId}`

```json
{
  "matchId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "externalMatchId": "DEMO-MATCH-0001",
  "created": true
}
```

**동일 ID·동일 payload 재시도 시 200 OK**

```json
{
  "matchId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "externalMatchId": "DEMO-MATCH-0001",
  "created": false
}
```

- 같은 외부 ID지만 payload 내용이 달라지면 **409 Conflict**, 오류 코드 `MATCH_ID_CONFLICT`.
- 존재하지 않는 참가자/잘못된 규칙은 **400 Bad Request**(코드 예: `INVALID_PARTICIPANTS`).
- 유효 서명/토큰 없음/만료: **401**. 인증은 되었지만 `matches.write` 없음: **403**.
- DB 저장 오류: 민감 내용 없는 5xx; DB 유니크 충돌 **이외**의 예외를 무조건 409로 변환하지 않는다.
- 데이터베이스 유니크 인덱스가 동시 등록의 최종 방어선. 애플리케이션 사전 조회만으로 보장 불가.

## 6. GET /api/rankings?top=20

- `top` 기본 20, 허용 1..100. 기록이 한 건 이상 있는 Player만 포함.
- `dbo.GetTopPlayerRankings(@Top)` 저장 프로시저 호출을 기본 구현으로 한다.
- 정렬: `wins DESC`, `kills DESC`, `playerId ASC`(모두 동률일 때 결정성 확보).

**성공 200**

```json
{
  "items": [
    {
      "rank": 1,
      "playerId": "11111111-1111-1111-1111-111111111111",
      "displayName": "PracticePlayerA",
      "matches": 5,
      "wins": 4,
      "kills": 37
    }
  ],
  "top": 20
}
```

`@Top`은 매개변수화하여 전달한다. 문자열 보간으로 SQL을 붙이지 않는다.

## 7. 헬스체크

- `GET /health/live`: 앱 실행 자체 확인(200/503), DB 의존성 없음.
- `GET /health/ready`: DB 접속 검증(200/503), 짧은 timeout; DB 주소, 상태, stack 등 상세 정보는 응답에 포함하지 않는다.

## 8. 상태 코드 표

| 상황 | 코드 | 참고 |
|---|---|---|
| 조회 성공 | 200 | 단건/컬렉션 |
| 신규 경기 생성 | 201 | `Location` 사용 |
| 멱등 재시도 | 200 | `created:false` |
| 유효성 실패 | 400 | 표준 ProblemDetails |
| 인증 실패 | 401 | Bearer challenge |
| 인가 실패 | 403 | `matches.write` 없음 |
| 리소스 없음 | 404 | 단일 Player/Match |
| 중복 외부 ID 내용 불일치 | 409 | `MATCH_ID_CONFLICT` |
| 서버/DB 장애 | 500/503 | 내부 정보 노출 금지 |

## 9. 계약 테스트 기준

각 API는 성공/경계/오류 사례를 HTTP integration에서 검증한다. `POST` 중복은 **동시 요청**도 실제 SQL Server에 대해 검증한다. JSON 필드 이름, null 처리, 정확한 상태 코드 및 정렬을 확인한다. 운영 기록/개인정보 대신 테스트용 임의 Player ID만 사용한다.

## 단계별 실행 범위 (P2 현재)

- P1의 Fake는 P2에서 테스트 프로젝트로 이동했다. 현재 실행 API는 EF SQL Server 저장소만 등록한다. 앱 기동 시 Migration/seed는 실행하지 않는다.
- 조회 Controller는 환경에 관계없이 제공하되 실제 DB 연결이 필요하다. JWT 구현 전 `POST /api/matches`는 Development에서만 허용하며 다른 환경에서는 404를 반환한다. OpenAPI는 Development 한정이다.
- POST 필드는 모두 필수다. UTC 오프셋 없는 시각, 숫자 enum/정확한 이름이 아닌 outcome, null 참가자, 누락 JSON 필드는 400이다. ASCII 검증은 대문자 변환 **이전**에 수행해 Unicode 문자가 ASCII로 접히는 경우를 차단한다.
- 참가자 정렬은 .NET Guid 순서, EF SQL Server의 경기 목록과 랭킹 UUID 정렬은 SQL Server uniqueidentifier 순서다. 각 경로는 일관된 2차 키를 사용한다.
- 실제 MSSQL 실행/제약/동시성/실행 계획은 P3에서 검증하기 전까지 미검증이다.
