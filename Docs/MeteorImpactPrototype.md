# 운석 낙하 프로토타입 (Impact vs Destructible)

컨셉: 생생한 지구 → 운석 낙하 → 임팩트 플래시 → 데미지%/지각 파편

참고 이미지: `Docs/concept_impact_vs_destructible.png`

## Unity에서 여는 방법

1. **Unity Hub** → New Project → **3D (Built-in Render Pipeline)**  
   - 위치를 `C:\Users\sunghwan\Documents\GitHub\EarthSmasher` 로 지정하거나  
   - 새 프로젝트 생성 후 이 폴더의 `Assets` 내용을 합치기
2. 빈 씬 저장: `Assets/Scenes/MeteorImpact.unity`
3. Hierarchy에 빈 GameObject → `MeteorImpactBootstrap` 추가
4. **Play**

자동으로 로드되는 것:
- `Resources/Earth` 고해상도 day/night/clouds 텍스처
- 별밭 배경, 궤도 카메라, HUD
- 운석 트레일 / 충격파 / 크레이터 / 지각 fracture

## 조작

| 입력 | 동작 |
|------|------|
| 지구 **탭/클릭** | 운석 낙하 |
| 드래그 | 카메라 궤도 |
| 휠 / 핀치 | 줌 |

## 추가된 시스템

| 스크립트 | 역할 |
|----------|------|
| `EarthTextureLoader` | Resources 텍스처 → 머티리얼 |
| `EarthFractureSystem` | 표면 조각을 임팩트 시 물리로 뜯어냄 |
| `MeteorTrail` | 화염 트레일 |
| `ImpactShockwave` | 충격파 링 |
| `ImpactCrater` | 그을린 크레이터 |
| `StarfieldBackdrop` | 우주 배경 |

## 다음

- Blender Voronoi fracture 메시로 스왑
- Particle System 기반 연기/불꽃
- 모바일 조준 링 UI
