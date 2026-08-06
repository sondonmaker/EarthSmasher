# 06. 리더보드 Anti-Cheat

클라이언트 단독 방어는 Cheat Engine·패킷 조작에 **무조건 뚫림**.  
**"클라이언트 = 연출, 서버 = 검증"** 구조가 핵심.

## 3단계 검증

### 1. 물리적 불가능 점수 필터 (서버)

점수만 보내면 1초 만에 999,999점 조작 가능.

```
클라이언트 → 서버 전송:
  userId, score, start_time, end_time, playTimeSec
```

서버 로직:

```
maxPossibleScore = playTimeSec × MAX_SCORE_PER_SECOND
if (score > maxPossibleScore) → REJECT
```

예: 초당 최대 100점 × 30초 = 3,000점 상한. 10,000점 제출 → 차단.

### 2. Action Log (리플레이) 검증

전체 리플레이 대신 **핵심 이벤트 로그**만 배열로 전송.

```json
[
  { "time": 1.2,  "event": "coin_eat",           "value": 10 },
  { "time": 3.5,  "event": "bull_market_boost",  "value": 0  },
  { "time": 8.1,  "event": "coin_eat",           "value": 10 },
  { "time": 12.0, "event": "doge_boss_dodge",    "value": 50 }
]
```

서버: 이벤트 합산 점수 == 제출 score, 이벤트 time <= playTimeSec.

### 3. HMAC-SHA256 서명

```csharp
// Unity (C#) — 클라이언트
string secretKey = "SNEK_SECRET_KEY_2026"; // 서버만 알고 IL2CPP로 난독화
string rawData = $"{userId}:{score}:{playTimeSec}";

using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
{
    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
    string signature = Convert.ToBase64String(hash);
    // HTTP Header: X-Signature: {signature}
}
```

서버: 동일 secretKey로 해시 재계산 → 불일치 시 REJECT.

## 클라이언트 메모리 변조 방지

| 방법 | 설명 |
|------|------|
| Obscured Types | `int score` 대신 XOR 암호화 변수 (Anti-Cheat Toolkit 등) |
| IL2CPP 빌드 | C# DLL 디컴파일 방지, secretKey 노출 최소화 |
| 코드 난독화 | 빌드 파이프라인에 obfuscator 적용 |

## 추천 백엔드 (직접 서버 구축 대안)

| 서비스 | 특징 |
|--------|------|
| **LootLocker** | Unity 연동 쉬움, 스코어 해시·플레이타임 검증 |
| **PlayFab** | Microsoft, 리더보드 + Cloud Script 검증 |

## 검증 플로우

```
[게임 시작] → start_time 기록
     ↓
[플레이 중] → actionLog[] append
     ↓
[게임 종료] → score, end_time, actionLog, HMAC signature 생성
     ↓
[서버] → ① 시간 상한 ② actionLog 합산 ③ HMAC 검증
     ↓
[PASS] → 리더보드 등록  /  [FAIL] → 거부 + 로그
```

## Discord/X 연동 (어뷰징 2차 방어)

- 리더보드 등록 시 X 또는 Discord OAuth 필수
- 1계정 1시즌 1순위 제한
- 수상 시 지갑 주소 + SNS 계정 대조
