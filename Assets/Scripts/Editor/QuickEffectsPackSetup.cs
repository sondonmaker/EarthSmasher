#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Gabriel Aguiar Free Quick Effects Vol.1 → Laser VFX catalog.</summary>
public static class QuickEffectsPackSetup
{
    const string CatalogAssetPath = "Assets/Resources/LaserVfx/Catalog.asset";
    const string PrefabRoot = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs";

    [MenuItem("EarthSmasher/Laser VFX/Link Free Quick Effects Vol.1")]
    public static void LinkQuickEffectsPack()
    {
        FleetAssetBootstrap.LinkAllImportedAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<LaserVfxCatalog>(CatalogAssetPath);
    }

    [MenuItem("EarthSmasher/Laser VFX/Import BIRP Package (extract)")]
    public static void ImportBirpPackageHint()
    {
        string path = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1_2022_BIRP_v1.0.unitypackage";
        var pkg = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (pkg == null)
        {
            EditorUtility.DisplayDialog(
                "Package not found",
                "Place FreeQuickEffectsVol1_2022_BIRP_v1.0.unitypackage under Assets/GabrielAguiarProductions/, then double-click to import.",
                "OK");
            return;
        }

        AssetDatabase.ImportPackage(path, false);
    }

    public static bool LinkLaserCatalog()
    {
        if (!HasImportedPrefabs())
            return false;

        EnsureResourcesFolder();
        var catalog = AssetDatabase.LoadAssetAtPath<LaserVfxCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LaserVfxCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.fireBeam = LoadPrefab("vfx_Flamethrower_01");
        catalog.fireImpact = LoadPrefab("vfx_Flames_01");
        catalog.iceBeam = LoadPrefab("vfx_Rain_01");
        catalog.iceImpact = LoadPrefab("vfx_Shield_01");
        catalog.pierceBeam = LoadPrefab("vfx_Hyperdrive_01");
        catalog.pierceImpact = LoadPrefab("vfx_Impact_01");
        catalog.plasmaBeam = LoadPrefab("vfx_Electricity_01");
        catalog.plasmaImpact = LoadPrefab("vfx_Projectile_02");
        catalog.lightningBeam = LoadPrefab("vfx_Lightning_01");
        catalog.lightningImpact = LoadPrefab("vfx_Lightning_02");
        catalog.sparks = LoadPrefab("vfx_Sparks_01");

        EditorUtility.SetDirty(catalog);
        return catalog.fireBeam != null || catalog.lightningBeam != null;
    }

    public static bool HasImportedPrefabs()
    {
        return LoadPrefab("vfx_Lightning_01") != null;
    }

    public static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/LaserVfx"))
            AssetDatabase.CreateFolder("Assets/Resources", "LaserVfx");
    }

    static GameObject LoadPrefab(string name)
    {
        string path = PrefabRoot + "/" + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
            return prefab;

        string[] guids = AssetDatabase.FindAssets(name + " t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string found = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!found.Contains("FreeQuickEffectsVol1"))
                continue;
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(found);
            if (prefab != null)
                return prefab;
        }

        return null;
    }
}
#endif
