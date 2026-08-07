using UnityEngine;

/// <summary>
/// 세계 인구. 평상시 완만히 증가, 재해로 감소.
/// </summary>
public class PopulationSystem : MonoBehaviour
{
    public static PopulationSystem Instance { get; private set; }

    public const long BaselinePopulation = 8045919933L;

    [SerializeField] double population = BaselinePopulation;
    [SerializeField] double birthsPerSecond = 24.0;

    float growthResumeTime;

    public long Population => (long)System.Math.Max(0, System.Math.Floor(population));
    public bool GrowthPaused { get; set; }
    public float GrowthSpeedMultiplier { get; set; } = 1f;

    void Awake()
    {
        Instance = this;
        if (population < 1)
            population = BaselinePopulation;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (GrowthPaused || Time.unscaledTime < growthResumeTime)
            return;
        population += birthsPerSecond * GrowthSpeedMultiplier * Time.unscaledDeltaTime;
    }

    public void SetPopulation(long value) => population = System.Math.Max(0, value);

    public void ApplyCasualties(long deaths)
    {
        deaths = System.Math.Max(0, deaths);
        if (deaths <= 0)
            return;

        population = System.Math.Max(0, population - deaths);

        // 피해 직후 출생 보충 완화 — 대량 재해일수록 더 오래 멈춤
        float pauseSec = (float)System.Math.Clamp(deaths / 40_000_000.0, 3.0, 60.0);
        growthResumeTime = Mathf.Max(growthResumeTime, Time.unscaledTime + pauseSec);
    }

    public void ClampPopulation(long max)
    {
        max = System.Math.Max(0, max);
        if (Population <= max)
            return;
        population = max;
    }

    public void ResetToDefaults()
    {
        population = BaselinePopulation;
        GrowthPaused = false;
        GrowthSpeedMultiplier = 1f;
        growthResumeTime = 0f;
    }

    public static string Format(long n) => n.ToString("#,0");
}
