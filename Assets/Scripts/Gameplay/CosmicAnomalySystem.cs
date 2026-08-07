using System.Collections;
using UnityEngine;

public enum CosmicAnomalyKind
{
    BlackHole,
    Vortex,
    SpikeErupt
}

/// <summary>블랙홀 / 보텍스 / 스파이크 분출.</summary>
public class CosmicAnomalySystem : MonoBehaviour
{
    public static CosmicAnomalySystem Instance { get; private set; }

    [SerializeField] Camera cam;
    [SerializeField] EarthPlanet earth;

    public bool IsAiming => false;
    public CosmicAnomalyKind AimKind { get; private set; }

    public static CosmicAnomalySystem Ensure()
    {
        var s = FindObjectOfType<CosmicAnomalySystem>();
        if (s != null)
            return s;
        return new GameObject("CosmicAnomalySystem").AddComponent<CosmicAnomalySystem>();
    }

    void Awake()
    {
        Instance = this;
        if (cam == null)
            cam = Camera.main;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Abort()
    {
        StopAllCoroutines();
    }

    public void SpawnAt(CosmicAnomalyKind kind, Vector3 point, Vector3 normal)
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        AimKind = kind;

        switch (kind)
        {
            case CosmicAnomalyKind.BlackHole:
                StartCoroutine(RunBlackHole(point, normal));
                break;
            case CosmicAnomalyKind.SpikeErupt:
                StartCoroutine(RunSpikeErupt(point, normal));
                break;
            default:
                StartCoroutine(RunVortex(point, normal));
                break;
        }
    }

    /// <summary>작은 블랙홀 → 점점 커지며 그 자리 지표를 안으로 삼킴.</summary>
    IEnumerator RunBlackHole(Vector3 point, Vector3 normal)
    {
        if (earth == null)
            yield break;

        ResolveSurface(earth, point, out float lat, out float lon, out _, out _);

        // 표면 바로 위 작은 점에서 시작
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BlackHole";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = point + normal * (earth.Radius * 0.02f);
        go.transform.localScale = Vector3.one * 0.04f;

        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.01f, 0.01f, 0.02f), 0f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (ProFxParticleSpawner.AttachCosmicPortal(go.transform, earth.Radius * 0.06f) != null)
            rend.enabled = false;

        var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(ring.GetComponent<Collider>());
        ring.name = "Accretion";
        ring.transform.SetParent(go.transform, false);
        ring.transform.localScale = Vector3.one * 1.55f;
        ring.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(new Color(0.7f, 0.25f, 1f, 0.4f));
        ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (rend.enabled == false)
            ring.GetComponent<Renderer>().enabled = false;

        CameraShake.Shake(0.1f, 0.2f);

        var deform = EarthCraterDeform.Ensure(earth);
        var scorch = EarthSurfaceScorch.Ensure(earth);

        const float life = 5.0f;
        float t = 0f;
        float lastCarve = -1f;
        while (t < life)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / life);
            // 작 → 큼 (끝에서 약간 유지)
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.75f));
            float size = Mathf.Lerp(0.04f, earth.Radius * 0.55f, grow);
            go.transform.localScale = Vector3.one * size;
            // 점점 표면 안으로 파고듦
            go.transform.position = point + normal * (earth.Radius * Mathf.Lerp(0.02f, -0.08f, grow));
            go.transform.Rotate(normal, 90f * Time.deltaTime, Space.World);

            // 간헐 carve — Dig 누적/림 없이 안쪽으로만
            if (deform != null && t - lastCarve > 0.12f)
            {
                lastCarve = t;
                float rad = Mathf.Lerp(0.05f, 0.26f, grow);
                float depth = Mathf.Lerp(0.02f, 0.18f, grow * grow);
                deform.CarveHole(point, rad, depth);
                if (scorch != null)
                    scorch.BurnAt(point, 0.02f + 0.04f * grow, 0.9f);
            }

            yield return null;
        }

        // 마지막 한 번 더 깊게
        deform?.CarveHole(SurfaceFromLatLon(earth, lat, lon), 0.28f, 0.2f);
        PopulationCasualtySystem.ApplyAtLatLon(
            lat,
            lon,
            PopulationCasualtySystem.DigNormToDegrees(0.28f),
            0.68f,
            1.25f);
        CameraShake.Shake(0.14f, 0.22f);
        Object.Destroy(go);
    }

    /// <summary>지표가 삐죽 솟는 스파이크 분출 (옛 블랙홀 버그 연출).</summary>
    IEnumerator RunSpikeErupt(Vector3 point, Vector3 normal)
    {
        if (earth == null)
            yield break;

        CameraShake.Shake(0.18f, 0.3f);
        ProFxParticleSpawner.SpawnCosmicSpikeBurst(point, normal, earth.Radius);

        // 짧은 경고 섬광
        var flash = new GameObject("SpikeFlash");
        flash.transform.position = point + normal * 0.3f;
        var light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.35f, 0.15f);
        light.intensity = 10f;
        light.range = earth.Radius * 2f;
        flash.AddComponent<ImpactFlashFade>().Begin(0.5f);
        Object.Destroy(flash, 0.8f);

        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
        {
            // 단계적으로 더 높이 솟아오름
            deform.SpikeErupt(point, 0.12f, 0.25f, 11);
            yield return new WaitForSecondsRealtime(0.12f);
            deform.SpikeErupt(point, 0.16f, 0.45f, 22);
            yield return new WaitForSecondsRealtime(0.12f);
            deform.SpikeErupt(point, 0.2f, 0.75f, 33);
        }

        EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, 0.05f, 0.7f);
        PopulationCasualtySystem.ApplyAt(
            earth,
            point,
            PopulationCasualtySystem.DigNormToDegrees(0.2f),
            0.42f,
            1f);
        CameraShake.Shake(0.12f, 0.2f);
    }

    IEnumerator RunVortex(Vector3 point, Vector3 normal)
    {
        if (earth == null)
            yield break;

        ResolveSurface(earth, point, out float lat, out float lon, out Vector3 surface, out Vector3 surfNormal);
        float radiusDeg = PopulationCasualtySystem.DigNormToDegrees(0.08f);

        PopulationCasualtySystem.ApplyAtLatLon(lat, lon, radiusDeg, 0.22f, 0.75f);

        // 하늘→지표 번개 연타 (구 Zeus Thunder 연출)
        SpawnSkyLightning(surface, surfNormal, 1.15f);
        CameraShake.Shake(0.08f, 0.2f);
        var deform = EarthCraterDeform.Ensure(earth);
        var scorch = EarthSurfaceScorch.Ensure(earth);
        float t = 0f;
        float lastCarve = -1f;
        float lastPop = 0f;
        float lastBolt = 0f;
        while (t < 3.8f)
        {
            t += Time.deltaTime;
            surface = SurfaceFromLatLon(earth, lat, lon);
            surfNormal = (surface - earth.transform.position).normalized;

            if (t - lastBolt > 0.34f)
            {
                lastBolt = t;
                Vector3 boltSurf = surface;
                Vector3 boltN = surfNormal;
                if (Random.value > 0.32f)
                {
                    Vector3 localJ = earth.transform.InverseTransformDirection(surfNormal);
                    localJ = (localJ + Random.insideUnitSphere * 0.14f).normalized;
                    boltSurf = WorldPointFromLocal(earth, localJ);
                    boltN = (boltSurf - earth.transform.position).normalized;
                }

                SpawnSkyLightning(boltSurf, boltN, Random.Range(0.78f, 1.08f));
                CameraShake.Shake(0.035f, 0.075f);
            }

            if (deform != null && t - lastCarve > 0.25f)
            {
                lastCarve = t;
                deform.CarveHole(surface, 0.08f, 0.03f);
                scorch?.BurnAt(surface, 0.018f, 0.55f);
            }

            if (t - lastPop > 0.55f)
            {
                lastPop = t;
                PopulationCasualtySystem.ApplyAtLatLon(lat, lon, radiusDeg * 0.92f, 0.12f, 0.7f);
            }

            yield return null;
        }

        PopulationCasualtySystem.ApplyAtLatLon(lat, lon, radiusDeg * 1.12f, 0.38f, 1.05f);
        SpawnSkyLightning(surface, surfNormal, 1.35f);
        CameraShake.Shake(0.1f, 0.18f);
    }

    void SpawnSkyLightning(Vector3 surface, Vector3 surfNormal, float scale)
    {
        float R = earth.Radius;
        Vector3 sky = surface + surfNormal * (R * Random.Range(2.05f, 2.75f));
        var beam = LaserVfxSpawner.ForKind(PlanetLaserKind.Lightning, impact: false);
        var impact = LaserVfxSpawner.ForKind(PlanetLaserKind.Lightning, impact: true);
        if (beam != null)
        {
            LaserVfxSpawner.SpawnBeam(beam, sky, surface, scale * 0.14f, 0.38f);
            LaserVfxSpawner.SpawnImpact(impact, surface, surfNormal, scale * 0.11f, 0.52f);
            return;
        }

        SpawnBoltFallback(sky, surface, new Color(0.72f, 0.86f, 1f), R * scale * 0.038f);
    }

    static void SpawnBoltFallback(Vector3 from, Vector3 to, Color color, float width)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "VortexBolt";
        go.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(color, 7f);
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Vector3 mid = (from + to) * 0.5f;
        float len = Vector3.Distance(from, to) * 0.5f;
        go.transform.position = mid;
        go.transform.up = (to - from).normalized;
        go.transform.localScale = new Vector3(width, Mathf.Max(0.01f, len), width);
        Object.Destroy(go, 0.24f);
    }

    static Vector3 WorldPointFromLocal(EarthPlanet earth, Vector3 localDir)
    {
        Vector3 worldDir = earth.transform.TransformDirection(localDir).normalized;
        return earth.transform.position + worldDir * earth.Radius;
    }

    static void ResolveSurface(
        EarthPlanet earth,
        Vector3 worldPoint,
        out float lat,
        out float lon,
        out Vector3 surfaceWorld,
        out Vector3 worldNormal)
    {
        Vector3 local = earth.transform.InverseTransformPoint(worldPoint);
        Vector3 dir = local.sqrMagnitude > 1e-8f ? local.normalized : Vector3.up;
        EarthGeo.DirectionToLatLon(dir, out lat, out lon);
        surfaceWorld = SurfaceFromLatLon(earth, lat, lon);
        worldNormal = (surfaceWorld - earth.transform.position).normalized;
    }

    static Vector3 SurfaceFromLatLon(EarthPlanet earth, float lat, float lon)
    {
        Vector3 dir = EarthGeo.LatLonToDirection(lat, lon);
        var col = earth.GetComponent<SphereCollider>();
        float localR = col != null ? col.radius : 0.5f;
        return earth.transform.TransformPoint(dir * localR);
    }
}
