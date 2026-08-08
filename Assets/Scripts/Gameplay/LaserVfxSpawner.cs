using UnityEngine;

/// <summary>Gabriel Aguiar Free Quick Effects Vol.1 prefab spawn helper for laser strikes.</summary>
public static class LaserVfxSpawner
{
    const string CatalogPath = "LaserVfx/Catalog";

    static LaserVfxCatalog catalog;

    public static bool HasCatalog => ResolveCatalog() != null;

    static LaserVfxCatalog ResolveCatalog()
    {
        if (catalog != null)
            return catalog;
        catalog = Resources.Load<LaserVfxCatalog>(CatalogPath);
        return catalog;
    }

    public static GameObject SpawnBeam(
        GameObject prefab,
        Vector3 from,
        Vector3 to,
        float scaleMul = 1f,
        float lifetime = -1f)
    {
        if (prefab == null)
            return null;

        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.01f)
            return null;
        dir /= len;

        var go = Object.Instantiate(prefab);
        go.name = prefab.name + "_Beam";
        go.transform.position = from;
        go.transform.rotation = Quaternion.LookRotation(dir);
        go.transform.localScale = Vector3.one * Mathf.Clamp(scaleMul, 0.06f, 0.28f);
        ImportedVfxMaterialFix.FixHierarchy(go);
        PlayAllParticles(go, true);

        if (lifetime > 0f)
            Object.Destroy(go, lifetime);
        return go;
    }

    public static GameObject SpawnImpact(
        GameObject prefab,
        Vector3 point,
        Vector3 normal,
        float scaleMul = 1f,
        float lifetime = 2.5f)
    {
        if (prefab == null)
            return null;

        normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
        var go = Object.Instantiate(prefab);
        go.name = prefab.name + "_Impact";
        go.transform.position = point + normal * 0.02f;
        go.transform.rotation = Quaternion.LookRotation(normal);
        go.transform.localScale = Vector3.one * Mathf.Clamp(scaleMul, 0.06f, 0.28f);
        ImportedVfxMaterialFix.FixHierarchy(go);
        PlayAllParticles(go, false);

        if (lifetime > 0f)
            Object.Destroy(go, lifetime);
        return go;
    }

    public static GameObject ForKind(PlanetLaserKind kind, bool impact)
    {
        var cat = ResolveCatalog();
        if (cat == null)
            return null;

        if (impact)
        {
            return kind switch
            {
                PlanetLaserKind.Fire => cat.fireImpact,
                PlanetLaserKind.Ice => cat.iceImpact,
                PlanetLaserKind.Pierce => cat.pierceImpact,
                PlanetLaserKind.Plasma => cat.plasmaImpact,
                PlanetLaserKind.Lightning => cat.lightningImpact,
                PlanetLaserKind.Sustain => cat.sparks != null ? cat.sparks : cat.fireImpact,
                _ => null
            };
        }

        return kind switch
        {
            PlanetLaserKind.Fire => cat.fireBeam,
            PlanetLaserKind.Ice => cat.iceBeam,
            PlanetLaserKind.Pierce => cat.pierceBeam,
            PlanetLaserKind.Plasma => cat.plasmaBeam,
            PlanetLaserKind.Lightning => cat.lightningBeam,
            PlanetLaserKind.Sustain => cat.plasmaBeam != null ? cat.plasmaBeam : cat.fireBeam,
            _ => null
        };
    }

    public static GameObject SparksPrefab()
    {
        var cat = ResolveCatalog();
        return cat != null ? cat.sparks : null;
    }

    public static GameObject ForBattleshipUfoBolt()
    {
        var cat = ResolveCatalog();
        if (cat == null)
            return null;
        if (cat.projectileBolt != null)
            return cat.projectileBolt;
        if (cat.pierceBeam != null)
            return cat.pierceBeam;
        return cat.plasmaBeam;
    }

    public static void SpawnMuzzleFlash(Vector3 at, Vector3 toward, float scale)
    {
        var cat = ResolveCatalog();
        if (cat == null || cat.muzzleFlash == null)
            return;

        Vector3 dir = toward - at;
        var go = Object.Instantiate(cat.muzzleFlash);
        go.name = cat.muzzleFlash.name + "_Muzzle";
        go.transform.position = at;
        if (dir.sqrMagnitude > 1e-6f)
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);
        go.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.06f, 0.22f);
        ImportedVfxMaterialFix.FixHierarchy(go);
        PlayAllParticles(go, false);
        Object.Destroy(go, 0.35f);
    }

    public static GameObject PierceImpactPrefab()
    {
        return ForKind(PlanetLaserKind.Pierce, impact: true);
    }

    public static GameObject ForFleetStrike(StrikeImpactKind kind, bool impact)
    {
        var cat = ResolveCatalog();
        if (cat == null)
            return null;

        if (impact)
        {
            return kind switch
            {
                StrikeImpactKind.FighterStrafe => cat.sparks,
                StrikeImpactKind.OrbitalCannon => cat.sparks != null ? cat.sparks : cat.fireImpact,
                StrikeImpactKind.BattleshipBeam => cat.sparks,
                StrikeImpactKind.PlanetKiller => cat.fireImpact != null ? cat.fireImpact : cat.sparks,
                StrikeImpactKind.VonNeumannProbe => cat.sparks,
                StrikeImpactKind.UfoPop => cat.fireImpact != null
                    ? cat.fireImpact
                    : cat.sparks,
                _ => cat.sparks
            };
        }

        return kind switch
        {
            StrikeImpactKind.FighterStrafe => cat.pierceBeam != null ? cat.pierceBeam : cat.fireBeam,
            StrikeImpactKind.OrbitalCannon => cat.plasmaBeam != null ? cat.plasmaBeam : cat.fireBeam,
            StrikeImpactKind.BattleshipBeam => cat.pierceBeam != null ? cat.pierceBeam : cat.fireBeam,
            StrikeImpactKind.PlanetKiller => cat.lightningBeam != null ? cat.lightningBeam : cat.plasmaBeam,
            StrikeImpactKind.VonNeumannProbe => cat.pierceBeam != null ? cat.pierceBeam : cat.iceBeam,
            StrikeImpactKind.UfoPop => cat.iceBeam != null ? cat.iceBeam : cat.plasmaBeam,
            _ => cat.fireBeam
        };
    }

    public static float FleetBeamScale(StrikeImpactKind kind) => kind switch
    {
        StrikeImpactKind.FighterStrafe => 0.045f,
        StrikeImpactKind.OrbitalCannon => 0.06f,
        StrikeImpactKind.BattleshipBeam => 0.055f,
        StrikeImpactKind.PlanetKiller => 0.09f,
        StrikeImpactKind.VonNeumannProbe => 0.035f,
        StrikeImpactKind.UfoPop => 0.035f,
        _ => 0.055f
    };

    /// <summary>함대 타격점 — 클릭 지점에 작게 모인 임팩트.</summary>
    public static float FleetImpactScale(StrikeImpactKind kind) => kind switch
    {
        StrikeImpactKind.FighterStrafe => 0.022f,
        StrikeImpactKind.OrbitalCannon => 0.028f,
        StrikeImpactKind.BattleshipBeam => 0.026f,
        StrikeImpactKind.PlanetKiller => 0.042f,
        StrikeImpactKind.VonNeumannProbe => 0.018f,
        StrikeImpactKind.UfoPop => 0.012f,
        _ => 0.024f
    };

    public static void AimBeam(Transform beam, Vector3 from, Vector3 to, float scaleMul)
    {
        if (beam == null)
            return;

        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 1e-6f)
            return;

        beam.position = from;
        beam.rotation = Quaternion.LookRotation(dir.normalized);
        beam.localScale = Vector3.one * Mathf.Clamp(scaleMul, 0.06f, 0.28f);
    }

    static void PlayAllParticles(GameObject root, bool loop)
    {
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;
            var main = ps.main;
            if (loop)
                main.loop = true;
            ps.Clear(true);
            ps.Play(true);
        }
    }
}
