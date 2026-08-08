#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>EvSeStudio Drill "Soviet" → Resources/Fleet + catalog.</summary>
public static class MiningDrillSetup
{
    const string DrillPrefabPath =
        "Assets/EvSeStudio/3D_Art/Tools/Drills_Vol_01/Drill_01/Prefabs/SM_Drill_01.prefab";
    const string ResourcesPrefabPath = "Assets/Resources/Fleet/SovietDrill.prefab";
    const string FleetCatalogPath = "Assets/Resources/Fleet/Catalog.asset";

    [MenuItem("EarthSmasher/Fleet/Link Soviet Drill")]
    public static void LinkSovietDrill()
    {
        if (FindSovietDrill() == null)
        {
            EditorUtility.DisplayDialog(
                "Soviet Drill not found",
                "Import Drill \"Soviet\" (EvSeStudio) into Assets first.",
                "OK");
            return;
        }

        EnsureResourcesCopy();
        FleetAssetBootstrap.LinkAllImportedAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(FleetCatalogPath);
    }

    public static GameObject FindSovietDrill()
    {
        var direct = AssetDatabase.LoadAssetAtPath<GameObject>(DrillPrefabPath);
        if (direct != null)
            return direct;

        string[] guids = AssetDatabase.FindAssets("SM_Drill_01 t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith("SM_Drill_01.prefab"))
                continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
                return go;
        }

        return null;
    }

    public static void EnsureResourcesCopy()
    {
        FleetUfoSetup.EnsureResourcesFolder();
        var source = FindSovietDrill();
        if (source == null)
            return;

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesPrefabPath);
        if (existing == null)
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), ResourcesPrefabPath))
                Debug.LogWarning("[MiningDrill] Failed to copy Soviet drill into Resources/Fleet.");
        }

        var catalog = AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(FleetCatalogPath);
        if (catalog == null)
            return;

        var linked = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesPrefabPath);
        if (linked != null)
            catalog.miningDrill = linked;

        EditorUtility.SetDirty(catalog);
    }
}
#endif
