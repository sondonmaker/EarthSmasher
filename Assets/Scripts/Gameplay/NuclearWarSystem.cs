using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NuclearCountryResult
{
    public string name;
    public string code;
    public long deaths;
    public int hits;
    public int interceptions;
    public bool indirect;
    public bool nonNuclear;
}

[System.Serializable]
public class NuclearWarReport
{
    public int units;
    public int mainCountryCount;
    public int mainCityCount;
    public int totalAffectedCities;
    public long totalDeaths;
    public long totalInjuries;
    public List<NuclearCountryResult> topCountries = new List<NuclearCountryResult>();
    public List<NuclearCountryResult> nonNuclearAffected = new List<NuclearCountryResult>();
}

/// <summary>
/// Nuclear War 재해: 세계 각지 핵폭발 → 인구 감소 → 리포트.
/// </summary>
public class NuclearWarSystem : MonoBehaviour
{
    public static NuclearWarSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;
    [SerializeField] int defaultUnits = 100;

    public bool IsRunning { get; private set; }
    public NuclearWarReport LastReport { get; private set; }

    struct StrikeSite
    {
        public string country;
        public string code;
        public float lat;
        public float lon;
        public float weight;
        public bool nuclearPower;
    }

    static readonly StrikeSite[] Sites =
    {
        new StrikeSite { country = "China", code = "CN", lat = 39.9f, lon = 116.4f, weight = 1.00f, nuclearPower = true },
        new StrikeSite { country = "China", code = "CN", lat = 31.2f, lon = 121.5f, weight = 0.85f, nuclearPower = true },
        new StrikeSite { country = "China", code = "CN", lat = 23.1f, lon = 113.3f, weight = 0.55f, nuclearPower = true },
        new StrikeSite { country = "India", code = "IN", lat = 28.6f, lon = 77.2f, weight = 0.95f, nuclearPower = true },
        new StrikeSite { country = "India", code = "IN", lat = 19.1f, lon = 72.9f, weight = 0.75f, nuclearPower = true },
        new StrikeSite { country = "India", code = "IN", lat = 22.6f, lon = 88.4f, weight = 0.5f, nuclearPower = true },
        new StrikeSite { country = "United States", code = "US", lat = 38.9f, lon = -77.0f, weight = 0.7f, nuclearPower = true },
        new StrikeSite { country = "United States", code = "US", lat = 40.7f, lon = -74.0f, weight = 0.8f, nuclearPower = true },
        new StrikeSite { country = "United States", code = "US", lat = 34.0f, lon = -118.2f, weight = 0.65f, nuclearPower = true },
        new StrikeSite { country = "Russia", code = "RU", lat = 55.75f, lon = 37.62f, weight = 0.55f, nuclearPower = true },
        new StrikeSite { country = "Russia", code = "RU", lat = 59.9f, lon = 30.3f, weight = 0.35f, nuclearPower = true },
        new StrikeSite { country = "United Kingdom", code = "GB", lat = 51.5f, lon = -0.12f, weight = 0.45f, nuclearPower = true },
        new StrikeSite { country = "Israel", code = "IL", lat = 32.08f, lon = 34.78f, weight = 0.35f, nuclearPower = true },
        new StrikeSite { country = "France", code = "FR", lat = 48.86f, lon = 2.35f, weight = 0.3f, nuclearPower = true },
        new StrikeSite { country = "Pakistan", code = "PK", lat = 33.7f, lon = 73.0f, weight = 0.4f, nuclearPower = true },
        new StrikeSite { country = "North Korea", code = "KP", lat = 39.0f, lon = 125.8f, weight = 0.25f, nuclearPower = true },
        // visual spread
        new StrikeSite { country = "Japan", code = "JP", lat = 35.7f, lon = 139.7f, weight = 0.2f, nuclearPower = false },
        new StrikeSite { country = "South Korea", code = "KR", lat = 37.6f, lon = 127.0f, weight = 0.18f, nuclearPower = false },
        new StrikeSite { country = "Germany", code = "DE", lat = 52.5f, lon = 13.4f, weight = 0.22f, nuclearPower = false },
        new StrikeSite { country = "Brazil", code = "BR", lat = -23.5f, lon = -46.6f, weight = 0.2f, nuclearPower = false },
        new StrikeSite { country = "Australia", code = "AU", lat = -33.9f, lon = 151.2f, weight = 0.15f, nuclearPower = false },
        new StrikeSite { country = "Egypt", code = "EG", lat = 30.0f, lon = 31.2f, weight = 0.18f, nuclearPower = false },
        new StrikeSite { country = "Turkey", code = "TR", lat = 41.0f, lon = 29.0f, weight = 0.2f, nuclearPower = false },
        new StrikeSite { country = "Iran", code = "IR", lat = 35.7f, lon = 51.4f, weight = 0.22f, nuclearPower = false },
        new StrikeSite { country = "Mexico", code = "MX", lat = 19.4f, lon = -99.1f, weight = 0.2f, nuclearPower = false },
        new StrikeSite { country = "Canada", code = "CA", lat = 45.4f, lon = -75.7f, weight = 0.12f, nuclearPower = false },
        new StrikeSite { country = "Saudi Arabia", code = "SA", lat = 24.7f, lon = 46.7f, weight = 0.15f, nuclearPower = false },
        new StrikeSite { country = "Taiwan", code = "TW", lat = 25.0f, lon = 121.5f, weight = 0.12f, nuclearPower = false },
    };

    static readonly (string name, string code, float share)[] IndirectTable =
    {
        ("Canada", "CA", 0.00122f),
        ("Japan", "JP", 0.00070f),
        ("Saudi Arabia", "SA", 0.00065f),
        ("Taiwan", "TW", 0.00034f),
        ("Turkmenistan", "TM", 0.00028f),
        ("South Korea", "KR", 0.00020f),
        ("Jordan", "JO", 0.00016f),
        ("Palestine", "PS", 0.00013f),
        ("United Arab Emirates", "AE", 0.00012f),
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

    public void Abort()
    {
        StopAllCoroutines();
        IsRunning = false;
        var pop = PopulationSystem.Instance;
        if (pop != null)
            pop.GrowthPaused = false;
    }

    public bool TryStart(int units)
    {
        if (IsRunning)
            return false;
        units = Mathf.Clamp(units, 1, 500);
        StartCoroutine(RunWar(units));
        return true;
    }

    IEnumerator RunWar(int units)
    {
        IsRunning = true;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();

        var pop = PopulationSystem.Instance;
        if (pop != null)
            pop.GrowthPaused = true;

        NuclearWarReport report = BuildReport(units);
        LastReport = report;

        // 연출용 미사일 수 (통계는 타격마다 누적, 전체는 cap으로 시간 제한)
        int missileCount = Mathf.Clamp(Mathf.RoundToInt(units * 0.4f), 10, 44);
        int maxConcurrent = Mathf.Clamp(Mathf.RoundToInt(14 + units * 0.14f), 16, 32);
        float launchSpacing = Mathf.Lerp(0.055f, 0.018f, Mathf.Clamp01(units / 100f));
        const float maxWarSeconds = 7.5f;
        float warStart = Time.unscaledTime;

        long totalApplied = 0;

        int launched = 0;
        int inflight = 0;
        int impacts = 0;
        bool stopLaunching = false;

        while ((!stopLaunching && launched < missileCount) || inflight > 0)
        {
            while (!stopLaunching && launched < missileCount && inflight < maxConcurrent)
            {
                StrikeSite target = PickTarget();
                StrikeSite origin = PickLaunchSite(target);

                float tLat = target.lat + Random.Range(-2.2f, 2.2f);
                float tLon = target.lon + Random.Range(-3f, 3f);
                float oLat = origin.lat + Random.Range(-1.5f, 1.5f);
                float oLon = origin.lon + Random.Range(-2f, 2f);

                float ang = Vector3.Angle(
                    EarthGeo.LatLonToDirection(oLat, oLon),
                    EarthGeo.LatLonToDirection(tLat, tLon));
                float flight = Mathf.Lerp(0.28f, 0.72f, Mathf.Clamp01(ang / 140f));
                float power = Random.Range(0.75f, 1.65f) * Mathf.Lerp(0.85f, 1.25f, target.weight);

                inflight++;
                launched++;

                NuclearMissile.Launch(
                    earth, oLat, oLon, tLat, tLon, power, flight,
                    () =>
                    {
                        inflight = Mathf.Max(0, inflight - 1);
                        impacts++;
                        Vector3 impactWorld = earth.transform.position
                            + EarthGeo.LatLonToDirection(tLat, tLon) * earth.Radius;
                        long applied = PopulationCasualtySystem.ApplyAt(
                            earth,
                            impactWorld,
                            PopulationCasualtySystem.ScorchNormToDegrees(0.048f * power),
                            Mathf.Clamp01(0.52f + power * 0.2f),
                            power);
                        totalApplied += applied;
                    });

                if (Time.unscaledTime - warStart >= maxWarSeconds)
                    stopLaunching = true;

                float wait = launchSpacing * Random.Range(0.65f, 1.15f);
                float sim = WorldStatusHud.Instance != null ? WorldStatusHud.Instance.SimSpeed : 1f;
                yield return new WaitForSecondsRealtime(wait / Mathf.Max(0.05f, sim));
            }

            yield return null;
        }

        // 남은 미사일 연출은 생략됐을 수 있음 — 통계는 실제 착탄 누적분
        // 실제 타격 결과로 보고서 갱신
        report.totalDeaths = totalApplied;
        report.totalInjuries = (long)(totalApplied * Random.Range(1.4f, 2.6f));

        if (pop != null)
            pop.GrowthPaused = false;

        IsRunning = false;

        var reportUi = NuclearWarReportUI.Ensure();
        reportUi.Show(report);
    }

    StrikeSite PickTarget()
    {
        StrikeSite site = Sites[Random.Range(0, Sites.Length)];
        if (!site.nuclearPower && Random.value > 0.35f)
            site = Sites[Random.Range(0, 16)];
        return site;
    }

    StrikeSite PickLaunchSite(StrikeSite target)
    {
        // 핵보유국에서 다른 목표로 발사
        for (int attempt = 0; attempt < 12; attempt++)
        {
            StrikeSite origin = Sites[Random.Range(0, 16)];
            if (origin.code != target.code)
                return origin;
        }
        return Sites[0].code != target.code ? Sites[0] : Sites[6];
    }

    NuclearWarReport BuildReport(int units)
    {
        float scale = units / 100f;
        var report = new NuclearWarReport
        {
            units = units,
            mainCountryCount = 6,
            mainCityCount = Mathf.RoundToInt(100 * scale),
            totalAffectedCities = Mathf.RoundToInt(180 * scale)
        };

        // Reference-scale estimate at 100 units (실제 값은 타격 지점에 따라 달라짐)
        long estDeaths = 0;
        int estMissiles = Mathf.Clamp(Mathf.RoundToInt(units * 0.4f), 10, 44);
        for (int i = 0; i < estMissiles; i++)
        {
            StrikeSite site = Sites[Random.Range(0, Sites.Length)];
            float tLat = site.lat + Random.Range(-2.2f, 2.2f);
            float tLon = site.lon + Random.Range(-3f, 3f);
            float power = Random.Range(0.75f, 1.65f) * Mathf.Lerp(0.85f, 1.25f, site.weight);
            estDeaths += PopulationCasualtySystem.EstimateDeaths(
                tLat,
                tLon,
                PopulationCasualtySystem.ScorchNormToDegrees(0.048f * power),
                Mathf.Clamp01(0.52f + power * 0.2f),
                power);
        }

        report.totalDeaths = estDeaths;
        report.totalInjuries = (long)(estDeaths * Random.Range(1.4f, 2.6f));

        AddDirect(report, "China", "CN", (long)(1058036564L * scale), ScaleHits(25, scale), ScaleHits(3, scale));
        AddDirect(report, "India", "IN", (long)(910092392L * scale), ScaleHits(15, scale), ScaleHits(1, scale));
        AddDirect(report, "United States", "US", (long)(130403973L * scale), ScaleHits(8, scale), ScaleHits(3, scale));
        AddDirect(report, "Russia", "RU", (long)(48739245L * scale), ScaleHits(6, scale), ScaleHits(1, scale));
        AddDirect(report, "United Kingdom", "GB", (long)(42104519L * scale), ScaleHits(16, scale), ScaleHits(2, scale));
        AddDirect(report, "Israel", "IL", (long)(6028057L * scale), ScaleHits(13, scale), ScaleHits(7, scale));

        foreach (var row in IndirectTable)
        {
            long d = (long)(report.totalDeaths * row.share);
            if (d < 1000) continue;
            var c = new NuclearCountryResult
            {
                name = row.name,
                code = row.code,
                deaths = d,
                hits = 0,
                interceptions = 0,
                indirect = true,
                nonNuclear = true
            };
            report.topCountries.Add(c);
            report.nonNuclearAffected.Add(c);
        }

        // Israel secondary non-nuclear listing (fallout portion)
        report.nonNuclearAffected.Add(new NuclearCountryResult
        {
            name = "Israel",
            code = "IL",
            deaths = (long)(84359L * scale),
            hits = 0,
            interceptions = 0,
            indirect = true,
            nonNuclear = true
        });

        return report;
    }

    static int ScaleHits(int at100, float scale) => Mathf.Max(0, Mathf.RoundToInt(at100 * scale));

    static void AddDirect(NuclearWarReport report, string name, string code, long deaths, int hits, int intercepts)
    {
        report.topCountries.Add(new NuclearCountryResult
        {
            name = name,
            code = code,
            deaths = deaths,
            hits = hits,
            interceptions = intercepts,
            indirect = false,
            nonNuclear = false
        });
    }
}
