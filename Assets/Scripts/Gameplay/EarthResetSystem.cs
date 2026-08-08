using UnityEngine;

/// <summary>
/// 전체 세션 초기화 — 지형/설정/연출/무기/재해를 새 게임 시작 상태로 되돌린다.
/// </summary>
public static class EarthResetSystem
{
    static readonly string[] PermanentChildren =
    {
        "Core",
        "Mantle",
        "Ocean",
        "Clouds",
        "Atmosphere",
        "Aurora",
        "MagneticNorth",
        "MagneticSouth",
        "CrustShards"
    };

    static readonly string[] LeftoverObjectNames =
    {
        "MissilePath", "SpaceNukeMissile", "NuclearMissile",
        "FighterWing", "VonNeumannSwarm", "MiningDrill", "OrbitalCannon",
        "Battleship", "UFO", "PlanetKiller", "Laser", "Beam",
        "CinematicExplosion", "ShockwaveRing", "NukeFlash", "ImpactFlash",
        "EventMoon", "MoonDebris", "MoonDust", "ImpactDustVeil", "MoonFlash",
        "MoltenCrackWave", "MoltenRibbon", "MemeShark", "MemeCaption", "MemeTicker",
        "Vortex", "SpikeFlash", "ZeusFlash", "ClawSwipe", "CrashArrow",
        "PierceMantleBore", "MeteorGlow", "ImpactFlash", "Exhaust", "MissileVisual",
        "SF_Fighter", "UFO_Battleship", "FallbackCapsule"
    };

    public static bool ResetEarth()
    {
        var earth = Object.FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;

        AbortActiveSystems();
        ClearWorldEffects();
        ResetUiAndSettings(earth);

        var deform = earth.GetComponent<EarthCraterDeform>();
        if (deform != null)
            deform.RestoreShape();

        var pierce = earth.GetComponent<EarthPierceHole>();
        if (pierce != null)
            pierce.ClearAll();

        var scorch = earth.GetComponent<EarthSurfaceScorch>();
        if (scorch != null)
            scorch.RestoreSurface();

        ClearDamageProps(earth.transform);
        earth.RestoreState();

        var layers = earth.GetComponent<EarthLayerController>();
        if (layers != null)
        {
            layers.ResetToDefaults();
            layers.ApplyAll();
        }

        var pop = PopulationSystem.Instance;
        if (pop != null)
            pop.ResetToDefaults();

        EarthSaveSystem.DeleteSave();
        return true;
    }

    static void AbortActiveSystems()
    {
        Object.FindObjectOfType<NuclearWarSystem>()?.Abort();
        Object.FindObjectOfType<EarthquakeSystem>()?.Abort();
        Object.FindObjectOfType<MoonImpactSystem>()?.Abort();
        Object.FindObjectOfType<MeteorShowerSystem>()?.Abort();
        Object.FindObjectOfType<SpacecraftFleetSystem>()?.Abort();
        Object.FindObjectOfType<GreekMythAttackSystem>()?.Abort();
        Object.FindObjectOfType<MemeAttackSystem>()?.Abort();
        Object.FindObjectOfType<LaserStrikeSystem>()?.Abort();
        Object.FindObjectOfType<CosmicAnomalySystem>()?.Abort();
        NuclearMissileStrike.Instance?.CancelAim();
    }

    static void ClearWorldEffects()
    {
        DestroyAll<NuclearMissile>();
        DestroyAll<MeteorProjectile>();
        DestroyAll<MiningDrillRig>();
        DestroyAll<FleetUfo>();
        DestroyAll<FleetBattleship>();
        DestroyAll<MemeUnitBase>();
        DestroyImportedVfxLeftovers();

        for (int i = 0; i < LeftoverObjectNames.Length; i++)
            DestroyByName(LeftoverObjectNames[i]);

        DismissModalUi();
    }

    static void ResetUiAndSettings(EarthPlanet earth)
    {
        WorldStatusHud.Instance?.ResetToDefaults();
        WeaponRailPanel.Instance?.ResetArming();
        Object.FindObjectOfType<EarthControlPanel>()?.ResetToDefaults();
        EarthSettingsPanel.Instance?.ResetToDefaults();

        var body = earth.GetComponent<EarthBodyData>();
        if (body != null)
        {
            body.RotationMultiplier = 1f;
            body.RotationEnabled = true;
        }

        Object.FindObjectOfType<OrbitCamera>()?.ResetToDefaults();
    }

    static void DismissModalUi()
    {
        DestroyUi<EarthquakeReportUI>();
        DestroyUi<NuclearWarReportUI>();
        DestroyUi<MoonImpactReportUI>();
        DestroyUi<EarthquakeConfirmUI>();
    }

    static void DestroyUi<T>() where T : Object
    {
        var ui = Object.FindObjectOfType<T>();
        if (ui != null)
            Object.Destroy((ui as MonoBehaviour).gameObject);
    }

    static void DestroyImportedVfxLeftovers()
    {
        var all = Object.FindObjectsOfType<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            string n = all[i].name;
            if (n.StartsWith("ppfx") || n.Contains("ProFxMeteorTrail")
                || n.EndsWith("_Fx") || n.EndsWith("_Visual")
                || n.EndsWith("_Beam") || n.EndsWith("_Impact") || n.StartsWith("vfx_"))
                Object.Destroy(all[i].gameObject);
        }
    }

    static void DestroyAll<T>() where T : MonoBehaviour
    {
        var items = Object.FindObjectsOfType<T>();
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                Object.Destroy(items[i].gameObject);
        }
    }

    static void DestroyByName(string objectName)
    {
        var all = Object.FindObjectsOfType<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                Object.Destroy(all[i].gameObject);
        }
    }

    static void ClearDamageProps(Transform earth)
    {
        for (int i = earth.childCount - 1; i >= 0; i--)
        {
            var child = earth.GetChild(i);
            if (IsPermanent(child.name))
                continue;
            Object.Destroy(child.gameObject);
        }
    }

    static bool IsPermanent(string name)
    {
        for (int i = 0; i < PermanentChildren.Length; i++)
        {
            if (PermanentChildren[i] == name)
                return true;
        }
        return false;
    }
}
