# 04. 개발 로드맵

## 1차 데모 목표 (10일)

플레이 타임 1분 이내, 15~30초 GIF 제작 가능한 수준의 데모.

### 1단계: 리소스 & 환경 (1~2일)

- [ ] Unity 3D 프로젝트 생성 (URP)
- [ ] Cinemachine, Unity Splines 설치
- [ ] Low Poly Snake 모델 다운로드 + 초록 텍스처
- [ ] DOGE / PEPE 3D 모델 수집
- [ ] SNEK 로고·밈 PNG 수집
- [ ] BGM·SFX 1차 수집

### 2단계: 프로토타입 (3~5일)

- [ ] Spline 기반 차트 도로 트랙 생성 (양봉/음봉 색상)
- [ ] SNEK 자동 전진 + 좌우 드래그 이동 스크립트
- [ ] Cinemachine 3인칭 백뷰 카메라
- [ ] 기본 충돌 판정 (벽, 장애물)

### 3단계: 코어 메카닉 (6~8일)

- [ ] 코인 획득 → 꼬리 segment 추가
- [ ] Red Candle / FUD 장애물 배치
- [ ] 게임 오버 → "RUG PULLED" / "REKT" 연출
- [ ] SNEK Energy 캔 → 5초 불장 모드
- [ ] DOGE 보스: 30초마다 뼈다귀 폭탄
- [ ] PEPE 보스: 독가스 시야 방해 (간단 버전)

### 4단계: 폴리싱 & 홍보 (9~10일)

- [ ] UI: 점수, 게임 오버, 재시작
- [ ] 밈 팝업 텍스트 애니메이션
- [ ] 15~30초 플레이 영상 녹화
- [ ] GIF 짤 생성
- [ ] SNEK 디스코드 `#collaborations` + X 게시

## 1차 데모 포함 / 제외 범위

| 포함 (MVP) | 제외 (2차 이후) |
|------------|-----------------|
| 차트 트랙 달리기 | Short 포지션 모드 |
| 좌우 이동 | Diamond Hands 점프 |
| 꼬리 성장 | Cash Out 게이트 |
| 불장 모드 | Whale 이벤트 |
| DOGE / PEPE 보스 (간단) | SHIB 군단 |
| 점수 UI | 리더보드 서버 |
| 게임 오버 연출 | 광고 / IAP |
| | 멀티플레이 |

## 2차: 리더보드 & 수익화 (데모 반응 후)

- [ ] LootLocker 또는 PlayFab 연동
- [ ] 주간 글로벌 리더보드
- [ ] X / Discord 계정 연동
- [ ] AdMob / Unity Ads (보상형 + 전면)
- [ ] 광고 제거 IAP ($2.99~$4.99)
- [ ] Anti-cheat 서버 검증 (→ [06-anti-cheat.md](06-anti-cheat.md))

## 기술 스택 요약

```
Unity 3D (URP)
├── Cinemachine          # 카메라
├── Unity Splines        # 차트 트랙
├── LeanTween/DOTween    # 애니메이션
├── TextMeshPro          # UI
├── AdMob / Unity Ads    # 광고 (2차)
├── Unity IAP            # 인앱 결제 (2차)
└── LootLocker/PlayFab   # 리더보드 (2차)
```

## 성공 기준 (1차 데모)

1. 15초 GIF만 봐도 "SNEK 차트 달리기"가 직관적으로 전달됨
2. DOGE/PEPE 격퇴 연출이 X 공유 욕구를 자극함
3. SNEK 디스코드/X에서 긍정 반응 (리트윗 or 댓글)
4. 재도전하고 싶은 1분 내 루프 완성
