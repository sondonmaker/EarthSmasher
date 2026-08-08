#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Particle ProFX One → meteor/impact/explosion catalog (lasers stay on Quick Effects).</summary>
public static class ProFxParticlePackSetup
{
    const string CatalogAssetPath = "Assets/Resources/ProFxParticles/Catalog.asset";
    const string Root = "Assets/ParticleProFX/Resources/Library";

    [MenuItem("EarthSmasher/ProFX Particles/Link Particle ProFX One")]
    public static void LinkProFxPack()
    {
        FleetAssetBootstrap.LinkAllImportedAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<ProFxParticleCatalog>(CatalogAssetPath);
    }

    public static bool LinkProFxCatalog()
    {
        if (!HasImportedPrefabs())
            return false;

        EnsureResourcesFolder();
        var catalog = AssetDatabase.LoadAssetAtPath<ProFxParticleCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ProFxParticleCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.meteorProjectile = Load("Fire & Explosions/ppfxMeteor.prefab");
        catalog.showerProjectile = Load("Fire & Explosions/ppfxExplosionFireball01.prefab");
        catalog.meteorTrail = Load("Smokes/ppfxSmokeTurbulence01.prefab");
        catalog.meteorImpact = Load("Fire & Explosions/ppfxGroundExplosionHit.prefab");
        catalog.smallMeteorImpact = Load("Fire & Explosions/ppfxDustHit01.prefab");
        catalog.blastMedium = Load("Fire & Explosions/ppfxExplosionHeavy.prefab");
        catalog.blastLarge = Load("Fire & Explosions/ppfxExplosionBig.prefab");
        catalog.blastCinematic = Load("Fire & Explosions/ppfxExplosionHeavyShockwave.prefab");
        catalog.blastArtillery = Load("ChainReactions/ppfxMultipleHit.prefab");
        catalog.portalSwirl = Load("Orbs/ppfxOrbBlueTrail.prefab");
        catalog.vortexSwirl = Load("Effects/ppfxLightningSphere.prefab");
        catalog.vortexImpact = Load("Effects/ppfxRayLightning.prefab");
        catalog.ufoExplosion = Load("Fire & Explosions/ppfxExplosionHeavyShockwave.prefab");
        catalog.fleetLaserBeam = Load("Effects/ppfxRayLightning.prefab");
        catalog.fleetLaserMuzzle = Load("Fire & Explosions/ppfxFireSmall.prefab");

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog.meteorImpact != null || catalog.blastMedium != null;
    }

    public static bool HasImportedPrefabs()
    {
        return Load("Fire & Explosions/ppfxMeteor.prefab") != null;
    }

    public static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/ProFxParticles"))
            AssetDatabase.CreateFolder("Assets/Resources", "ProFxParticles");
    }

    static GameObject Load(string relative)
    {
        string path = Root + "/" + relative;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
            return prefab;

        string name = System.IO.Path.GetFileNameWithoutExtension(relative);
        string[] guids = AssetDatabase.FindAssets(name + " t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string found = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!found.Contains("ParticleProFX"))
                continue;
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(found);
            if (prefab != null)
                return prefab;
        }

        return null;
    }
}
#endif
