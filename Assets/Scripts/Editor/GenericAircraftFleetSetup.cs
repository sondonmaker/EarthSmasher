#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Generic Aircraft Models — Free → Fleet catalog.</summary>
public static class GenericAircraftFleetSetup
{
    const string AircraftFolder = "Assets/Generic Aircraft Models/Prefabs/Aircrafts";

    [MenuItem("EarthSmasher/Fleet/Link Generic Aircraft Models")]
    public static void LinkGenericAircraft()
    {
        if (FindFighter() == null)
        {
            EditorUtility.DisplayDialog(
                "Generic Aircraft not found",
                "Import Generic Aircraft Models — Free into Assets first.",
                "OK");
            return;
        }

        FleetAssetBootstrap.LinkAllImportedAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(
            "Assets/Resources/Fleet/Catalog.asset");
    }

    public static GameObject FindAircraft(string prefabName)
    {
        var direct = AssetDatabase.LoadAssetAtPath<GameObject>($"{AircraftFolder}/{prefabName}.prefab");
        if (direct != null)
            return direct;

        string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith($"/{prefabName}.prefab"))
                continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
                return go;
        }

        return null;
    }

    public static GameObject FindFighter() => FindAircraft("aircraft-f");

    public static GameObject FindFighterAlt() => FindAircraft("aircraft-c");

    public static GameObject FindPlanetKiller() => FindAircraft("aircraft-k");

    public static GameObject FindProbe() => FindAircraft("aircraft-a");

    public static GameObject FindOrbitalCannon() => FindAircraft("aircraft-h");
}
#endif
