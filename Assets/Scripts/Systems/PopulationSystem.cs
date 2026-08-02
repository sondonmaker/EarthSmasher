using UnityEngine;

/// <summary>
/// 세계 인구. 평상시 완만히 증가, 재해로 감소.
/// </summary>
public class PopulationSystem : MonoBehaviour
{
    public static PopulationSystem Instance { get; private set; }

    public const long BaselinePopulation = 8045919933L;

    [SerializeField] double population = BaselinePopulation;
    [SerializeField] double birthsPerSecond = 85.0;

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
        if (GrowthPaused)
            return;
        population += birthsPerSecond * GrowthSpeedMultiplier * Time.unscaledDeltaTime;
    }

    public void SetPopulation(long value) => population = System.Math.Max(0, value);

    public void ApplyCasualties(long deaths)
    {
        population = System.Math.Max(0, population - System.Math.Max(0, deaths));
    }

    public static string Format(long n) => n.ToString("#,0");
}
