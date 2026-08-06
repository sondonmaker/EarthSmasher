# 03. 에셋 준비 가이드

개발 시작 전 필요한 3D 모델·이미지·사운드를 수집·제작합니다.  
**처음부터 모델링하지 말고** Asset Store·Sketchfab 무료/저가 에셋 활용 → SNEK 시그니처 컬러(초록) 텍스처만 적용.

## 3D 모델

### 주인공 — SNEK

| 출처 | 검색 키워드 | 작업 |
|------|-------------|------|
| Unity Asset Store | `Low Poly Snake`, `Stylized Snake` | 초록 텍스처 적용 |
| Sketchfab / CGTrader | `low poly snake free` | 리깅 없어도 Spline 이동 가능 |

### 라이벌 보스 / 장애물

| 캐릭터 | 출처 | 검색 키워드 |
|--------|------|-------------|
| DOGE | Sketchfab | `Shiba Inu low poly` |
| PEPE | Sketchfab | `Pepe frog low poly` |
| SHIB | Sketchfab | `Shiba army`, `Shiba Inu` |
| Whale | Asset Store | `low poly whale`, `cartoon whale` |

### 아이템 / 환경

| 아이템 | 구현 방법 |
|--------|-----------|
| Green/Red Candle | 단순 3D 박스 + 캔들스틱 텍스처 |
| Rocket, Coin | Asset Store `low poly coin`, `rocket` |
| SNEK Energy 캔 | 실린더 + 라벨 텍스처 (직접 제작) |
| 차트 트랙 | Spline + 양봉/음봉 그리드 텍스처 |

## 2D 이미지 / UI

| 리소스 | 출처 | 용도 |
|--------|------|------|
| SNEK 공식 로고 | [snekcoinada](https://x.com/snekcoinada) / 공식 사이트 | 타이틀, 크레딧 |
| 뱀 머리 PNG | SNEK 공식 CI | 아이콘, UI |
| 차트 트랙 텍스처 | 직접 제작 (Photoshop/Figma) | 양봉 초록, 음봉 빨강 그리드 |
| 밈 팝업 PNG | 직접 제작 | RUG PULLED, REKT, LIQUIDATED, TO THE MOON |
| 폰트 | Impact, Comic Sans 계열 (무료 대체: Bangers, Permanent Marker) | 밈 텍스트 |

### 밈 팝업 텍스트 목록

```
RUG PULLED
REKT
LIQUIDATED
DOGE LIQUIDATED
PEPE REKT
TO THE MOON
DIAMOND HANDS!
Much Wow
Very Fast
```

## 사운드

| 종류 | 출처 | 파일 |
|------|------|------|
| BGM | Freesound.org, OpenGameArt | 8bit synthwave loop |
| 코인 획득 | Freesound | coin pickup sfx |
| 폭발 | Freesound | explosion sfx |
| DOGE 짖음 | Freesound | dog bark (코믹하게) |
| 뱀 이동 | Freesound | slither / whoosh |
| 보스 등장 | 직접 또는 Freesound | warning siren |

## Unity 패키지 (무료)

| 패키지 | 용도 |
|--------|------|
| Universal RP (URP) | 렌더 파이프라인 |
| Cinemachine | 3인칭 카메라 |
| Unity Splines / Path Creation | 차트 트랙 |
| LeanTween (또는 DOTween) | 트윈 애니메이션 |
| TextMeshPro | UI 텍스트 |

## 에셋 수집 체크리스트

```
[ ] SNEK 3D 모델 (Low Poly Snake)
[ ] DOGE 3D 모델
[ ] PEPE 3D 모델
[ ] SHIB 3D 모델 (선택)
[ ] Coin / Rocket / Candle 3D
[ ] SNEK 공식 로고 PNG
[ ] 차트 트랙 텍스처 (양봉/음봉)
[ ] 밈 팝업 PNG 세트
[ ] BGM 1곡
[ ] SFX 5종 이상
[ ] Cinemachine + Spline 패키지 설치
```

## 저작권 주의

- SNEK IP: **팬메이드** 명시, 상업적 사용 시 공식 팀 협의 권장
- DOGE/PEPE/SHIB: 패러디·비하적이지 않은 유머 수준 유지 (커뮤니티 밈 문화 범위)
- Asset Store 에셋: 라이선스 확인 (Commercial OK 여부)
- SNEK 공식 CI: 크레딧 및 Fan-Made 표기 필수
