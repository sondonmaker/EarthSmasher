#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Import된 SF Fighter / UFO Battleship / RMB 미사일 팩을 Resources 카탈로그에 연결.</summary>
public static class FleetAssetBootstrap
{
    const string FleetCatalogPath = "Assets/Resources/Fleet/Catalog.asset";
    const string MissileCatalogPath = "Assets/Resources/NuclearMissiles/Catalog.asset";

    [MenuItem("EarthSmasher/Link All Imported Assets")]
    public static void LinkAllImportedAssets()
    {
        int linked = LinkAllImportedAssetsSilent();

        EditorUtility.DisplayDialog(
            "Asset link complete",
            linked >= 4
                ? "Fleet, Missile, Laser VFX, and ProFX Particle catalogs are ready."
                : $"Linked {linked}/4 catalogs. Import missing packages if any failed.",
            "OK");
    }

    public static int LinkAllImportedAssetsSilent()
    {
        int linked = 0;
        if (LinkFleetCatalog()) linked++;
        if (LinkMissileCatalog()) linked++;
        if (QuickEffectsPackSetup.LinkLaserCatalog()) linked++;
        if (ProFxParticlePackSetup.LinkProFxCatalog()) linked++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return linked;
    }

    [InitializeOnLoadMethod]
    static void AutoLinkOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying)
            {
                LinkFleetCatalog();
                LinkMissileCatalog();
                QuickEffectsPackSetup.LinkLaserCatalog();
                ProFxParticlePackSetup.LinkProFxCatalog();
            }
        };
    }

    static bool LinkFleetCatalog()
    {
        var battleship = FleetBattleshipSetup.FindBattleshipModel();
        var ufo = FleetUfoSetup.FindUfoModel();
        var fighter = GenericAircraftFleetSetup.FindFighter();
        var fighterAlt = GenericAircraftFleetSetup.FindFighterAlt();
        var planetKiller = GenericAircraftFleetSetup.FindPlanetKiller();
        var probe = GenericAircraftFleetSetup.FindProbe();
        var orbitalCannon = GenericAircraftFleetSetup.FindOrbitalCannon();

        if (battleship == null && ufo == null && fighter == null && planetKiller == null && probe == null
            && orbitalCannon == null)
            return false;

        FleetBattleshipSetup.EnsureResourcesFolder();
        var catalog = AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(FleetCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<FleetVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, FleetCatalogPath);
        }

        if (battleship != null)
            catalog.battleship = battleship;
        if (ufo != null)
            catalog.ufo = ufo;
        if (fighter != null)
            catalog.fighter = fighter;
        if (fighterAlt != null)
            catalog.fighterAlt = fighterAlt;
        if (planetKiller != null)
            catalog.planetKiller = planetKiller;
        if (probe != null)
            catalog.probe = probe;
        if (orbitalCannon != null)
            catalog.orbitalCannon = orbitalCannon;

        EditorUtility.SetDirty(catalog);
        return true;
    }

    static bool LinkMissileCatalog()
    {
        var variants = NuclearMissilePackSetup.FindCartoonMissilePrefabs();
        if (variants == null || variants.Length == 0)
            return false;

        NuclearMissilePackSetup.EnsureResourcesFolder();
        var catalog = AssetDatabase.LoadAssetAtPath<NuclearMissileCatalog>(MissileCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<NuclearMissileCatalog>();
            AssetDatabase.CreateAsset(catalog, MissileCatalogPath);
        }

        catalog.variants = variants;
        EditorUtility.SetDirty(catalog);
        return true;
    }
}
#endif
