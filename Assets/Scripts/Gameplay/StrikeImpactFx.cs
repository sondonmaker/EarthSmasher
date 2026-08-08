using UnityEngine;

public enum StrikeImpactKind
{
    Generic,
    FighterStrafe,
    OrbitalCannon,
    BattleshipBeam,
    PlanetKiller,
    VonNeumannProbe,
    UfoPop,
    MemeStomp,
    MemeSoldier,
    MemeTariffShot,
    OrePunch
}

public enum MemeBurstStyle
{
    RocketLaunch,
    TariffFinale,
    MarketCrash,
    ArrowSlam,
    TrojanReveal,
    TariffBlast,
    DogeCoin
}

/// <summary>함대/밈 연사 타격 — 종류별 ProFX + 색/흔들림 분리 (핵미사일과 다른 연출).</summary>
public static class StrikeImpactFx
{
    public static void Play(
        EarthPlanet earth,
        Vector3 point,
        Vector3 normal,
        float intensity = 0.4f,
        StrikeImpactKind kind = StrikeImpactKind.Generic)
    {
        intensity = Mathf.Clamp(intensity, 0.12f, 0.95f);
        float R = earth != null ? earth.Radius : 2.5f;
        normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;

        if (!ProFxParticleSpawner.TryStyledImpact(kind, point, normal, R, intensity))
            SpawnTinyFlash(point, normal, R * FlashScale(kind, intensity), FlashColor(kind));

        MaybeShockwave(kind, point, normal, R, intensity);
        CameraShake.Shake(ShakeAmp(kind, intensity), ShakeDur(kind, intensity));

        if (earth != null)
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, BurnRadius(kind, intensity), BurnDark(kind));
    }

    static void MaybeShockwave(StrikeImpactKind kind, Vector3 point, Vector3 normal, float R, float intensity)
    {
        float threshold = kind switch
        {
            StrikeImpactKind.FighterStrafe => 0.82f,
            StrikeImpactKind.OrbitalCannon => 0.78f,
            StrikeImpactKind.BattleshipBeam => 0.8f,
            StrikeImpactKind.UfoPop => 0.95f,
            StrikeImpactKind.MemeSoldier => 0.55f,
            StrikeImpactKind.OrePunch => 0.28f,
            _ => 0.5f
        };
        if (intensity < threshold)
            return;

        float baseSize = kind switch
        {
            StrikeImpactKind.OrbitalCannon => 0.06f,
            StrikeImpactKind.BattleshipBeam => 0.05f,
            StrikeImpactKind.PlanetKiller => 0.09f,
            StrikeImpactKind.MemeStomp => 0.11f,
            StrikeImpactKind.OrePunch => 0.13f,
            StrikeImpactKind.MemeTariffShot => 0.13f,
            StrikeImpactKind.FighterStrafe => 0.035f,
            StrikeImpactKind.VonNeumannProbe => 0.03f,
            _ => 0.05f
        };
        ImpactShockwave.Spawn(point, normal, R * (baseSize + intensity * 0.025f));
    }

    static float FlashScale(StrikeImpactKind kind, float intensity) =>
        (kind switch
        {
            StrikeImpactKind.OrbitalCannon => 0.028f,
            StrikeImpactKind.BattleshipBeam => 0.024f,
            StrikeImpactKind.PlanetKiller => 0.034f,
            StrikeImpactKind.VonNeumannProbe => 0.016f,
            StrikeImpactKind.MemeStomp => 0.026f,
            StrikeImpactKind.OrePunch => 0.038f,
            StrikeImpactKind.UfoPop => 0.02f,
            _ => 0.022f
        }) * intensity;

    static Color FlashColor(StrikeImpactKind kind) => kind switch
    {
        StrikeImpactKind.FighterStrafe => new Color(1f, 0.58f, 0.12f, 0.52f),
        StrikeImpactKind.OrbitalCannon => new Color(1f, 0.28f, 0.08f, 0.58f),
        StrikeImpactKind.BattleshipBeam => new Color(1f, 0.88f, 0.22f, 0.5f),
        StrikeImpactKind.PlanetKiller => new Color(1f, 0.35f, 0.08f, 0.62f),
        StrikeImpactKind.VonNeumannProbe => new Color(0.55f, 0.85f, 1f, 0.45f),
        StrikeImpactKind.UfoPop => new Color(0.45f, 0.92f, 1f, 0.55f),
        StrikeImpactKind.MemeStomp => new Color(0.35f, 0.72f, 1f, 0.48f),
        StrikeImpactKind.OrePunch => new Color(1f, 0.42f, 0.06f, 0.62f),
        StrikeImpactKind.MemeSoldier => new Color(0.92f, 0.72f, 0.38f, 0.45f),
        StrikeImpactKind.MemeTariffShot => new Color(1f, 0.42f, 0.08f, 0.5f),
        _ => new Color(1f, 0.72f, 0.28f, 0.5f)
    };

    static float ShakeAmp(StrikeImpactKind kind, float intensity) => kind switch
    {
        StrikeImpactKind.OrbitalCannon => 0.022f + intensity * 0.032f,
        StrikeImpactKind.BattleshipBeam => 0.02f + intensity * 0.03f,
        StrikeImpactKind.PlanetKiller => 0.04f + intensity * 0.05f,
        StrikeImpactKind.VonNeumannProbe => 0.008f + intensity * 0.012f,
        StrikeImpactKind.MemeStomp => 0.024f + intensity * 0.028f,
        StrikeImpactKind.OrePunch => 0.038f + intensity * 0.048f,
        StrikeImpactKind.FighterStrafe => 0.012f + intensity * 0.018f,
        _ => 0.016f + intensity * 0.024f
    };

    static float ShakeDur(StrikeImpactKind kind, float intensity) => kind switch
    {
        StrikeImpactKind.MemeStomp => 0.03f + intensity * 0.03f,
        StrikeImpactKind.OrePunch => 0.04f + intensity * 0.038f,
        StrikeImpactKind.FighterStrafe => 0.02f + intensity * 0.025f,
        _ => 0.024f + intensity * 0.032f
    };

    static float BurnRadius(StrikeImpactKind kind, float intensity) => kind switch
    {
        StrikeImpactKind.OrbitalCannon => 0.006f * intensity,
        StrikeImpactKind.FighterStrafe => 0.005f * intensity,
        StrikeImpactKind.BattleshipBeam => 0.008f * intensity,
        StrikeImpactKind.PlanetKiller => 0.014f * intensity,
        StrikeImpactKind.VonNeumannProbe => 0.005f * intensity,
        StrikeImpactKind.MemeStomp => 0.016f * intensity,
        StrikeImpactKind.OrePunch => 0.012f * intensity,
        StrikeImpactKind.MemeTariffShot => 0.018f * intensity,
        _ => 0.006f * intensity
    };

    static float BurnDark(StrikeImpactKind kind) => kind switch
    {
        StrikeImpactKind.UfoPop => 0.35f,
        StrikeImpactKind.MemeStomp => 0.38f,
        StrikeImpactKind.OrePunch => 0.52f,
        StrikeImpactKind.MemeSoldier => 0.48f,
        _ => 0.42f
    };

    static void SpawnTinyFlash(Vector3 point, Vector3 normal, float radius, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "StrikeFlash";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = point + normal * 0.015f;
        go.transform.localScale = Vector3.one * Mathf.Max(radius, 0.025f);
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.UnlitTransparent(col);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.Destroy(go, 0.16f);
    }
}
