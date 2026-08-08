# Google Play 빌드 가이드 — Earth Smasher

| 항목 | 값 |
|------|-----|
| 패키지명 | `com.sunsoft.earthsmasher` |
| 업로드 파일 | **AAB** (`EarthSmasher.aab`) |
| 출력 경로 | `Build/Android/Release/EarthSmasher.aab` |

## 1. Unity에서 빌드 (에디터)

1. **File → Build Settings → Android** (Switch Platform)
2. 메뉴 **Build → Google Play AAB (Release)**
3. 생성된 AAB를 Play Console에 업로드

## 2. 스크립트로 빌드 (권장)

```powershell
cd C:\Users\sunghwan\EarthCrack

# (최초 1회) 업로드 키 생성
.\Build\android\create-upload-keystore.ps1

# keystore 설정
copy Build\android\play-keystore.properties.example Build\android\play-keystore.properties
# play-keystore.properties 편집

# AAB 빌드
.\Build\android\build-play-aab.ps1
```

로컬 테스트 APK:

```powershell
.\Build\android\build-debug-apk.ps1
```

## 3. Play Console 업로드

1. [Google Play Console](https://play.google.com/console) → **Earth Smasher**
2. **Testing → Internal testing** (또는 Production)
3. **Create new release** → **Upload** → `Build/Android/Release/EarthSmasher.aab`
4. **Version code**는 매 업로드마다 증가 (Unity: Player Settings → Version → `Bundle Version Code`)

### "Upload a valid app bundle" 오류

| 원인 | 해결 |
|------|------|
| **APK를 올림** | `EarthSmasher.apk` ❌ — **`.aab`만** 업로드 |
| APK 파일명을 `.aab`로만 변경 | Play가 ZIP 구조 검사 → 거부. Unity로 **AAB 재빌드** |
| 예전 빌드 (`Build/Android/EarthSmasher.apk`) | 패키지 `com.sondonmaker...` — Play 등록 `com.sunsoft.earthsmasher` 와 불일치 |
| AAB 없음 | `.\Build\android\build-play-aab.ps1` 실행 |

검증:

```powershell
.\Build\android\validate-aab.ps1
```

올릴 파일 경로 (이것만):

```
C:\Users\sunghwan\EarthCrack\Build\Android\Release\EarthSmasher.aab
```

## 4. 서명 (Play App Signing)

- Google **Play App Signing** 사용 권장
- `create-upload-keystore.ps1`로 만든 키 = **Upload key**
- `play-keystore.properties`는 Git에 커밋하지 마세요

## 5. 버전 올리기

Unity **Edit → Project Settings → Player → Android**:

- **Version** (표시): 예) `0.1` → `0.2`
- **Bundle Version Code** (정수): 예) `1` → `2` (매 업로드 +1 필수)
