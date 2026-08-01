# 지구뿌수기 (EarthSmasher)

Unity 모바일 — 생생한 지구에 운석을 떨어뜨려 부수는 게임.

## 지금 Play로 되는 것

1. Unity Hub에서 **3D (Built-in)** 로 이 폴더 열기
2. 빈 씬에 `MeteorImpactBootstrap` 붙이기
3. Play → 지구 탭

포함: 2K 지구 텍스처, 구름, 야간광, 운석 트레일, 충격파, 크레이터, 지각 파괴, HUD

세팅 상세: [Docs/MeteorImpactPrototype.md](Docs/MeteorImpactPrototype.md)

## 구조

```
Assets/
  Resources/Earth/   # day / night / clouds
  Scripts/
    Core/            # Bootstrap
    Gameplay/        # Earth, Meteor, Fracture, VFX
    UI/              # ImpactHud
Docs/
```

## 상태

- [x] GitHub 레포
- [x] 운석 낙하 프로토타입
- [x] 고해상도 지구 텍스처
- [x] Fracture + 임팩트 VFX
- [ ] Unity Hub로 씬 저장 / 모바일 빌드
- [ ] 본격 Destructible Mesh 에셋
