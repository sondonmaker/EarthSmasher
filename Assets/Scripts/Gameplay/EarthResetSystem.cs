using UnityEngine;

/// <summary>
/// 지구 상태 초기화 — 파인 지형, 태운 지표, 관통구, 잔해 연출을 한 번에 되돌린다.
/// </summary>
public static class EarthResetSystem
{
    // 지구에 원래부터 붙어 있는 자식. 나머지 자식은 피해 연출로 보고 지운다.
    static readonly string[] PermanentChildren =
    {
        "Core",
        "Ocean",
        "Clouds",
        "Atmosphere",
        "Aurora",
        "MagneticNorth",
        "MagneticSouth",
        "CrustShards"
    };

    public static bool ResetEarth()
    {
        var earth = Object.FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;

        var deform = earth.GetComponent<EarthCraterDeform>();
        if (deform != null)
            deform.RestoreShape();

        var pierce = earth.GetComponent<EarthPierceHole>();
        if (pierce != null)
            pierce.ClearAll();

        var scorch = earth.GetComponent<EarthSurfaceScorch>();
        if (scorch != null)
            scorch.RestoreSurface();

        ClearDamageProps(earth.transform);
        earth.RestoreState();

        // 레이어 알파/야간등은 크러스트 머티리얼을 공유하므로 다시 적용
        var layers = earth.GetComponent<EarthLayerController>();
        if (layers != null)
            layers.ApplyAll();

        var pop = PopulationSystem.Instance;
        if (pop != null)
            pop.SetPopulation(PopulationSystem.BaselinePopulation);

        return true;
    }

    static void ClearDamageProps(Transform earth)
    {
        for (int i = earth.childCount - 1; i >= 0; i--)
        {
            var child = earth.GetChild(i);
            if (IsPermanent(child.name))
                continue;
            Object.Destroy(child.gameObject);
        }
    }

    static bool IsPermanent(string name)
    {
        for (int i = 0; i < PermanentChildren.Length; i++)
        {
            if (PermanentChildren[i] == name)
                return true;
        }
        return false;
    }
}
