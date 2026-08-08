using UnityEngine;

/// <summary>Particle ProFX One — meteor/impact/explosion (lasers use LaserVfxCatalog).</summary>
public static class ProFxParticleSpawner
{
    const string CatalogPath = "ProFxParticles/Catalog";
    const string Lib = "Library/";

    static ProFxParticleCatalog catalog;
    static bool packValidated;
    static bool packUsable;

    public static bool HasCatalog => PackUsable();

    static bool PackUsable()
    {
        if (packValidated)
            return packUsable;

        packValidated = true;
        packUsable = false;

        var cat = Resolve();
        if (cat == null)
            return false;

        var probe = cat.meteorImpact != null ? cat.meteorImpact : cat.blastMedium;
        if (probe == null)
            return false;

        packUsable = ImportedVfxMaterialFix.PrefabLooksValid(probe)
            || ImportedVfxMaterialFix.CanRuntimeFix(probe);
        return packUsable;
    }

    static ProFxParticleCatalog Resolve()
    {
        if (catalog != null)
            return catalog;

        catalog = Resources.Load<ProFxParticleCatalog>(CatalogPath);
        if (catalog == null)
            catalog = ScriptableObject.CreateInstance<ProFxParticleCatalog>();

        BindDefaults(catalog);
        return catalog;
    }

    static void BindDefaults(ProFxParticleCatalog cat)
    {
        cat.meteorProjectile = First(cat.meteorProjectile, Lib + "Fire & Explosions/ppfxMeteor");
        cat.showerProjectile = First(cat.showerProjectile, Lib + "Fire & Explosions/ppfxExplosionFireball01");
        cat.meteorTrail = First(cat.meteorTrail, Lib + "Smokes/ppfxSmokeTurbulence01");
        cat.meteorImpact = First(cat.meteorImpact, Lib + "Fire & Explosions/ppfxGroundExplosionHit");
        cat.smallMeteorImpact = First(cat.smallMeteorImpact, Lib + "Fire & Explosions/ppfxDustHit01");
        cat.blastMedium = First(cat.blastMedium, Lib + "Fire & Explosions/ppfxExplosionHeavy");
        cat.blastLarge = First(cat.blastLarge, Lib + "Fire & Explosions/ppfxExplosionBig");
        cat.blastCinematic = First(cat.blastCinematic, Lib + "Fire & Explosions/ppfxExplosionHeavyShockwave");
        cat.blastArtillery = First(cat.blastArtillery, Lib + "ChainReactions/ppfxMultipleHit");
        cat.portalSwirl = First(cat.portalSwirl, Lib + "Orbs/ppfxOrbBlueTrail");
        cat.vortexSwirl = First(cat.vortexSwirl, Lib + "Effects/ppfxLightningSphere");
        cat.vortexImpact = First(cat.vortexImpact, Lib + "Effects/ppfxRayLightning");
        cat.ufoExplosion = First(cat.ufoExplosion, cat.blastCinematic, Lib + "Fire & Explosions/ppfxExplosionHeavyShockwave");
        cat.fleetLaserBeam = First(cat.fleetLaserBeam, cat.vortexImpact, Lib + "Effects/ppfxRayLightning");
        cat.fleetLaserMuzzle = First(cat.fleetLaserMuzzle, Lib + "Fire & Explosions/ppfxFireSmall");
    }

    static GameObject First(GameObject assigned, string resourcesPath) =>
        First(assigned, null, resourcesPath);

    static GameObject First(GameObject assigned, GameObject fallback, string resourcesPath)
    {
        if (assigned != null)
            return assigned;
        if (fallback != null)
            return fallback;
        return Resources.Load<GameObject>(resourcesPath);
    }

    public static GameObject SpawnWorld(
        GameObject prefab,
        Vector3 point,
        Vector3 normal,
        float scale,
        float lifetime,
        bool loop = false,
        float minScale = 0.05f,
        float maxScale = 0.28f)
    {
        if (!PackUsable() || prefab == null)
            return null;

        normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
        var go = Object.Instantiate(prefab);
        go.name = prefab.name + "_Fx";
        go.transform.position = point + normal * 0.02f;
        go.transform.rotation = Quaternion.LookRotation(normal);
        go.transform.localScale = Vector3.one * Mathf.Clamp(scale, minScale, maxScale);
        ImportedVfxMaterialFix.FixHierarchy(go);
        PlayParticles(go, loop);

        if (lifetime > 0f)
            Object.Destroy(go, lifetime);
        return go;
    }

    public static GameObject SpawnWorldFromPath(
        string resourcesPath,
        Vector3 point,
        Vector3 normal,
        float scale,
        float lifetime,
        bool loop = false,
        float minScale = 0.05f,
        float maxScale = 0.28f)
    {
        if (string.IsNullOrEmpty(resourcesPath))
            return null;

        var prefab = First(null, Lib + resourcesPath);
        return SpawnWorld(prefab, point, normal, scale, lifetime, loop, minScale, maxScale);
    }

    public static GameObject AttachVisual(Transform parent, GameObject prefab, float scale)
    {
        if (!PackUsable() || prefab == null || parent == null)
            return null;

        var go = Object.Instantiate(prefab, parent);
        go.name = prefab.name + "_Visual";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.08f, 0.35f);
        StripColliders(go);
        ImportedVfxMaterialFix.FixHierarchy(go);
        PlayParticles(go, true);
        return go;
    }

    public static GameObject AttachTrail(Transform parent, float earthRadius)
    {
        var cat = Resolve();
        if (!PackUsable() || cat == null || cat.meteorTrail == null || parent == null)
            return null;

        var trail = Object.Instantiate(cat.meteorTrail, parent);
        trail.name = "ProFxMeteorTrail";
        trail.transform.localPosition = Vector3.zero;
        trail.transform.localRotation = Quaternion.identity;
        trail.transform.localScale = Vector3.one * Mathf.Clamp(earthRadius * 0.05f, 0.08f, 0.25f);
        StripColliders(trail);
        ImportedVfxMaterialFix.FixHierarchy(trail);
        PlayParticles(trail, true);
        return trail;
    }

    public static void AttachMeteorBody(Transform parent, bool smallShower, float earthRadius)
    {
        if (parent == null)
            return;

        EnsureRockBody(parent, smallShower, earthRadius);

        var cat = Resolve();
        if (!PackUsable() || cat == null)
            return;

        var prefab = smallShower ? cat.showerProjectile : cat.meteorProjectile;
        if (prefab == null)
            prefab = cat.meteorProjectile;
        if (prefab == null)
            return;

        float auraScale = smallShower ? earthRadius * 0.038f : earthRadius * 0.06f;
        AttachVisual(parent, prefab, auraScale);
    }

    static void EnsureRockBody(Transform parent, bool smallShower, float earthRadius)
    {
        var rend = parent.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.enabled = true;
            if (parent.localScale.sqrMagnitude < 0.04f)
            {
                float s = smallShower ? earthRadius * 0.05f : earthRadius * 0.08f;
                parent.localScale = Vector3.one * s;
            }

            return;
        }

        if (parent.Find("RockBody") != null)
            return;

        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "RockBody";
        Object.Destroy(rock.GetComponent<Collider>());
        rock.transform.SetParent(parent, false);
        float rockSize = smallShower ? earthRadius * 0.05f : earthRadius * 0.082f;
        rock.transform.localScale = Vector3.one * rockSize;
        rock.transform.localRotation = Quaternion.Euler(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f));
        rock.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.3f, 0.23f, 0.17f), 0.18f);
    }

    public static bool TryMeteorImpact(Vector3 point, Vector3 normal, float earthRadius, float damage)
    {
        if (!PackUsable())
            return false;

        var cat = Resolve();
        if (cat == null)
            return false;

        var prefab = damage >= 12f ? cat.meteorImpact : cat.smallMeteorImpact;
        if (prefab == null)
            prefab = cat.meteorImpact;
        if (prefab == null)
            return false;

        float scale = Mathf.Lerp(earthRadius * 0.06f, earthRadius * 0.14f, Mathf.Clamp01(damage / 24f));
        SpawnWorld(prefab, point, normal, scale, 4f);
        return true;
    }

    public static GameObject SpawnBeamBetween(
        GameObject prefab,
        Vector3 from,
        Vector3 to,
        float width,
        float lifetime,
        bool loop = true)
    {
        if (!PackUsable() || prefab == null)
            return null;

        Vector3 delta = to - from;
        float len = delta.magnitude;
        if (len < 0.01f)
            return null;

        Vector3 dir = delta / len;
        var go = Object.Instantiate(prefab);
        go.name = prefab.name + "_Beam";
        go.transform.position = (from + to) * 0.5f;
        go.transform.up = dir;
        float thickness = Mathf.Clamp(width, 0.035f, 0.12f);
        go.transform.localScale = new Vector3(thickness, len * 0.5f, thickness);
        ImportedVfxMaterialFix.FixHierarchy(go);
        PlayParticles(go, loop);

        if (lifetime > 0f)
            Object.Destroy(go, lifetime);
        return go;
    }

    public static System.Collections.IEnumerator FireBattleshipUfoLaser(
        Vector3 from,
        FleetUfo ufo,
        float earthRadius)
    {
        if (!PackUsable() || ufo == null)
            yield break;

        var cat = Resolve();
        if (cat == null)
            yield break;

        Vector3 to = ufo.transform.position;
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 1e-6f)
            yield break;
        dir.Normalize();

        Vector3 muzzle = from + dir * (earthRadius * 0.05f);
        var muzzlePrefab = cat.fleetLaserMuzzle;
        if (muzzlePrefab != null)
        {
            SpawnWorld(
                muzzlePrefab,
                muzzle,
                dir,
                earthRadius * 0.0075f,
                0.28f,
                minScale: 0.028f,
                maxScale: 0.05f);
        }

        var beamPrefab = cat.fleetLaserBeam != null ? cat.fleetLaserBeam : cat.vortexImpact;
        const float beamLife = 0.34f;
        float elapsed = 0f;
        GameObject beam = null;
        float beamWidth = earthRadius * 0.011f;

        while (elapsed < beamLife && ufo != null)
        {
            to = ufo.transform.position;
            if (beam != null)
                Object.Destroy(beam);

            if (beamPrefab != null)
                beam = SpawnBeamBetween(beamPrefab, muzzle, to, beamWidth, -1f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (beam != null)
            Object.Destroy(beam);

        if (ufo == null)
            yield break;

        Vector3 hit = ufo.transform.position;
        Vector3 hitNormal = (muzzle - hit).normalized;
        if (hitNormal.sqrMagnitude < 1e-6f)
            hitNormal = Vector3.up;

        if (!ufo.WillDieFrom(1f) && cat.smallMeteorImpact != null)
        {
            SpawnWorld(
                cat.smallMeteorImpact,
                hit,
                hitNormal,
                earthRadius * 0.006f,
                0.35f,
                minScale: 0.022f,
                maxScale: 0.04f);
        }

        ufo.TakeHit(1f, from);
    }

    public static bool TryUfoDestroy(Vector3 point, float earthRadius)
    {
        if (!PackUsable())
            return false;

        var cat = Resolve();
        if (cat == null)
            return false;

        var prefab = cat.ufoExplosion != null ? cat.ufoExplosion : cat.blastCinematic;
        if (prefab == null)
            prefab = First(null, Lib + "Fire & Explosions/ppfxExplosionHeavyShockwave");

        Vector3 normal = (point - Vector3.zero).sqrMagnitude > 1e-6f
            ? point.normalized
            : Vector3.up;

        return SpawnWorld(
            prefab,
            point,
            normal,
            earthRadius * 0.011f,
            0.75f,
            minScale: 0.038f,
            maxScale: 0.082f) != null;
    }

    public static bool TryStyledImpact(
        StrikeImpactKind kind,
        Vector3 point,
        Vector3 normal,
        float earthRadius,
        float intensity)
    {
        if (!PackUsable())
            return false;

        ResolveStrikeStyle(kind, intensity, out string path, out float scaleMul, out float lifetime);
        var prefab = First(null, Lib + path);
        if (prefab == null)
            return TryStrikeImpact(point, normal, earthRadius, intensity);

        float scale = earthRadius * scaleMul * Mathf.Lerp(0.55f, 0.82f, intensity);
        scale = CapFleetImpactScale(kind, earthRadius, scale);
        SpawnWorld(prefab, point, normal, scale, lifetime);
        return true;
    }

    static float CapFleetImpactScale(StrikeImpactKind kind, float earthRadius, float scale)
    {
        float maxNorm = kind switch
        {
            StrikeImpactKind.PlanetKiller => 0.048f,
            StrikeImpactKind.OrbitalCannon => 0.028f,
            StrikeImpactKind.BattleshipBeam => 0.024f,
            StrikeImpactKind.FighterStrafe => 0.018f,
            StrikeImpactKind.VonNeumannProbe => 0.016f,
            StrikeImpactKind.UfoPop => 0.018f,
            _ => 0.022f
        };
        return Mathf.Min(scale, earthRadius * maxNorm);
    }

    public static bool TryMemeBurst(
        MemeBurstStyle style,
        Vector3 point,
        Vector3 normal,
        float earthRadius,
        float intensity)
    {
        if (!PackUsable())
            return false;

        ResolveMemeBurstStyle(style, intensity, out string path, out float scaleMul, out float lifetime);
        var prefab = First(null, Lib + path);
        if (prefab == null)
            return false;

        float scale = earthRadius * scaleMul * Mathf.Lerp(0.9f, 1.2f, intensity);
        SpawnWorld(prefab, point, normal, scale, lifetime);
        return true;
    }

    static void ResolveStrikeStyle(
        StrikeImpactKind kind,
        float intensity,
        out string resourcesPath,
        out float scaleMul,
        out float lifetime)
    {
        switch (kind)
        {
            case StrikeImpactKind.FighterStrafe:
                resourcesPath = intensity >= 0.22f
                    ? "Fire & Explosions/ppfxSparkles"
                    : "Fire & Explosions/ppfxDustHit01";
                scaleMul = 0.008f + intensity * 0.006f;
                lifetime = 0.85f;
                return;
            case StrikeImpactKind.OrbitalCannon:
                resourcesPath = "Fire & Explosions/ppfxFireSmall";
                scaleMul = 0.01f + intensity * 0.006f;
                lifetime = 1.1f;
                return;
            case StrikeImpactKind.BattleshipBeam:
                resourcesPath = intensity >= 0.28f
                    ? "Fire & Explosions/ppfxFastSparksExplosion"
                    : "Fire & Explosions/ppfxSparkles";
                scaleMul = 0.009f + intensity * 0.005f;
                lifetime = 0.95f;
                return;
            case StrikeImpactKind.PlanetKiller:
                resourcesPath = "Fire & Explosions/ppfxExplosionSmall";
                scaleMul = 0.018f + intensity * 0.01f;
                lifetime = 1.6f;
                return;
            case StrikeImpactKind.VonNeumannProbe:
                resourcesPath = intensity >= 0.22f
                    ? "Fire & Explosions/ppfxSparkles"
                    : "Fire & Explosions/ppfxDustHit01";
                scaleMul = 0.007f + intensity * 0.004f;
                lifetime = 0.75f;
                return;
            case StrikeImpactKind.UfoPop:
                resourcesPath = "Fire & Explosions/ppfxExplosionHeavyShockwave";
                scaleMul = 0.009f + intensity * 0.004f;
                lifetime = 0.75f;
                return;
            case StrikeImpactKind.MemeStomp:
                resourcesPath = "Smokes/ppfxGroundDust";
                scaleMul = 0.026f + intensity * 0.018f;
                lifetime = 1.6f;
                return;
            case StrikeImpactKind.MemeSoldier:
                resourcesPath = "Fire & Explosions/ppfxExplosionHeavySimple";
                scaleMul = 0.018f + intensity * 0.012f;
                lifetime = 1.4f;
                return;
            case StrikeImpactKind.MemeTariffShot:
                resourcesPath = "Smokes/ppfxFlareSmokeOrange";
                scaleMul = 0.024f + intensity * 0.016f;
                lifetime = 1.7f;
                return;
            default:
                resourcesPath = intensity >= 0.55f
                    ? "Fire & Explosions/ppfxExplosionSmall"
                    : "Fire & Explosions/ppfxDustHit01";
                scaleMul = 0.02f + intensity * 0.014f;
                lifetime = 1.6f;
                return;
        }
    }

    static void ResolveMemeBurstStyle(
        MemeBurstStyle style,
        float intensity,
        out string resourcesPath,
        out float scaleMul,
        out float lifetime)
    {
        switch (style)
        {
            case MemeBurstStyle.RocketLaunch:
                resourcesPath = "Fire & Explosions/ppfxExplosionFireball01";
                scaleMul = 0.05f + intensity * 0.028f;
                lifetime = 2.4f;
                return;
            case MemeBurstStyle.TariffFinale:
                resourcesPath = "Fire & Explosions/ppfxExplosionHeavyFireTrail";
                scaleMul = 0.048f + intensity * 0.032f;
                lifetime = 2.8f;
                return;
            case MemeBurstStyle.MarketCrash:
                resourcesPath = "Fire & Explosions/ppfxExplosionPixel";
                scaleMul = 0.044f + intensity * 0.03f;
                lifetime = 2.6f;
                return;
            case MemeBurstStyle.ArrowSlam:
                resourcesPath = "Effects/ppfxRayRing01";
                scaleMul = 0.042f + intensity * 0.026f;
                lifetime = 2.2f;
                return;
            case MemeBurstStyle.TrojanReveal:
                resourcesPath = "Fire & Explosions/ppfxExplosionHeavyDust";
                scaleMul = 0.046f + intensity * 0.028f;
                lifetime = 2.5f;
                return;
            case MemeBurstStyle.TariffBlast:
                resourcesPath = "Fire & Explosions/ppfxFireBig";
                scaleMul = 0.034f + intensity * 0.022f;
                lifetime = 2f;
                return;
            case MemeBurstStyle.DogeCoin:
                resourcesPath = "Fire & Explosions/ppfxSparkles";
                scaleMul = 0.04f + intensity * 0.024f;
                lifetime = 2.2f;
                return;
            default:
                resourcesPath = "Fire & Explosions/ppfxExplosionSmall";
                scaleMul = 0.038f + intensity * 0.02f;
                lifetime = 2f;
                return;
        }
    }

    public static bool TryStrikeImpact(Vector3 point, Vector3 normal, float earthRadius, float intensity)
    {
        return TryStyledImpact(StrikeImpactKind.Generic, point, normal, earthRadius, intensity);
    }

    public static bool TryNuclearExplosion(Vector3 point, Vector3 normal, float power)
    {
        if (!PackUsable())
            return false;

        var cat = Resolve();
        if (cat == null)
            return false;

        GameObject prefab;
        if (power >= 1.35f)
            prefab = cat.blastLarge != null ? cat.blastLarge : cat.blastMedium;
        else if (power >= 0.85f)
            prefab = First(cat.blastMedium, Lib + "Fire & Explosions/ppfxExplosionHeavyRough");
        else
            prefab = First(cat.blastMedium, Lib + "Fire & Explosions/ppfxExplosionGasBig");
        if (prefab == null)
            return false;

        SpawnWorld(prefab, point, normal, 0.08f + power * 0.07f, 2.6f);
        return true;
    }

    public static bool TryCinematicExplosion(Vector3 point, Vector3 normal, float power)
    {
        if (!PackUsable())
            return false;

        var cat = Resolve();
        if (cat == null)
            return false;

        GameObject prefab = power >= 1.1f
            ? (cat.blastCinematic != null ? cat.blastCinematic : cat.blastMedium)
            : (cat.smallMeteorImpact != null ? cat.smallMeteorImpact : cat.blastMedium);
        if (prefab == null)
            return false;

        SpawnWorld(prefab, point, normal, 0.09f + power * 0.07f, 3.2f);
        return true;
    }

    public static GameObject AttachCosmicPortal(Transform parent, float scale)
    {
        var cat = Resolve();
        if (!PackUsable() || cat == null || cat.portalSwirl == null || parent == null)
            return null;
        return AttachVisual(parent, cat.portalSwirl, scale);
    }

    public static GameObject SpawnCosmicVortex(Vector3 point, Vector3 normal, float earthRadius)
    {
        var cat = Resolve();
        if (!PackUsable() || cat == null || cat.vortexSwirl == null)
            return null;
        return SpawnWorld(cat.vortexSwirl, point, normal, earthRadius * 0.075f, 4.2f, true);
    }

    public static GameObject SpawnCosmicVortexImpact(Vector3 point, Vector3 normal, float earthRadius)
    {
        var cat = Resolve();
        var prefab = First(cat != null ? cat.vortexImpact : null, Lib + "Effects/ppfxRayLightning");
        if (!PackUsable() || prefab == null)
            return null;
        return SpawnWorld(prefab, point, normal, earthRadius * 0.06f, 2.4f);
    }

    public static GameObject SpawnCosmicSpikeBurst(Vector3 point, Vector3 normal, float earthRadius)
    {
        var cat = Resolve();
        if (cat == null)
            return null;

        var prefab = First(cat.blastArtillery, Lib + "Effects/ppfxElectricExplosion");
        if (prefab == null || !PackUsable())
            return null;

        return SpawnWorld(prefab, point, normal, earthRadius * 0.07f, 2.5f);
    }

    static void StripColliders(GameObject go)
    {
        var cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            Object.Destroy(cols[i]);
    }

    static void PlayParticles(GameObject root, bool loop)
    {
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;
            if (loop)
            {
                var main = ps.main;
                main.loop = true;
            }
            ps.Clear(true);
            ps.Play(true);
        }
    }
}
