#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Free SF Fighter(Asset Store #11711) → Fleet catalog.</summary>
public static class FleetBattleshipSetup
{
    const string LegacyPrefabPath = "Assets/Resources/Fleet/Battleship.prefab";

    [MenuItem("EarthSmasher/Fleet/Link SF Fighter Battleship")]
    public static void LinkSfFighterBattleship()
    {
        var model = FindBattleshipModel();
        if (model == null)
        {
            EditorUtility.DisplayDialog(
                "SF Fighter not found",
                "Import Free SF Fighter (#11711) into Assets first.",
                "OK");
            return;
        }

        FleetAssetBootstrap.LinkAllImportedAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(
            "Assets/Resources/Fleet/Catalog.asset");
    }

    public static GameObject FindBattleshipModel()
    {
        string[] names = { "SF_Free-Fighter", "SF_Fighter" };
        for (int n = 0; n < names.Length; n++)
        {
            string[] guids = AssetDatabase.FindAssets(names[n] + " t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.Contains("/Resources/Fleet/"))
                    continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                    return go;
            }

            guids = AssetDatabase.FindAssets(names[n] + " t:GameObject");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.Contains("/Resources/Fleet/"))
                    continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                    return go;
            }
        }

        return null;
    }

    public static void EnsureResourcesFolder()
    {
        EnsureFolder("Assets/Resources/Fleet");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
