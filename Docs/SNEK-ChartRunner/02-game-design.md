# 02. 게임 디자인

## 게임 루프

```
[메인 화면]
    ↓
[차트 라이딩 시작]
    ↓
[아이템 획득 & 꼬리 확대]
    ↓
[보스/장애물 회피 or 격퇴]
    ↓
[게임 오버 / 점수 청산]
    ↓
[리더보드 & X 공유]
```

## 조작 체계

| 입력 | 동작 |
|------|------|
| 좌우 드래그 | 차트 트랙 안에서 SNEK 좌우 이동 |
| 터치 홀드 (길게) | **Diamond Hands** 점프 — 차트 끊어진 구간·FUD 벽 통과 |
| 아래 드래그 | 슬라이딩 — 장애물 하단 통과 (2단계 이후) |
| Leverage 버튼 | **Short 포지션** — 하락장 구간에서 뱀 뒤집어 지하 차트 질주 (2단계 이후) |

## 코어 메카닉

### 1. 차트 트랙 (Chart Track)

- Spline 기반 3D 패스 — 양봉(초록)·음봉(빨강) 캔들스틱 도로
- Y축 높낮이 변화로 롤러코스터 입체감
- Cinemachine 3인칭 백뷰 카메라 추적

### 2. 꼬리 성장 & 리스크 관리 (1차 데모 핵심)

- 코인·양봉 아이템 획득 시 body segment 추가 → 꼬리 길어짐
- 꼬리가 길수록 회피 판정 불리, 자기 꼬리 충돌 위험 증가
- **Cash Out 게이트** (선택): 꼬리 일부 절단 → 안전 점수 환산 vs. 유지 → 배율(Multiplier) 상승

### 3. 불장 모드 (Bull Market / SNEK Energy)

- `SNEK Energy` 캔 아이템 획득 시 **5초간**:
  - 무적 + 속도 증가
  - Trail Renderer 화염/빛 잔상
  - Post-Processing (Bloom, Motion Blur)
  - Cinemachine FOV 확대
  - 장애물 충돌 시 Rigidbody 폭발 연출 + **"DOGE LIQUIDATED"**, **"PEPE REKT"** 팝업

### 4. 게임 오버 연출

- 벽·자기 몸 충돌 시 **"RUG PULLED"** 또는 **"REKT"** 텍스트
- Impact/Comic Sans 계열 밈 폰트

## 아이템 & 장애물

| 종류 | 이름 | 효과 |
|------|------|------|
| 수집 | Green Candle (양봉) | 점수 + 꼬리 1 segment |
| 수집 | ADA 로고 / 로켓 | 보너스 점수 |
| 수집 | SNEK Energy 캔 | 불장 모드 발동 |
| 장애물 | Red Candle (음봉) | 충돌 시 게임 오버 |
| 장애물 | FUD 텍스트 벽 | 회피 필요 |
| 장애물 | 차트 단절 구간 | Diamond Hands 점프 필요 |

## 라이벌 밈코인 보스 이벤트

커뮤니티 바이럴을 위한 핵심 콘텐츠. SNEK이 라이벌을 격퇴하는 카타르시스 연출.

### DOGE — "Bonk & Bark" (30초마다 등장)

- 거대 도지 머리가 뒤에서 등장
- **DOGE 뼈다귀** 폭탄 투하
- 밈 텍스트 장애물: "Much Wow", "Very Fast"
- 무적 상태 충돌 시 → **"DOGE LIQUIDATED"**

### PEPE — "FOG 보스"

- 페페가 초록 독가스(웁스 페페 눈물) 분사 → 시야 가림
- 미세 컨트롤로 안개 구간 통과
- 무적 상태 충돌 시 → **"PEPE REKT"**

### SHIB — "시바 군단 돌진"

- 작은 시바견 다수가 역주행으로 돌진
- 좌우 회피 + 타이밍 점프

### Whale 이벤트 (랜덤)

- 거대 고래 3D 모델 등장 → 차트 상하 흔들림
- 코인 폭풍(Airdrop) — 수집 vs. 회피 선택

## 2단계 이후 추가 메카닉 (우선순위)

| 순위 | 메카닉 | 설명 |
|------|--------|------|
| 1 | 꼬리 성장 시스템 | 구현 쉬움, 시각 만족도 큼 |
| 2 | Diamond Hands 점프 / 슬라이딩 | 손맛 증대 |
| 3 | Short 포지션 반전 모드 | SNEK 컨셉 극대화 |
| 4 | Whale 랜덤 이벤트 | 돌발 재미 |

## 사운드 & 비주얼

| 요소 | 방향 |
|------|------|
| BGM | 8비트 레트로 신스웨이브 |
| SFX | 코인 획득, 폭발, DOGE 짖음, 뱀 슉슉 |
| 스킨 | SNEK 홀더 PFP·의상/모자 (2단계) |
| UI | 점수, 밈 팝업, 리더보드, X 공유 버튼 |

## 3D 구현 핵심 (Unity)

| 요소 | 도구 |
|------|------|
| 트랙 | Path Creation / Unity Spline |
| 뱀 이동 | Spline Animate / LeanTween |
| 카메라 | Cinemachine (3인칭 백뷰) |
| 불장 VFX | Trail Renderer + URP Post-Processing |
| 물리 연출 | Rigidbody.AddExplosionForce |
