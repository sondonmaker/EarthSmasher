using System.Collections;
using UnityEngine;

public enum PlanetLaserKind
{
    Fire,      // 1 화염
    Ice,       // 2 얼음
    Pierce,    // 3 관통 (반대쪽까지)
    Plasma,    // 4 플라즈마
    Lightning, // 5 번개
    Sustain    // 6 누르고 있는 동안 연속
}

/// <summary>5번 메뉴: 레이저. 클릭 지점으로 발사.</summary>
public class LaserStrikeSystem : MonoBehaviour
{
    public static LaserStrikeSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;
    [SerializeField] Camera cam;

    bool sustainActive;
    GameObject sustainVfxBeam;
    GameObject sustainPrimBeam;
    GameObject sustainPrimGlow;
    GameObject sustainImpact;
    float sustainShakeTimer;
    float sustainBurnPulse;
    float sustainPaintTimer;
    float sustainCasualtyTimer;
    int sustainVisualTick;
    Vector3 sustainPoint;
    Vector3 sustainNormal;
    Vector3 sustainPrevPoint;
    bool sustainHasPrev;

    const float SustainMoveAngleDeg = 0.32f;
    const float SustainPaintInterval = 0.11f;
    const float SustainCasualtyInterval = 0.45f;
    const float SustainShakeInterval = 0.35f;

    public bool IsSustaining => sustainActive;

    public static LaserStrikeSystem Ensure()
    {
        var s = FindObjectOfType<LaserStrikeSystem>();
        if (s != null)
            return s;
        return new GameObject("LaserStrikeSystem").AddComponent<LaserStrikeSystem>();
    }

    void Awake()
    {
        Instance = this;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (cam == null)
            cam = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Abort()
    {
        EndSustain();
        StopAllCoroutines();
    }

    public void BeginSustain(Vector3 worldPoint, Vector3 normal, Vector2 screenPos)
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return;
        if (cam == null)
            cam = Camera.main;

        if (sustainActive)
        {
            UpdateSustain(worldPoint, normal, screenPos);
            return;
        }

        sustainActive = true;
        sustainShakeTimer = 0f;
        sustainBurnPulse = 0f;
        sustainPaintTimer = 0f;
        sustainCasualtyTimer = 0f;
        sustainVisualTick = 0;
        sustainHasPrev = false;
        ApplySustainTarget(worldPoint, normal, true);
    }

    public void UpdateSustain(Vector3 worldPoint, Vector3 normal, Vector2 screenPos)
    {
        if (!sustainActive)
            return;
        ApplySustainTarget(worldPoint, normal, false);
    }

    public void UpdateSustain(Vector3 worldPoint, Vector3 normal)
    {
        UpdateSustain(worldPoint, normal, Vector2.zero);
    }

    public void EndSustain()
    {
        if (!sustainActive)
            return;

        sustainActive = false;
        CleanupSustainVisuals();

        if (earth != null)
        {
            ApplySustainBurnAt(sustainPoint, 0.022f, 1f);
            EarthSurfaceScorch.Ensure(earth)?.FlushTexture();
            earth.ApplyImpact(sustainPoint, 1.2f);
            PopulationCasualtySystem.ApplyAt(
                earth,
                sustainPoint,
                PopulationCasualtySystem.ScorchNormToDegrees(0.04f),
                0.35f,
                0.75f);
            CameraShake.Shake(0.07f, 0.1f);
        }

        sustainHasPrev = false;
    }

    Vector3 SnapSurfaceHit(Vector3 worldPoint, Vector3 normal)
    {
        Vector3 center = earth.transform.position;
        Vector3 radial = (worldPoint - center).normalized;
        if (radial.sqrMagnitude < 1e-6f)
            radial = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
        return center + radial * earth.Radius;
    }

    void ApplySustainBurnAt(Vector3 worldPoint, float radiusNorm, float heat)
    {
        EarthSurfaceScorch.Ensure(earth)?.PaintSustainBurnAt(worldPoint, radiusNorm, heat);
    }

    void ApplySustainBurnTrail(Vector3 fromWorld, Vector3 toWorld, bool stationaryPulse)
    {
        var scorch = EarthSurfaceScorch.Ensure(earth);
        if (scorch == null)
            return;

        const float radiusNorm = 0.017f;
        const float heat = 0.9f;

        if (!sustainHasPrev || stationaryPulse)
            scorch.PaintSustainBurnAt(toWorld, radiusNorm, heat);
        else
            scorch.PaintSustainBurnSegment(fromWorld, toWorld, radiusNorm, heat);

        sustainPrevPoint = toWorld;
        sustainHasPrev = true;
    }

    void ApplySustainTarget(Vector3 worldPoint, Vector3 normal, bool firstFrame)
    {
        Vector3 prevPoint = sustainPoint;
        bool hadPrev = sustainHasPrev;

        sustainPoint = SnapSurfaceHit(worldPoint, normal);
        sustainNormal = (sustainPoint - earth.transform.position).normalized;
        normal = sustainNormal;

        Vector3 origin = BeamOrigin(sustainPoint, normal);
        float vfxScale = earth.Radius * 0.15f;
        Color coreColor = new Color(0.92f, 1f, 0.78f);
        Color glowColor = new Color(0.15f, 1f, 0.38f);
        sustainVisualTick++;
        float pulse = 1f + (sustainVisualTick % 4 == 0 ? 0.08f * Mathf.Sin(Time.time * 36f) : 0f);

        if (firstFrame)
        {
            sustainVfxBeam = SpawnKindBeam(PlanetLaserKind.Sustain, origin, sustainPoint, vfxScale, -1f);
            if (sustainVfxBeam == null)
            {
                sustainPrimBeam = MakeBeam("SustainLaserCore", origin, sustainPoint, coreColor, 0.034f * pulse, 9f);
                sustainPrimGlow = MakeBeam("SustainLaserGlow", origin, sustainPoint, glowColor, 0.09f * pulse, 3.5f, true);
            }
            CameraShake.Shake(0.05f, 0.08f);
            ApplySustainBurnAt(sustainPoint, 0.018f, 0.96f);
            sustainPrevPoint = sustainPoint;
            sustainHasPrev = true;
        }
        else
        {
            if (sustainVisualTick % 2 == 0)
            {
                if (sustainVfxBeam != null)
                    AlignVfxBeam(sustainVfxBeam, origin, sustainPoint, vfxScale * pulse);
                if (sustainPrimBeam != null)
                    AlignBeam(sustainPrimBeam.transform, origin, sustainPoint, 0.034f * pulse);
                if (sustainPrimGlow != null)
                    AlignBeam(sustainPrimGlow.transform, origin, sustainPoint, 0.09f * pulse);
            }
        }

        Vector3 center = earth.transform.position;
        float moveAngle = hadPrev
            ? Vector3.Angle((prevPoint - center).normalized, sustainNormal)
            : 999f;
        bool moved = moveAngle >= SustainMoveAngleDeg;

        sustainPaintTimer += Time.deltaTime;
        if (!firstFrame && sustainPaintTimer >= SustainPaintInterval && (moved || sustainBurnPulse >= SustainPaintInterval))
        {
            sustainPaintTimer = 0f;
            sustainBurnPulse = 0f;
            ApplySustainBurnTrail(prevPoint, sustainPoint, !moved);
        }
        else
        {
            sustainBurnPulse += Time.deltaTime;
        }

        if (moved && !firstFrame)
        {
            sustainCasualtyTimer += Time.deltaTime;
            if (sustainCasualtyTimer >= SustainCasualtyInterval)
            {
                sustainCasualtyTimer = 0f;
                PopulationCasualtySystem.ApplyAt(
                    earth,
                    sustainPoint,
                    PopulationCasualtySystem.ScorchNormToDegrees(0.018f),
                    0.12f,
                    0.18f);
            }
        }

        sustainShakeTimer += Time.deltaTime;
        if (sustainShakeTimer >= SustainShakeInterval)
        {
            sustainShakeTimer = 0f;
            CameraShake.Shake(moved ? 0.018f : 0.01f, moved ? 0.028f : 0.015f);
        }
    }

    void CleanupSustainVisuals()
    {
        if (sustainVfxBeam != null)
            Destroy(sustainVfxBeam);
        if (sustainPrimBeam != null)
            Destroy(sustainPrimBeam);
        if (sustainPrimGlow != null)
            Destroy(sustainPrimGlow);
        if (sustainImpact != null)
            Destroy(sustainImpact);
        sustainVfxBeam = null;
        sustainPrimBeam = null;
        sustainPrimGlow = null;
        sustainImpact = null;
    }

    public void FireAt(PlanetLaserKind kind, Vector3 worldPoint, Vector3 normal)
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return;
        if (cam == null)
            cam = Camera.main;

        normal = normal.normalized;
        Vector3 point = earth.transform.position + normal * earth.Radius;

        switch (kind)
        {
            case PlanetLaserKind.Fire:
                StartCoroutine(FireBeam(point, normal, new Color(1f, 0.35f, 0.05f), 0.09f, 1.6f, true, PlanetLaserKind.Fire));
                break;
            case PlanetLaserKind.Ice:
                StartCoroutine(IceBeam(point, normal));
                break;
            case PlanetLaserKind.Pierce:
                StartCoroutine(PierceBeam(point, normal));
                break;
            case PlanetLaserKind.Plasma:
                StartCoroutine(FireBeam(point, normal, new Color(0.85f, 0.25f, 1f), 0.11f, 1.9f, true, PlanetLaserKind.Plasma));
                break;
            case PlanetLaserKind.Lightning:
                StartCoroutine(LightningBurst(point, normal));
                break;
        }
    }

    Vector3 BeamOrigin(Vector3 target, Vector3 normal)
    {
        if (cam != null)
        {
            Vector3 fromCam = cam.transform.position;
            // 카메라에서 타겟 쪽으로, 지구 바깥에서 시작
            Vector3 dir = (target - fromCam).normalized;
            return target - dir * (earth.Radius * 2.8f);
        }
        return target + normal * (earth.Radius * 2.5f);
    }

    IEnumerator FireBeam(Vector3 point, Vector3 normal, Color color, float width, float hold, bool dig, PlanetLaserKind kind = PlanetLaserKind.Fire)
    {
        Vector3 origin = BeamOrigin(point, normal);
        float vfxScale = earth.Radius * 0.18f;
        GameObject vfxBeam = SpawnKindBeam(kind, origin, point, vfxScale, hold + 0.35f);
        GameObject vfxImpact = SpawnKindImpact(kind, point, normal, vfxScale * 0.85f, hold + 1.2f);

        bool usePrimitive = vfxBeam == null;
        GameObject beam = usePrimitive ? MakeBeam("FireLaser", origin, point, color, width) : null;
        CameraShake.Shake(0.08f, 0.12f);

        float t = 0f;
        float digTimer = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            digTimer += Time.deltaTime;
            if (usePrimitive)
                AlignBeam(beam.transform, origin, point, width * (0.85f + 0.15f * Mathf.Sin(t * 40f)));
            else
                AlignVfxBeam(vfxBeam, origin, point, vfxScale);

            if (dig && digTimer > 0.12f)
            {
                digTimer = 0f;
                var deform = EarthCraterDeform.Ensure(earth);
                int hits = deform != null ? deform.DrillBore(point, 0.1f, 0.06f, 0.3f) : 0;
                var scorch = EarthSurfaceScorch.Ensure(earth);
                scorch?.BurnAt(point, 0.04f, color.r > 0.7f ? 0.85f : 0.55f);
                if (hits > 0 && (hits % 3 == 0 || hits >= 8))
                    ImpactCrater.ApplyDigVisuals(earth, point, 0.038f + hits * 0.002f, hits, point.GetHashCode());
                PopulationCasualtySystem.ApplyAt(
                    earth,
                    point,
                    PopulationCasualtySystem.ScorchNormToDegrees(0.045f),
                    0.42f,
                    0.95f);
            }
            yield return null;
        }

        // 마무리 강한 한 방
        if (dig)
        {
            var deform = EarthCraterDeform.Ensure(earth);
            int hits = deform != null ? deform.DrillBore(point, 0.14f, 0.1f, 0.26f) : 0;
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, 0.06f, 0.9f);
            if (hits > 0)
                ImpactCrater.ApplyDigVisuals(earth, point, 0.07f, hits, point.GetHashCode() ^ 17);
        }
        PopulationCasualtySystem.ApplyAt(
            earth,
            point,
            PopulationCasualtySystem.ScorchNormToDegrees(0.09f),
            0.68f,
            1.25f);
        CameraShake.Shake(0.1f, 0.15f);
        if (beam != null)
            Destroy(beam);
        if (vfxImpact == null)
            SpawnKindImpact(kind, point, normal, vfxScale, 1.5f);
    }

    IEnumerator IceBeam(Vector3 point, Vector3 normal)
    {
        Vector3 origin = BeamOrigin(point, normal);
        Color ice = new Color(0.45f, 0.85f, 1f);
        float vfxScale = earth.Radius * 0.16f;
        GameObject vfxBeam = SpawnKindBeam(PlanetLaserKind.Ice, origin, point, vfxScale, 2.2f);
        GameObject vfxImpact = SpawnKindImpact(PlanetLaserKind.Ice, point, normal, vfxScale * 0.9f, 2.8f);

        bool usePrimitive = vfxBeam == null;
        GameObject beam = usePrimitive ? MakeBeam("IceLaser", origin, point, ice, 0.08f) : null;
        CameraShake.Shake(0.05f, 0.1f);

        // 서리 덮개 (VFX 없을 때만 구체 표시)
        GameObject frost = null;
        if (vfxImpact == null)
        {
            frost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(frost.GetComponent<Collider>());
            frost.name = "FrostPatch";
            frost.transform.position = point + normal * (earth.Radius * 0.01f);
            frost.transform.localScale = Vector3.one * (earth.Radius * 0.12f);
            frost.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(new Color(0.75f, 0.95f, 1f, 0.55f));
        }

        float t = 0f;
        while (t < 1.8f)
        {
            t += Time.deltaTime;
            if (usePrimitive)
                AlignBeam(beam.transform, origin, point, 0.08f);
            else
                AlignVfxBeam(vfxBeam, origin, point, vfxScale);

            if (frost != null)
            {
                float grow = Mathf.Lerp(0.08f, 0.2f, t / 1.8f) * earth.Radius;
                frost.transform.localScale = Vector3.one * grow;
            }

            if (Time.frameCount % 5 == 0)
            {
                // 얕게만 — 얼음은 녹아내리듯 표면 자국
                EarthCraterDeform.Ensure(earth)?.DrillBore(point, 0.08f, 0.025f, 0.34f);
                EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, 0.05f, 0.25f); // 밝게 변색 느낌
            }
            yield return null;
        }

        if (beam != null)
            Destroy(beam);
        if (frost != null)
            Destroy(frost, 2.5f);

        PopulationCasualtySystem.ApplyAt(
            earth,
            point,
            PopulationCasualtySystem.ScorchNormToDegrees(0.05f),
            0.06f,
            0.7f);
    }

    IEnumerator PierceBeam(Vector3 point, Vector3 normal)
    {
        Vector3 center = earth.transform.position;
        Vector3 antipode = center - normal * earth.Radius;
        Vector3 origin = point + normal * (earth.Radius * 4f);
        Vector3 exitBeyond = antipode - normal * (earth.Radius * 1.5f);

        float vfxScale = earth.Radius * 0.22f;
        GameObject vfxBeam = SpawnKindBeam(PlanetLaserKind.Pierce, origin, exitBeyond, vfxScale, 3.2f);
        GameObject entryImpact = SpawnKindImpact(PlanetLaserKind.Pierce, point, normal, vfxScale * 0.75f, 2.5f);
        GameObject exitImpact = SpawnKindImpact(PlanetLaserKind.Pierce, antipode, -normal, vfxScale * 0.65f, 2.5f);

        // 레퍼런스처럼 푸른 레이저 (코어/글로우/아우터) — VFX 없을 때 폴백
        float holeR = earth.Radius * 0.16f;
        bool usePrimitive = vfxBeam == null;
        GameObject core = usePrimitive ? MakeBeam("PierceBeamCore", origin, exitBeyond, new Color(0.85f, 0.98f, 1f), holeR * 0.55f, 8f) : null;
        GameObject glow = usePrimitive ? MakeBeam("PierceBeamGlow", origin, exitBeyond, new Color(0.15f, 0.55f, 1f), holeR * 1.05f, 5f) : null;
        GameObject outer = usePrimitive ? MakeBeam("PierceBeamOuter", origin, exitBeyond, new Color(0.2f, 0.45f, 1f), holeR * 1.45f, 2.2f, true) : null;

        CameraShake.Shake(0.25f, 0.4f);

        // 즉시 깔끔한 원통 관통 + 가장자리 용암(셰이더)
        EarthPierceHole.Ensure(earth)?.AddPierce(point, antipode, holeR);

        PopulationCasualtySystem.ApplyAt(
            earth,
            point,
            PopulationCasualtySystem.DigNormToDegrees(0.16f),
            0.48f,
            1.15f);
        PopulationCasualtySystem.ApplyAt(
            earth,
            antipode,
            PopulationCasualtySystem.DigNormToDegrees(0.1f),
            0.32f,
            0.85f);

        float t = 0f;
        const float hold = 2.2f;
        while (t < hold)
        {
            t += Time.deltaTime;
            float pulse = 1f + 0.06f * Mathf.Sin(t * 28f);
            if (usePrimitive)
            {
                AlignBeam(core.transform, origin, exitBeyond, holeR * 0.55f * pulse);
                AlignBeam(glow.transform, origin, exitBeyond, holeR * 1.05f * pulse);
                AlignBeam(outer.transform, origin, exitBeyond, holeR * 1.45f);
            }
            else
            {
                AlignVfxBeam(vfxBeam, origin, exitBeyond, vfxScale * pulse);
            }

            // 스파크
            if (Time.frameCount % 2 == 0)
            {
                Vector3 sparkPos = Vector3.Lerp(origin, exitBeyond, Random.value);
                SpawnSparks(sparkPos, holeR * 0.4f);
            }

            yield return null;
        }

        // 짧게 남았다가 페이드
        float fade = 0.7f;
        t = 0f;
        while (t < fade && usePrimitive)
        {
            t += Time.deltaTime;
            float a = 1f - t / fade;
            SetBeamAlpha(core, a);
            SetBeamAlpha(glow, a * 0.8f);
            SetBeamAlpha(outer, a * 0.5f);
            yield return null;
        }

        if (core != null) Destroy(core);
        if (glow != null) Destroy(glow);
        if (outer != null) Destroy(outer);
        if (entryImpact == null && exitImpact == null)
        {
            SpawnKindImpact(PlanetLaserKind.Pierce, point, normal, vfxScale * 0.7f, 1.8f);
            SpawnKindImpact(PlanetLaserKind.Pierce, antipode, -normal, vfxScale * 0.6f, 1.8f);
        }
        EarthPierceHole.Ensure(earth)?.ReapplyShader();
        CameraShake.Shake(0.12f, 0.2f);
    }

    void SpawnSparks(Vector3 pos, float size)
    {
        var sparks = LaserVfxSpawner.SparksPrefab();
        if (sparks != null)
        {
            LaserVfxSpawner.SpawnImpact(sparks, pos, Random.onUnitSphere, size * 0.08f, 0.35f);
            return;
        }
        SpawnBlueSpark(pos, size);
    }

    void SpawnBlueSpark(Vector3 pos, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "PierceSpark";
        go.transform.position = pos + Random.insideUnitSphere * size;
        go.transform.localScale = Vector3.one * (size * Random.Range(0.08f, 0.2f));
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.4f, 0.8f, 1f), 6f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go, 0.18f);
    }

    static void SetBeamAlpha(GameObject beam, float a)
    {
        if (beam == null)
            return;
        var rend = beam.GetComponent<Renderer>();
        if (rend == null || rend.material == null)
            return;
        var c = rend.material.color;
        c.a = Mathf.Clamp01(a);
        if (rend.material.HasProperty("_Color"))
            rend.material.SetColor("_Color", c);
        else
            rend.material.color = c;
    }

    IEnumerator LightningBurst(Vector3 point, Vector3 normal)
    {
        CameraShake.Shake(0.12f, 0.2f);
        Vector3 origin = BeamOrigin(point, normal);
        float vfxScale = earth.Radius * 0.14f;

        for (int bolt = 0; bolt < 7; bolt++)
        {
            Vector3 jitter = (normal + Random.insideUnitSphere * 0.35f).normalized;
            Vector3 hit = earth.transform.position + jitter * earth.Radius;
            Vector3 boltOrigin = origin + Random.insideUnitSphere * (earth.Radius * 0.3f);

            GameObject vfx = SpawnKindBeam(PlanetLaserKind.Lightning, boltOrigin, hit, vfxScale * 0.85f, 0.25f);
            if (vfx == null)
            {
                var beam = MakeBeam("Lightning", boltOrigin, hit, new Color(1f, 0.95f, 0.4f), 0.035f);
                Destroy(beam, 0.12f);
            }
            SpawnKindImpact(PlanetLaserKind.Lightning, hit, (hit - earth.transform.position).normalized, vfxScale * 0.7f, 0.45f);

            EarthCraterDeform.Ensure(earth)?.DrillBore(hit, 0.06f, 0.04f, 0.32f);
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, 0.025f, 0.6f);
            PopulationCasualtySystem.ApplyAt(
                earth,
                hit,
                PopulationCasualtySystem.ScorchNormToDegrees(0.025f),
                0.18f,
                0.55f);
            yield return new WaitForSecondsRealtime(0.05f);
        }

        // 중심 타격
        GameObject mainVfx = SpawnKindBeam(PlanetLaserKind.Lightning, origin, point, vfxScale, 0.45f);
        if (mainVfx == null)
        {
            var main = MakeBeam("LightningMain", origin, point, new Color(1f, 1f, 0.7f), 0.07f);
            Destroy(main, 0.25f);
        }
        SpawnKindImpact(PlanetLaserKind.Lightning, point, normal, vfxScale * 1.1f, 1.2f);

        EarthCraterDeform.Ensure(earth)?.DrillBore(point, 0.12f, 0.08f, 0.28f);
        PopulationCasualtySystem.ApplyAt(
            earth,
            point,
            PopulationCasualtySystem.ScorchNormToDegrees(0.04f),
            0.38f,
            1f);
        yield return new WaitForSecondsRealtime(0.25f);
    }

    static GameObject SpawnKindBeam(PlanetLaserKind kind, Vector3 from, Vector3 to, float scale, float lifetime)
    {
        var prefab = LaserVfxSpawner.ForKind(kind, impact: false);
        return LaserVfxSpawner.SpawnBeam(prefab, from, to, scale, lifetime);
    }

    static GameObject SpawnKindImpact(PlanetLaserKind kind, Vector3 point, Vector3 normal, float scale, float lifetime)
    {
        var prefab = LaserVfxSpawner.ForKind(kind, impact: true);
        return LaserVfxSpawner.SpawnImpact(prefab, point, normal, scale, lifetime);
    }

    static void AlignVfxBeam(GameObject vfx, Vector3 from, Vector3 to, float scale)
    {
        if (vfx == null)
            return;
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 1e-6f)
            return;
        vfx.transform.position = from;
        vfx.transform.rotation = Quaternion.LookRotation(dir.normalized);
        vfx.transform.localScale = Vector3.one * Mathf.Max(0.25f, scale);
    }

    static GameObject MakeBeam(string name, Vector3 from, Vector3 to, Color color, float width, float emission = 4f, bool soft = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = name;
        var rend = go.GetComponent<Renderer>();
        if (soft)
            rend.material = RuntimeMaterial.UnlitTransparent(new Color(color.r, color.g, color.b, 0.35f));
        else
            rend.material = RuntimeMaterial.Opaque(color, emission);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AlignBeam(go.transform, from, to, width);
        return go;
    }

    static void AlignBeam(Transform beam, Vector3 from, Vector3 to, float width)
    {
        if (beam == null)
            return;
        Vector3 mid = (from + to) * 0.5f;
        float len = Vector3.Distance(from, to) * 0.5f;
        beam.position = mid;
        beam.up = (to - from).normalized;
        beam.localScale = new Vector3(width, Mathf.Max(0.01f, len), width);
    }
}
