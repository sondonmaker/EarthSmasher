using System.Collections;
using UnityEngine;

/// <summary>올림포스 신들 — 클릭 지점 즉시 타격 (카메라 이동·시간차 연출 없음).</summary>
public class GreekMythAttackSystem : MonoBehaviour
{
    public static GreekMythAttackSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;

    public bool IsBusy => false;

    void Awake()
    {
        Instance = this;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static GreekMythAttackSystem Ensure()
    {
        if (Instance != null)
            return Instance;
        return new GameObject("GreekMythAttackSystem").AddComponent<GreekMythAttackSystem>();
    }

    public void Configure(EarthPlanet planet) => earth = planet;

    public void Abort() { }

    public bool TryFire(string godId, Vector3 worldPoint, Vector3 worldNormal)
    {
        if (string.IsNullOrEmpty(godId))
            return false;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;

        Vector3 localDir = earth.transform.InverseTransformPoint(worldPoint).normalized;

        switch (godId)
        {
            case "zeus":
                ZeusThunder(localDir);
                return true;
            case "poseidon":
                PoseidonTsunami(localDir);
                return true;
            case "hades":
                HadesPlague(localDir);
                return true;
            case "apollo":
                ApolloSunScourge(localDir);
                return true;
            case "ares":
                AresWarRain(localDir);
                return true;
            case "hephaestus":
                HephaestusForge(localDir);
                return true;
            default:
                return false;
        }
    }

    void ZeusThunder(Vector3 localDir)
    {
        float R = earth.Radius;
        Vector3 n = DirWorld(localDir);
        Vector3 hit = Point(localDir, R);

        // 전기 소용돌이 (구 Vortex 연출)
        SpawnElectricVortexBurst(hit, n, R, 1f);
        for (int i = 0; i < 5; i++)
        {
            Vector3 jitter = (localDir + Random.insideUnitSphere * 0.16f).normalized;
            Vector3 jHit = Point(jitter, R);
            Vector3 jN = DirWorld(jitter);
            ProFxParticleSpawner.SpawnCosmicVortexImpact(jHit, jN, R * (i == 4 ? 0.88f : 0.68f));
            if (i == 0 || i == 4)
                SpawnElectricVortexBurst(jHit, jN, R, 0.62f);
        }

        EarthCraterDeform.Ensure(earth)?.DrillBore(hit, 0.12f, 0.07f, 0.28f);
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, 0.045f, 0.6f);
        MemeAttackSystem.ApplyCasualtiesAt(earth, hit, 0.09f, 0.88f, 1.2f);
        CameraShake.Shake(0.12f, 0.16f);
    }

    void SpawnElectricVortexBurst(Vector3 hit, Vector3 normal, float earthRadius, float scaleMul)
    {
        if (ProFxParticleSpawner.SpawnCosmicVortex(hit, normal, earthRadius * scaleMul) != null)
        {
            ProFxParticleSpawner.SpawnCosmicVortexImpact(hit, normal, earthRadius * scaleMul);
            return;
        }

        SpawnElectricVortexFallback(hit, normal, earthRadius * 0.22f * scaleMul);
    }

    static void SpawnElectricVortexFallback(Vector3 hit, Vector3 normal, float size)
    {
        var root = new GameObject("ZeusVortex");
        root.transform.position = hit + normal * 0.12f;
        root.transform.rotation = Quaternion.LookRotation(normal);
        for (int i = 0; i < 3; i++)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float r = size * (0.55f + i * 0.35f);
            ring.transform.localScale = new Vector3(r, 0.012f, r);
            ring.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(
                new Color(1f, 0.92f, 0.42f, 0.4f - i * 0.1f));
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Object.Destroy(root, 1.6f);
    }

    void PoseidonTsunami(Vector3 localDir)
    {
        float R = earth.Radius;
        Vector3 n = DirWorld(localDir);
        Vector3 hit = Point(localDir, R);

        EarthGeo.DirectionToLatLon(localDir, out float lat, out float lon);
        bool ocean = PopulationCasualtySystem.IsOcean(lat, lon);
        float maxRing = ocean ? 0.34f : 0.22f;

        Vector3 sky = hit + n * (R * 2.5f);
        SpawnIceTrident(sky, hit, n, R);

        ProFxParticleSpawner.SpawnWorldFromPath(
            "Smokes/ppfxFlareSmokeBlue",
            hit,
            n,
            R * 0.052f,
            2.6f);
        ProFxParticleSpawner.SpawnWorldFromPath(
            "Orbs/ppfxOrbBlueTrail",
            hit + n * (R * 0.04f),
            n,
            R * 0.042f,
            2.8f,
            loop: true);
        ProFxParticleSpawner.SpawnWorldFromPath(
            "Smokes/ppfxSmokeTurbulence02",
            hit,
            n,
            R * 0.048f,
            2.2f);

        var rippleDriver = new GameObject("PoseidonRipples");
        rippleDriver.AddComponent<PoseidonRipplePulse>().Play(hit, n, R, maxRing, ocean ? 7 : 5);

        var deform = EarthCraterDeform.Ensure(earth);
        deform?.CarveHole(hit, ocean ? 0.2f : 0.12f, ocean ? 0.045f : 0.028f);

        MemeAttackSystem.ApplyCasualtiesAt(earth, hit, ocean ? 0.18f : 0.11f, ocean ? 0.72f : 0.48f, 1.15f);
        CameraShake.Shake(0.1f, 0.12f);
    }

    void SpawnIceTrident(Vector3 from, Vector3 to, Vector3 hitNormal, float R)
    {
        var beam = LaserVfxSpawner.ForKind(PlanetLaserKind.Ice, impact: false);
        var impact = LaserVfxSpawner.ForKind(PlanetLaserKind.Ice, impact: true);
        if (beam != null)
        {
            LaserVfxSpawner.SpawnBeam(beam, from, to, 0.11f, 0.5f);
            LaserVfxSpawner.SpawnImpact(impact, to, hitNormal, 0.1f, 1.6f);
            return;
        }

        SpawnBolt(from, to, new Color(0.55f, 0.88f, 1f), R * 0.028f);
    }

    void HadesPlague(Vector3 localDir)
    {
        float R = earth.Radius;
        Vector3 n = DirWorld(localDir);
        Vector3 hit = Point(localDir, R);

        ProFxParticleSpawner.SpawnWorldFromPath(
            "Fire & Explosions/ppfxExplosionGasSmall",
            hit,
            n,
            R * 0.048f,
            2f);
        SpawnMiasma(hit, n, R * 0.45f, new Color(0.35f, 0.05f, 0.45f, 0.55f));

        EarthCraterDeform.Ensure(earth)?.CarveHole(hit, 0.2f, 0.14f);
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, 0.07f, 0.82f);
        MemeAttackSystem.ApplyCasualtiesAt(earth, hit, 0.1f, 0.65f, 1f);
        CameraShake.Shake(0.08f, 0.12f);
    }

    void ApolloSunScourge(Vector3 localDir)
    {
        float R = earth.Radius;
        Vector3 n = DirWorld(localDir);
        Vector3 hit = Point(localDir, R);

        ProFxParticleSpawner.SpawnWorldFromPath(
            "Fire & Explosions/ppfxFireBig",
            hit,
            n,
            R * 0.05f,
            2.4f);
        SpawnSunCorona(hit, n, R * 1.1f);

        var scorch = EarthSurfaceScorch.Ensure(earth);
        scorch?.BurnAt(hit, 0.12f, 0.62f);
        scorch?.PaintImpactCrater(hit, 0.1f, 88);
        MemeAttackSystem.ApplyCasualtiesAt(earth, hit, 0.12f, 0.58f, 1.05f);
        CameraShake.Shake(0.09f, 0.14f);
    }

    void AresWarRain(Vector3 localDir)
    {
        float R = earth.Radius;
        Vector3 n = DirWorld(localDir);
        Vector3 hit = Point(localDir, R);

        for (int i = 0; i < 8; i++)
        {
            Vector3 jitter = (localDir + Random.insideUnitSphere * 0.22f).normalized;
            Vector3 sHit = Point(jitter, R);
            Vector3 sN = DirWorld(jitter);
            float power = Random.Range(0.5f, 0.9f);
            NuclearBlast.Play(earth, sHit, sN, power);
            EarthCraterDeform.Ensure(earth)?.Dig(sHit, 0.06f, 0.028f, false);
        }

        ProFxParticleSpawner.SpawnWorldFromPath(
            "Fire & Explosions/ppfxExplosionHeavyRough",
            hit,
            n,
            R * 0.038f,
            2f);
        MemeAttackSystem.ApplyCasualtiesAt(earth, hit, 0.13f, 0.75f, 1.1f);
        CameraShake.Shake(0.11f, 0.16f);
    }

    void HephaestusForge(Vector3 localDir)
    {
        float R = earth.Radius;
        Vector3 n = DirWorld(localDir);
        Vector3 hit = Point(localDir, R);

        var deform = EarthCraterDeform.Ensure(earth);
        var scorch = EarthSurfaceScorch.Ensure(earth);

        deform?.SpikeErupt(hit, 0.18f, 0.72f, 58);
        scorch?.BurnAt(hit, 0.08f, 0.75f);
        scorch?.PaintMoltenFissures(hit, 0.11f, 0.55f, 2.4f, 14);
        scorch?.PaintLavaCracks(hit, 0.1f, 18);
        ProFxParticleSpawner.SpawnWorldFromPath(
            "Fire & Explosions/ppfxFireSmallSmoke",
            hit,
            n,
            R * 0.044f,
            2f);
        SpawnForgeSparks(hit, n, R * 0.35f);

        MemeAttackSystem.ApplyCasualtiesAt(earth, hit, 0.1f, 0.68f, 1.15f);
        CameraShake.Shake(0.14f, 0.18f);
    }

    Vector3 DirWorld(Vector3 localDir) =>
        earth.transform.TransformDirection(localDir).normalized;

    Vector3 Point(Vector3 localDir, float dist) =>
        earth.transform.position + DirWorld(localDir) * dist;

    static void SpawnBolt(Vector3 from, Vector3 to, Color color, float width)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "ZeusBolt";
        go.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(color, 8f);
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AlignBeam(go.transform, from, to, width);
        Object.Destroy(go, 0.22f);
    }

    static void AlignBeam(Transform beam, Vector3 from, Vector3 to, float width)
    {
        Vector3 mid = (from + to) * 0.5f;
        float len = Vector3.Distance(from, to) * 0.5f;
        beam.position = mid;
        beam.up = (to - from).normalized;
        beam.localScale = new Vector3(width, Mathf.Max(0.01f, len), width);
    }

    static void SpawnTsunamiRing(Vector3 hit, Vector3 n, float size, Color col)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<Collider>());
        ring.name = "TsunamiRing";
        ring.transform.position = hit + n * 0.04f;
        ring.transform.up = n;
        ring.transform.localScale = new Vector3(size, 0.015f * size, size);
        ring.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(col);
        ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.Destroy(ring, 1.4f);
    }

    static Vector3 RotateOnTangent(Vector3 normal, float azimuth)
    {
        Vector3 t = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 b = Vector3.Cross(normal, t);
        return (Mathf.Cos(azimuth) * t + Mathf.Sin(azimuth) * b).normalized;
    }

    static void SpawnMiasma(Vector3 hit, Vector3 n, float size, Color col)
    {
        for (int i = 0; i < 5; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "HadesMiasma";
            go.transform.position = hit + n * Random.Range(0.05f, 0.2f) * size + Random.insideUnitSphere * size * 0.35f;
            go.transform.localScale = Vector3.one * Random.Range(size * 0.08f, size * 0.18f);
            var mat = RuntimeMaterial.UnlitTransparent(col);
            go.GetComponent<Renderer>().material = mat;
            go.AddComponent<QuakeDustFade>().Init(mat, Random.Range(0.5f, 0.9f), size * 1.2f);
        }
    }

    static void SpawnSunCorona(Vector3 hit, Vector3 n, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "ApolloCorona";
        go.transform.position = hit + n * 0.06f;
        go.transform.localScale = Vector3.one * size * 0.15f;
        var mat = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.85f, 0.2f, 0.45f));
        go.GetComponent<Renderer>().material = mat;
        go.AddComponent<MoonFlashBurst>().Init(mat, size * 0.02f, size * 0.22f, 0.85f);
    }

    static void SpawnForgeSparks(Vector3 hit, Vector3 n, float size)
    {
        for (int i = 0; i < 12; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "ForgeSpark";
            go.transform.position = hit + n * Random.Range(0.02f, 0.15f) * size;
            go.transform.localScale = Vector3.one * Random.Range(size * 0.04f, size * 0.1f);
            var mat = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.55f, 0.05f, 0.85f));
            go.GetComponent<Renderer>().material = mat;
            go.AddComponent<QuakeDustFade>().Init(mat, Random.Range(0.25f, 0.5f), size * 0.8f);
        }
    }
}

/// <summary>포세이돈 — 파란 파동 링이 연속으로 울려 퍼짐.</summary>
public class PoseidonRipplePulse : MonoBehaviour
{
    static readonly Color RippleColor = new Color(0.35f, 0.82f, 1f, 0.78f);
    static readonly Color RingColor = new Color(0.25f, 0.72f, 0.98f, 0.42f);

    public void Play(Vector3 hit, Vector3 normal, float earthRadius, float maxRingNorm, int waveCount)
    {
        StartCoroutine(Run(hit, normal, earthRadius, maxRingNorm, waveCount));
    }

    IEnumerator Run(Vector3 hit, Vector3 normal, float R, float maxRingNorm, int waveCount)
    {
        for (int i = 0; i < waveCount; i++)
        {
            float u = (i + 1) / (float)waveCount;
            float ringSize = R * Mathf.Lerp(0.32f, 0.55f + maxRingNorm * 1.6f, u);
            ImpactShockwave.Spawn(hit, normal, ringSize, RippleColor);
            SpawnTsunamiRing(hit, normal, ringSize * 0.92f,
                new Color(RingColor.r, RingColor.g, RingColor.b, RingColor.a * (1f - u * 0.35f)));
            CameraShake.Shake(0.035f + u * 0.03f, 0.055f);
            yield return new WaitForSeconds(0.11f);
        }

        Destroy(gameObject, 0.2f);
    }

    static void SpawnTsunamiRing(Vector3 hit, Vector3 n, float size, Color col)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<Collider>());
        ring.name = "TsunamiRing";
        ring.transform.position = hit + n * 0.035f;
        ring.transform.up = n;
        ring.transform.localScale = new Vector3(size, 0.012f * size, size);
        ring.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(col);
        ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.Destroy(ring, 1.1f);
    }
}
