using UnityEngine;

/// <summary>
/// 지구 파괴 상태(열·크레이터·그을음)에 맞춰 인구 상한을 강제한다.
/// </summary>
public static class PopulationDestructionSync
{
    const float SyncInterval = 0.75f;
    static float lastSyncTime = -999f;

    public static float ComputeDestruction01(EarthPlanet earth)
    {
        if (earth == null)
            return 0f;

        float d = 0f;
        d += earth.Heat * 0.26f;
        d += earth.NuclearScorch * 0.20f;
        d += Mathf.InverseLerp(4f, 100f, earth.ImpactCount) * 0.10f;

        var deform = earth.GetComponent<EarthCraterDeform>();
        if (deform != null)
            d += deform.SampleCrustDamage01() * 0.28f;

        var scorch = earth.GetComponent<EarthSurfaceScorch>();
        if (scorch != null)
            d += scorch.SampleSurfaceDamage01() * 0.32f;

        return Mathf.Clamp01(d);
    }

    public static long MaxPopulationFor(EarthPlanet earth)
    {
        float destruction = ComputeDestruction01(earth);
        float habitable = Mathf.Pow(1f - destruction, 2.15f);
        if (destruction > 0.88f)
            habitable = Mathf.Min(habitable, 0.015f);
        if (destruction > 0.95f)
            habitable = 0f;

        return (long)System.Math.Floor(PopulationSystem.BaselinePopulation * habitable);
    }

    public static void EnforceCap(EarthPlanet earth, bool force = false)
    {
        if (earth == null)
            return;

        if (!force && Time.unscaledTime - lastSyncTime < SyncInterval)
            return;
        lastSyncTime = Time.unscaledTime;

        var pop = PopulationSystem.Instance;
        if (pop == null)
            return;

        long cap = MaxPopulationFor(earth);
        pop.ClampPopulation(cap);
    }
}
