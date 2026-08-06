using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MoonImpactMode
{
    Orbit,
    Crash
}

[System.Serializable]
public class MoonImpactReport
{
    public MoonImpactMode mode;
    public string modeLabel;
    public float lat;
    public float lon;
    public string regionHint;
    public long deaths;
    public long injuries;
    public List<string> notes = new List<string>();
}

/// <summary>
/// Moon Impact: Orbit(스쳐 지나감) / Crash(직격 충돌).
/// </summary>
public class MoonImpactSystem : MonoBehaviour
{
    public static MoonImpactSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;

    public bool IsRunning { get; private set; }
    public MoonImpactReport LastReport { get; private set; }
    public MoonImpactMode LastMode { get; private set; } = MoonImpactMode.Orbit;

    GameObject moonGo;

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
        DestroyMoon();
    }

    public void Configure(EarthPlanet planet) => earth = planet;

    public bool TryStart(MoonImpactMode mode)
    {
        return TryStartAt(mode, null);
    }

    public bool TryStartAt(MoonImpactMode mode, Vector3? worldTarget)
    {
        if (IsRunning)
            return false;
        LastMode = mode;
        StartCoroutine(Run(mode, worldTarget));
        return true;
    }

    IEnumerator Run(MoonImpactMode mode, Vector3? worldTarget)
    {
        IsRunning = true;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();

        float lat;
        float lon;
        if (worldTarget.HasValue && earth != null)
        {
            Vector3 local = earth.transform.InverseTransformPoint(worldTarget.Value).normalized;
            EarthGeo.DirectionToLatLon(local, out lat, out lon);
        }
        else
        {
            lat = Random.Range(-45f, 55f);
            lon = Random.Range(-180f, 180f);
        }

        Vector3 localDir = EarthGeo.LatLonToDirection(lat, lon);
        float R = earth != null ? earth.Radius : 2.5f;
        Vector3 center = earth != null ? earth.transform.position : Vector3.zero;

        MoonImpactReport report = BuildReport(mode, lat, lon);
        LastReport = report;

        Vector3 worldDir = earth != null
            ? earth.transform.TransformDirection(localDir).normalized
            : localDir;
        center = earth != null ? earth.transform.position : Vector3.zero;

        CleanupLeftoverFx();
        var orbitCam = Object.FindObjectOfType<OrbitCamera>();

        if (mode == MoonImpactMode.Crash)
        {
            // Crash: 달 즉시 등장 + 카메라가 달을 추적
            moonGo = SpawnMoon(R);
            if (orbitCam != null && moonGo != null && earth != null)
                orbitCam.BeginChase(moonGo.transform, earth.transform, R * 2.4f, 0.32f);

            yield return RunCrash(moonGo, localDir, R, report);

            if (orbitCam != null)
                orbitCam.EndChase(true);
        }
        else
        {
            // Orbit: 시네마틱 구도 후 플라이바이
            if (orbitCam != null)
                orbitCam.FrameApproachShot(worldDir, R, 0.65f);
            yield return WaitSim(0.55f);

            moonGo = SpawnMoon(R);
            yield return RunOrbit(moonGo, center, worldDir, R, report);
        }

        DestroyMoon();

        var pop = PopulationSystem.Instance;
        if (pop != null)
            pop.ApplyCasualties(report.deaths);

        yield return WaitSim(0.35f);

        IsRunning = false;
        // Moon Impact Report 모달은 띄우지 않음
    }

    /// <summary>
    /// Orbit = 충돌 없이 카메라 앞에서 지구를 스쳐 지나감 (니어미스 플라이바이).
    /// </summary>
    IEnumerator RunOrbit(GameObject moon, Vector3 center, Vector3 approachDir, float R, MoonImpactReport report)
    {
        // 시네마틱 구도 기준으로 횡단 — 달이 화면에서 지구를 스치며 지나감
        var cam = Camera.main;
        Vector3 toCam = cam != null
            ? (cam.transform.position - center).normalized
            : approachDir;
        Vector3 up = cam != null ? cam.transform.up : Vector3.up;
        Vector3 flyDir = Vector3.Cross(toCam, up);
        if (flyDir.sqrMagnitude < 1e-4f)
            flyDir = Vector3.Cross(toCam, Vector3.up);
        flyDir.Normalize();

        Vector3 closest = center + toCam * (R * 1.55f);
        Vector3 p0 = closest - flyDir * (R * 7.5f) + toCam * (R * 1.8f);
        Vector3 p1 = closest;
        Vector3 p2 = closest + flyDir * (R * 7.0f) + toCam * (R * 0.5f);

        moon.transform.position = p0;

        bool tidalDone = false;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * Sim() * 0.5f;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            moon.transform.position = Bezier(p0, p1, p2, u);
            moon.transform.Rotate(Vector3.up, 90f * Time.deltaTime * Sim(), Space.Self);

            if (!tidalDone && u >= 0.48f)
            {
                tidalDone = true;
                CameraShake.Shake(1.8f, 1.0f);
                Vector3 hitNormal = (closest - center).normalized;
                Vector3 hit = center + hitNormal * R;
                var scorch = earth != null ? EarthSurfaceScorch.Ensure(earth) : null;
                if (scorch != null)
                {
                    scorch.BurnAt(hit, 0.05f, 0.45f);
                    scorch.CrackAt(hit, 0.07f, 8);
                }
                SpawnDustCloud(hit, hitNormal, R * 0.07f, 12);
            }

            yield return null;
        }

        CameraShake.Shake(0.4f, 0.2f);
        yield return WaitSim(0.25f);
    }

    IEnumerator RunCrash(GameObject moon, Vector3 localDir, float R, MoonImpactReport report)
    {
        float t = 0f;
        while (t < 1f)
        {
            float speed = Mathf.Lerp(0.55f, 2.2f, t);
            t += Time.unscaledDeltaTime * Sim() * speed;
            float u = Mathf.Clamp01(t);
            u = u * u;
            Vector3 center = earth.transform.position;
            Vector3 n = DirectionWorld(localDir);
            Vector3 start = center + n * (R * 8.5f);
            Vector3 impact = center + n * R;
            moon.transform.position = Vector3.Lerp(start, impact, u);
            moon.transform.Rotate(Vector3.right, 120f * Time.deltaTime * Sim(), Space.Self);
            yield return null;
        }

        Vector3 hit = earth.transform.position + DirectionWorld(localDir) * R;
        Vector3 approachDir = DirectionWorld(localDir);
        moon.transform.position = hit;
        // 달은 충돌과 함께 소멸 — 추적 해제 후 파편 없이 제거
        var orbitCam = Object.FindObjectOfType<OrbitCamera>();
        if (orbitCam != null)
            orbitCam.EndChase(true);
        DestroyMoon();

        CameraShake.Shake(2.8f, 1.45f);
        CinematicExplosion.Play(hit, approachDir, 2.6f);
        SpawnFlashBurst(hit, approachDir, R * 1.4f, 1.35f);
        SpawnDustCloud(hit, approachDir, R * 0.18f, 22);
        SpawnDustVeil(earth.transform.position, R * 1.08f);

        // 움푹 + 맞은 면 용암 원형
        float craterR = 0.22f;
        ImpactCrater.SpawnHuge(earth, hit, craterR, 0.09f);

        // 쉐이크와 함께 용암 균열이 바깥으로 갈라짐 (텍스처 + 발광 리본)
        yield return MoltenCrackFx.Play(earth, hit, craterR);

        yield return WaitSim(0.45f);
        CameraShake.Shake(0.8f, 0.4f);
        yield return WaitSim(0.4f);
    }

    MoonImpactReport BuildReport(MoonImpactMode mode, float lat, float lon)
    {
        bool crash = mode == MoonImpactMode.Crash;
        long popNow = PopulationSystem.Instance != null
            ? PopulationSystem.Instance.Population
            : PopulationSystem.BaselinePopulation;

        // float 범위 오차 피하려고 인구 비율로 계산
        // Crash: 달 직격 ≈ 거의 전멸 / Orbit: 조석·쓰나미로 대규모 피해
        double killFrac = crash
            ? Random.Range(0.92f, 0.995f)
            : Random.Range(0.12f, 0.28f);
        long deaths = (long)System.Math.Floor(popNow * killFrac);
        deaths = System.Math.Max(0, System.Math.Min(deaths, popNow));
        long survivors = popNow - deaths;
        long injuries = crash
            ? (long)System.Math.Min(survivors, System.Math.Floor(survivors * Random.Range(0.6f, 0.95f)))
            : (long)System.Math.Floor(deaths * Random.Range(1.8f, 3.2f));

        var report = new MoonImpactReport
        {
            mode = mode,
            modeLabel = crash ? "Crash Mode" : "Orbit Mode",
            lat = lat,
            lon = lon,
            regionHint = crash ? "Global Extinction Zone" : "Tidal Corridor",
            deaths = deaths,
            injuries = injuries
        };

        if (crash)
        {
            report.notes.Add($"~{killFrac * 100.0:0}% of humanity lost on impact day");
            report.notes.Add("Moon destroyed — crustal rupture at impact site");
            report.notes.Add("Global firestorm, ejecta winter, oceans flash-boiled near limb");
            report.notes.Add("Civilization-ending event");
            report.notes.Add($"Impact near {lat:0.0}, {lon:0.0}");
        }
        else
        {
            report.notes.Add($"~{killFrac * 100.0:0}% casualties from tides and megatsunamis");
            report.notes.Add("Near-miss flyby — Moon remains intact");
            report.notes.Add("Extreme crustal stress along closest-approach belt");
            report.notes.Add($"Closest approach over {lat:0.0}, {lon:0.0}");
        }

        return report;
    }

    GameObject SpawnMoon(float earthR)
    {
        // NASA LROC 텍스처 + 하이스피어 (회색 프리미티브/가짜 크레이터 제거)
        return MoonVisual.Create(earthR, 0.42f);
    }

    void CleanupLeftoverFx()
    {
        DestroyByName("MoonDebris");
        DestroyByName("ShockRing");
        DestroyByName("MoonDust");
        DestroyByName("ImpactDustVeil");
        DestroyByName("MoonFlash");
        DestroyByName("EventMoon");
    }

    static void DestroyByName(string name)
    {
        var all = Object.FindObjectsOfType<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                Object.Destroy(all[i].gameObject);
        }
    }

    void SpawnDustCloud(Vector3 point, Vector3 normal, float size, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "MoonDust";
            Object.Destroy(go.GetComponent<Collider>());
            // 표면에서 바깥으로만 — 땅에 박히지 않게
            go.transform.position = point + normal * (size * Random.Range(0.35f, 1.1f))
                + Random.insideUnitSphere * (size * 0.35f);
            go.transform.localScale = Vector3.one * Random.Range(size * 0.2f, size * 0.7f);

            var mat = RuntimeMaterial.UnlitTransparent(new Color(0.4f, 0.35f, 0.3f, 0.35f));
            var rend = go.GetComponent<Renderer>();
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var fade = go.AddComponent<QuakeDustFade>();
            fade.Init(mat, Random.Range(0.55f, 1.1f), size * Random.Range(1.4f, 2.2f));
        }
    }

    void SpawnDustVeil(Vector3 center, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ImpactDustVeil";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = center;
        go.transform.localScale = Vector3.one * (radius * 2f);
        var mat = RuntimeMaterial.UnlitTransparent(new Color(0.15f, 0.12f, 0.1f, 0.0f));
        var rend = go.GetComponent<Renderer>();
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MoonDustVeil>().Init(mat, 0.22f, 3.2f);
    }

    /// <summary>투명 섬광 구체 — 잠깐 커졌다가 사라짐 (원판 데칼 없음).</summary>
    void SpawnFlashBurst(Vector3 point, Vector3 normal, float endRadius, float life)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MoonFlash";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = point + normal * 0.05f;
        go.transform.localScale = Vector3.one * (endRadius * 0.15f);
        var mat = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.7f, 0.35f, 0.55f));
        var rend = go.GetComponent<Renderer>();
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MoonFlashBurst>().Init(mat, endRadius * 0.15f, endRadius, life);
    }

    void DestroyMoon()
    {
        if (moonGo != null)
        {
            Object.Destroy(moonGo);
            moonGo = null;
        }
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    Vector3 DirectionWorld(Vector3 localDir) =>
        earth.transform.TransformDirection(localDir).normalized;

    float Sim()
    {
        return WorldStatusHud.Instance != null
            ? Mathf.Max(0.05f, WorldStatusHud.Instance.SimSpeed)
            : 1f;
    }

    IEnumerator WaitSim(float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            left -= Time.unscaledDeltaTime * Sim();
            yield return null;
        }
    }
}

public class MoonFlashBurst : MonoBehaviour
{
    Material mat;
    float startR;
    float endR;
    float life;
    float t;

    public void Init(Material m, float start, float end, float lifeSec)
    {
        mat = m;
        startR = start;
        endR = end;
        life = Mathf.Max(0.1f, lifeSec);
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / life);
        float r = Mathf.Lerp(startR, endR, Mathf.SmoothStep(0f, 1f, u));
        transform.localScale = Vector3.one * r;
        if (mat != null)
        {
            Color c = mat.color;
            c.a = Mathf.Lerp(0.55f, 0f, u);
            mat.color = c;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", c);
        }
        if (u >= 1f)
            Destroy(gameObject);
    }
}

public class MoonDustVeil : MonoBehaviour
{
    Material mat;
    float peakAlpha;
    float life;
    float t;

    public void Init(Material m, float peak, float lifeSec)
    {
        mat = m;
        peakAlpha = peak;
        life = lifeSec;
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / life);
        float a;
        if (u < 0.2f)
            a = Mathf.Lerp(0f, peakAlpha, u / 0.2f);
        else
            a = Mathf.Lerp(peakAlpha, 0f, (u - 0.2f) / 0.8f);

        if (mat != null)
        {
            Color c = mat.color;
            c.a = a;
            mat.color = c;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", c);
        }

        transform.localScale *= 1.0015f;
        if (u >= 1f)
            Destroy(gameObject);
    }
}
