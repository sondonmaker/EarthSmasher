using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EarthquakeReport
{
    public float magnitude;
    public string regionName;
    public string regionCode;
    public float lat;
    public float lon;
    public long deaths;
    public long injuries;
    public int aftershocks;
    public List<string> notes = new List<string>();
}

/// <summary>
/// Earthquake 재해: 쉐이크 → 균열 확산 → 여진 → 인구 타격 → 리포트.
/// </summary>
public class EarthquakeSystem : MonoBehaviour
{
    public static EarthquakeSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;

    public bool IsRunning { get; private set; }
    public EarthquakeReport LastReport { get; private set; }

    struct QuakeRegion
    {
        public string name;
        public string code;
        public float lat;
        public float lon;
        public float weight; // population / risk
    }

    static readonly QuakeRegion[] Regions =
    {
        new QuakeRegion { name = "Japan", code = "JP", lat = 35.7f, lon = 139.7f, weight = 1.1f },
        new QuakeRegion { name = "Indonesia", code = "ID", lat = -6.2f, lon = 106.8f, weight = 1.15f },
        new QuakeRegion { name = "Chile", code = "CL", lat = -33.4f, lon = -70.7f, weight = 0.7f },
        new QuakeRegion { name = "California", code = "US", lat = 34.0f, lon = -118.2f, weight = 0.95f },
        new QuakeRegion { name = "Turkey", code = "TR", lat = 41.0f, lon = 29.0f, weight = 0.85f },
        new QuakeRegion { name = "Iran", code = "IR", lat = 35.7f, lon = 51.4f, weight = 0.8f },
        new QuakeRegion { name = "Mexico", code = "MX", lat = 19.4f, lon = -99.1f, weight = 0.9f },
        new QuakeRegion { name = "China", code = "CN", lat = 31.2f, lon = 104.1f, weight = 1.2f },
        new QuakeRegion { name = "Nepal", code = "NP", lat = 27.7f, lon = 85.3f, weight = 0.75f },
        new QuakeRegion { name = "Philippines", code = "PH", lat = 14.6f, lon = 121.0f, weight = 0.9f },
        new QuakeRegion { name = "Italy", code = "IT", lat = 40.9f, lon = 14.3f, weight = 0.65f },
        new QuakeRegion { name = "New Zealand", code = "NZ", lat = -41.3f, lon = 174.8f, weight = 0.5f },
        new QuakeRegion { name = "Peru", code = "PE", lat = -12.0f, lon = -77.0f, weight = 0.7f },
        new QuakeRegion { name = "Alaska", code = "US", lat = 61.2f, lon = -149.9f, weight = 0.4f },
        new QuakeRegion { name = "Taiwan", code = "TW", lat = 23.7f, lon = 121.0f, weight = 0.85f },
    };

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

    public void Configure(EarthPlanet planet) => earth = planet;

    public bool TrySuggestLocation(out float lat, out float lon)
    {
        return TrySuggestLocation(out lat, out lon, out _);
    }

    public bool TrySuggestLocation(out float lat, out float lon, out string regionName)
    {
        QuakeRegion region = PickRegion();
        lat = region.lat + Random.Range(-2.5f, 2.5f);
        lon = region.lon + Random.Range(-3.5f, 3.5f);
        regionName = region.name;
        return true;
    }

    public bool TryStart(float magnitude, float lat, float lon)
    {
        if (IsRunning)
            return false;
        magnitude = Mathf.Clamp(magnitude, 0.1f, 12f);
        lat = Mathf.Clamp(lat, -90f, 90f);
        lon = Mathf.Clamp(lon, -180f, 180f);
        StartCoroutine(RunQuake(magnitude, lat, lon));
        return true;
    }

    IEnumerator RunQuake(float magnitude, float lat, float lon)
    {
        IsRunning = true;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();

        QuakeRegion region = FindNearestRegion(lat, lon);
        EarthquakeReport report = BuildReport(magnitude, region, lat, lon);
        LastReport = report;

        // 로컬 위경도 → 월드 (지구 자전 반영)
        Vector3 localDir = EarthGeo.LatLonToDirection(lat, lon);
        Vector3 worldDir = earth != null ? earth.transform.TransformDirection(localDir).normalized : localDir;
        float R = earth != null ? earth.Radius : 2.5f;
        Vector3 epicenter = (earth != null ? earth.transform.position : Vector3.zero) + worldDir * R;

        // 카메라 강제 이동 없음 — 재해 중에도 자유 조작
        yield return WaitSim(0.35f);

        // 자전 반영해 진앙 갱신
        worldDir = earth.transform.TransformDirection(localDir).normalized;
        epicenter = earth.transform.position + worldDir * R;

        // 1) 메인 충격 — 쉐이크
        float shakeStr = Mathf.Lerp(0.15f, 1.35f, Mathf.InverseLerp(3f, 9.5f, magnitude));
        float shakeDur = Mathf.Lerp(0.5f, 2.4f, Mathf.InverseLerp(3f, 9.5f, magnitude));
        CameraShake.Shake(shakeDur, shakeStr);
        SpawnDust(epicenter, worldDir, magnitude);

        yield return WaitSim(0.25f);

        // 2) 균열 페인팅 (단계적으로 퍼짐)
        var scorch = earth != null ? EarthSurfaceScorch.Ensure(earth) : null;
        float crackR = Mathf.Lerp(0.025f, 0.09f, Mathf.InverseLerp(3f, 9.5f, magnitude));
        int branches = Mathf.RoundToInt(Mathf.Lerp(4f, 11f, Mathf.InverseLerp(3f, 9.5f, magnitude)));

        if (scorch != null)
        {
            worldDir = earth.transform.TransformDirection(localDir).normalized;
            epicenter = earth.transform.position + worldDir * R;
            scorch.CrackAt(epicenter, crackR * 0.55f, Mathf.Max(3, branches / 2));
            yield return WaitSim(0.35f);
            CameraShake.Shake(shakeDur * 0.45f, shakeStr * 0.55f);
            worldDir = earth.transform.TransformDirection(localDir).normalized;
            epicenter = earth.transform.position + worldDir * R;
            scorch.CrackAt(epicenter, crackR, branches);
        }

        // 인구 피해 (메인 충격 시)
        var pop = PopulationSystem.Instance;
        if (pop != null)
        {
            long chunk = report.deaths / 2;
            pop.ApplyCasualties(chunk);
        }

        yield return WaitSim(0.5f);

        // 3) 여진
        for (int i = 0; i < report.aftershocks; i++)
        {
            yield return WaitSim(Random.Range(0.55f, 1.35f));
            float m2 = magnitude - Random.Range(0.8f, 1.8f);
            float s2 = shakeStr * Random.Range(0.25f, 0.55f);
            CameraShake.Shake(0.35f + s2 * 0.4f, s2);
            // 여진 시점 최신 진앙 월드좌표
            worldDir = earth.transform.TransformDirection(localDir).normalized;
            epicenter = earth.transform.position + worldDir * R;
            SpawnDust(epicenter + Random.onUnitSphere * (R * 0.02f), worldDir, m2);

            if (scorch != null && Random.value > 0.35f)
            {
                Vector3 offsetLocal = EarthGeo.LatLonToDirection(
                    lat + Random.Range(-1.5f, 1.5f),
                    lon + Random.Range(-2f, 2f));
                Vector3 p2 = earth.transform.position + earth.transform.TransformDirection(offsetLocal).normalized * R;
                scorch.CrackAt(p2, crackR * Random.Range(0.25f, 0.45f), Random.Range(3, 6));
            }
        }

        if (pop != null)
        {
            long remain = report.deaths - report.deaths / 2;
            if (remain > 0)
                pop.ApplyCasualties(remain);
        }

        yield return WaitSim(0.4f);

        IsRunning = false;
        EarthquakeReportUI.Ensure().Show(report);
    }

    EarthquakeReport BuildReport(float magnitude, QuakeRegion region, float lat, float lon)
    {
        // 게임용 스케일: M4 소규모 ~ M9 대재앙
        double raw = System.Math.Pow(10.0, magnitude - 1.8) * region.weight * Random.Range(0.55f, 1.25f);
        long deaths = (long)System.Math.Min(raw, 80_000_000);
        if (magnitude < 4f)
            deaths = (long)(deaths * 0.15f);
        long injuries = (long)(deaths * Random.Range(2.2f, 4.5f));

        int aftershocks = 0;
        if (magnitude >= 6f)
            aftershocks = Mathf.RoundToInt(Mathf.Lerp(1f, 4f, Mathf.InverseLerp(6f, 9.5f, magnitude)));

        var report = new EarthquakeReport
        {
            magnitude = magnitude,
            regionName = region.name,
            regionCode = region.code,
            lat = lat,
            lon = lon,
            deaths = System.Math.Max(0, deaths),
            injuries = System.Math.Max(0, injuries),
            aftershocks = aftershocks
        };

        report.notes.Add($"Epicenter near {region.name}");
        report.notes.Add($"Lat {lat:0.0}, Lon {lon:0.0}");
        if (magnitude >= 8f)
            report.notes.Add("Severe structural collapse across metro areas");
        else if (magnitude >= 6.5f)
            report.notes.Add("Major damage in dense urban zones");
        else if (magnitude >= 5f)
            report.notes.Add("Moderate damage, widespread panic");
        else
            report.notes.Add("Light to moderate shaking");

        if (aftershocks > 0)
            report.notes.Add($"{aftershocks} aftershock(s) recorded");

        return report;
    }

    QuakeRegion PickRegion()
    {
        float total = 0f;
        for (int i = 0; i < Regions.Length; i++)
            total += Regions[i].weight;
        float r = Random.value * total;
        for (int i = 0; i < Regions.Length; i++)
        {
            r -= Regions[i].weight;
            if (r <= 0f)
                return Regions[i];
        }
        return Regions[0];
    }

    QuakeRegion FindNearestRegion(float lat, float lon)
    {
        Vector3 target = EarthGeo.LatLonToDirection(lat, lon);
        int best = 0;
        float bestDot = -2f;
        for (int i = 0; i < Regions.Length; i++)
        {
            Vector3 d = EarthGeo.LatLonToDirection(Regions[i].lat, Regions[i].lon);
            float dot = Vector3.Dot(target, d);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = i;
            }
        }
        return Regions[best];
    }

    void SpawnDust(Vector3 point, Vector3 normal, float magnitude)
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(magnitude * 1.2f), 4, 14);
        float size = Mathf.Lerp(0.08f, 0.28f, Mathf.InverseLerp(3f, 9f, magnitude));
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "QuakeDust";
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = point + normal * 0.02f + Random.insideUnitSphere * (size * 0.4f);
            go.transform.localScale = Vector3.one * Random.Range(size * 0.35f, size);

            var mat = RuntimeMaterial.UnlitTransparent(new Color(0.45f, 0.35f, 0.25f, 0.45f));
            var rend = go.GetComponent<Renderer>();
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var fade = go.AddComponent<QuakeDustFade>();
            fade.Init(mat, Random.Range(0.45f, 0.9f), size * Random.Range(1.4f, 2.4f));
        }
    }

    IEnumerator WaitSim(float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            float sim = WorldStatusHud.Instance != null ? WorldStatusHud.Instance.SimSpeed : 1f;
            left -= Time.unscaledDeltaTime * Mathf.Max(0.05f, sim);
            yield return null;
        }
    }
}

public class QuakeDustFade : MonoBehaviour
{
    Material mat;
    float life;
    float t;
    float start;
    float end;
    Color color;

    public void Init(Material m, float lifeSec, float endScale)
    {
        mat = m;
        life = Mathf.Max(0.1f, lifeSec);
        start = transform.localScale.x;
        end = endScale;
        color = mat != null ? mat.color : Color.white;
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / life);
        transform.localScale = Vector3.one * Mathf.Lerp(start, end, u);
        if (mat != null)
        {
            Color c = color;
            c.a = color.a * (1f - u);
            mat.color = c;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", c);
        }
        if (u >= 1f)
            Destroy(gameObject);
    }
}
