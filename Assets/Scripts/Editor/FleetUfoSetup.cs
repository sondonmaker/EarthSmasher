#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>FlexUnit UFO Battleship(Asset Store #289193) → Fleet catalog.</summary>
public static class FleetUfoSetup
{
    [MenuItem("EarthSmasher/Fleet/Link UFO Battleship")]
    public static void LinkUfoBattleship()
    {
        var model = FindUfoModel();
        if (model == null)
        {
            EditorUtility.DisplayDialog(
                "UFO Battleship not found",
                "Import UFO Battleship (#289193) into Assets first.",
                "OK");
            return;
        }

        FleetAssetBootstrap.LinkAllImportedAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(
            "Assets/Resources/Fleet/Catalog.asset");
    }

    public static GameObject FindUfoModel()
    {
        string[] preferredPaths =
        {
            "Assets/FlexUnit/UFO_Battleship/Built-In/Prefabs/UFO_Color1.prefab",
            "Assets/FlexUnit/UFO_Battleship/URP/Prefabs/UFO_Color1.prefab",
            "Assets/FlexUnit/UFO_Battleship/HDRP/Prefabs/UFO_Color1.prefab"
        };

        for (int i = 0; i < preferredPaths.Length; i++)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(preferredPaths[i]);
            if (go != null)
                return go;
        }

        string[] guids = AssetDatabase.FindAssets("UFO_Color1 t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.Contains("/Resources/Fleet/"))
                continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
                return go;
        }

        guids = AssetDatabase.FindAssets("UFO_Battleship t:GameObject");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
                return go;
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
