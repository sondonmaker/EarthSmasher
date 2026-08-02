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

        int blastCount = Mathf.Clamp(Mathf.RoundToInt(units * 0.85f), 12, 120);
        float duration = Mathf.Lerp(6f, 14f, units / 100f);

        long deathsLeft = report.totalDeaths;

        for (int i = 0; i < blastCount; i++)
        {
            StrikeSite site = Sites[Random.Range(0, Sites.Length)];
            // bias toward nuclear powers
            if (!site.nuclearPower && Random.value > 0.35f)
                site = Sites[Random.Range(0, 16)];

            float jitterLat = site.lat + Random.Range(-2.5f, 2.5f);
            float jitterLon = site.lon + Random.Range(-3.5f, 3.5f);
            Vector3 dir = EarthGeo.LatLonToDirection(jitterLat, jitterLon);
            float radius = earth != null ? earth.Radius : 2.5f;
            Vector3 point = (earth != null ? earth.transform.position : Vector3.zero) + dir * radius;
            Vector3 normal = dir;

            float power = Random.Range(0.7f, 1.6f) * Mathf.Lerp(0.8f, 1.25f, site.weight);
            if (earth != null)
                NuclearBlast.Play(earth, point, normal, power);

            long chunk = report.totalDeaths / blastCount;
            if (i == blastCount - 1)
                chunk = deathsLeft;
            deathsLeft -= chunk;
            if (pop != null && chunk > 0)
                pop.ApplyCasualties(chunk);

            float wait = duration / blastCount;
            wait *= Random.Range(0.55f, 1.35f);
            yield return new WaitForSeconds(wait);
        }

        if (pop != null)
        {
            // ensure report deaths applied (rounding)
            long target = PopulationSystem.BaselinePopulation - report.totalDeaths;
            // keep growth that happened before pause relative — just clamp to not exceed start-deaths
            if (pop.Population > target)
                pop.ApplyCasualties(pop.Population - target);
            pop.GrowthPaused = false;
        }

        IsRunning = false;

        var reportUi = NuclearWarReportUI.Ensure();
        reportUi.Show(report);
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

        // Reference-scale casualties at 100 units
        long baseDeaths = 2204006850L;
        long baseInjuries = 1456637425L;
        report.totalDeaths = (long)(baseDeaths * scale);
        report.totalInjuries = (long)(baseInjuries * scale);

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
