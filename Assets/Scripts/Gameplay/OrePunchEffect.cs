using UnityEngine;

/// <summary>
/// Pepe 펀치형 깊은 광석 파괴 — BOOM 메뉴 "Ore Punch" 프리셋.
/// 렉 줄이기: 플러리 중엔 좁은 반경·드문 텍스처, 마무리에만 넓게+용암 광석.
/// </summary>
public static class OrePunchEffect
{
    public const string WeaponId = "ore_punch";

    // 저장된 프리셋 (Pepe 펀치 마무리 기준, 최적화 버전)
    const int BoomBoreSteps = 5;
    const float BoomFinalRadius = 0.19f;
    const float BoomFinalDepth = 0.24f;
    const float BoomFinalFloor = 0.18f;

    /// <summary>BOOM → Ore Punch: 클릭 지점에 깊은 광석 크레이터.</summary>
    public static void ApplyBoom(EarthPlanet earth, Vector3 worldPoint, Vector3 normal)
    {
        if (earth == null)
            return;

        Vector3 hit = SnapSurface(earth, worldPoint);
        normal = (hit - earth.transform.position).normalized;

        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
        {
            for (int i = 0; i < BoomBoreSteps; i++)
            {
                float t = i / (float)Mathf.Max(1, BoomBoreSteps - 1);
                float rad = Mathf.Lerp(0.07f, BoomFinalRadius, t);
                float dep = Mathf.Lerp(0.045f, BoomFinalDepth, t);
                float floor = Mathf.Lerp(0.33f, BoomFinalFloor, t);
                bool widen = i >= BoomBoreSteps - 2;
                deform.DrillBore(hit, rad, dep, floor, widen);
            }
        }

        ApplyOrePaint(earth, hit, BoomFinalRadius, 1f, lite: false, seed: hit.GetHashCode() ^ 77);
        earth.ApplyImpact(hit, 4f);
        PopulationCasualtySystem.ApplyAt(
            earth,
            hit,
            PopulationCasualtySystem.DigNormToDegrees(BoomFinalRadius),
            0.42f,
            0.95f);
        PlayExplosionFx(earth, hit, normal, 0.72f);
    }

    /// <summary>Pepe 플러리 — 메시만 (홀수 타격마다, 반경 고정으로 주변 밀림 방지).</summary>
    public static void ApplyFlurryDeform(EarthPlanet earth, Vector3 hit, float progress)
    {
        if (earth == null)
            return;

        var deform = EarthCraterDeform.Ensure(earth);
        if (deform == null)
            return;

        float rad = Mathf.Lerp(0.055f, 0.14f, progress);
        float depth = Mathf.Lerp(0.035f, 0.16f, progress);
        float floor = Mathf.Lerp(0.34f, 0.24f, progress);
        deform.DrillBore(hit, rad, depth, floor, widenOnRepeat: false);
    }

    /// <summary>Pepe 플러리 — 작은 폭발 (먼지 대신).</summary>
    public static void ApplyFlurryBurn(EarthPlanet earth, Vector3 hit, float progress)
    {
        if (earth == null)
            return;

        Vector3 normal = (hit - earth.transform.position).normalized;
        PlayExplosionFx(earth, hit, normal, Mathf.Lerp(0.3f, 0.5f, progress));
    }

    /// <summary>Pepe 플러리 — 드문 광석 텍스처 (lite).</summary>
    public static void ApplyFlurryOrePaint(EarthPlanet earth, Vector3 hit, float progress, int seed)
    {
        float rad = Mathf.Lerp(0.055f, 0.14f, progress);
        float depth01 = Mathf.Max(progress * 0.55f, EarthCraterDeform.Ensure(earth)?.GetSiteDepth01(hit) ?? progress);
        ApplyOrePaint(earth, hit, rad, depth01, lite: true, seed: seed);
    }

    /// <summary>Pepe 마무리 — 깊게 + 용암 광석 풀 품질.</summary>
    public static void ApplyPepeFinisher(EarthPlanet earth, Vector3 hit, Vector3 normal)
    {
        if (earth == null)
            return;

        var deform = EarthCraterDeform.Ensure(earth);
        deform?.DrillBore(hit, 0.2f, 0.26f, 0.17f, widenOnRepeat: true);

        float depth01 = deform != null ? Mathf.Max(0.75f, deform.GetSiteDepth01(hit)) : 0.85f;
        ApplyOrePaint(earth, hit, 0.19f, depth01, lite: false, seed: 991);
        PopulationCasualtySystem.ApplyAt(
            earth,
            hit,
            PopulationCasualtySystem.DigNormToDegrees(0.05f),
            0.45f,
            0.95f);
        PlayExplosionFx(earth, hit, normal, 0.9f);
    }

    /// <summary>드릴 틱 — Pepe 플러리와 동일한 깊은 광석 파괴.</summary>
    public static void ApplyDrillTick(EarthPlanet earth, Vector3 hit, Vector3 normal, float progress, int tick, int seed)
    {
        if (earth == null)
            return;

        ApplyFlurryDeform(earth, hit, progress);

        if (tick % 2 == 0)
            ApplyFlurryBurn(earth, hit, progress);

        if (tick % 3 == 2)
            ApplyFlurryOrePaint(earth, hit, progress, seed);
    }

    /// <summary>드릴 마무리 — Pepe 펀치 피니셔급 광석+폭발.</summary>
    public static void ApplyDrillFinisher(EarthPlanet earth, Vector3 hit, Vector3 normal)
    {
        ApplyPepeFinisher(earth, hit, normal);
    }

    /// <summary>광석 펀치 전용 폭발 — ProFX fire/explosion (먼지 없음).</summary>
    public static void PlayExplosionFx(EarthPlanet earth, Vector3 hit, Vector3 normal, float intensity)
    {
        if (earth == null)
            return;

        StrikeImpactFx.Play(earth, hit, normal, intensity, StrikeImpactKind.OrePunch);
        MemeAttackSystem.SpawnFlash(
            hit,
            normal,
            earth.Radius * (0.026f + intensity * 0.024f),
            new Color(1f, 0.4f, 0.06f, 0.52f));
    }

    static void ApplyOrePaint(EarthPlanet earth, Vector3 hit, float radiusNorm, float depth01, bool lite, int seed)
    {
        var scorch = EarthSurfaceScorch.Ensure(earth);
        if (scorch == null)
            return;

        if (depth01 > 0.55f && !lite)
            scorch.PaintDeepOreInterior(hit, radiusNorm * 0.92f, depth01, seed);
        else if (depth01 > 0.12f)
            scorch.PaintDeepOreInterior(hit, radiusNorm * (lite ? 0.72f : 0.88f), depth01, seed, lite);

        if (!lite)
            scorch.FlushTexture();
    }

    static Vector3 SnapSurface(EarthPlanet earth, Vector3 worldPoint)
    {
        Vector3 center = earth.transform.position;
        Vector3 radial = (worldPoint - center).normalized;
        if (radial.sqrMagnitude < 1e-6f)
            radial = Vector3.up;
        return center + radial * earth.Radius;
    }
}
