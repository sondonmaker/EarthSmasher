using UnityEngine;

[CreateAssetMenu(fileName = "NuclearMissileCatalog", menuName = "EarthSmasher/Nuclear Missile Catalog")]
public class NuclearMissileCatalog : ScriptableObject
{
    public GameObject[] variants;
}

[CreateAssetMenu(fileName = "FleetCatalog", menuName = "EarthSmasher/Fleet Visual Catalog")]
public class FleetVisualCatalog : ScriptableObject
{
    public GameObject battleship;
    public GameObject ufo;

    [Header("Generic Aircraft Models — Free")]
    public GameObject fighter;
    public GameObject fighterAlt;
    public GameObject planetKiller;
    public GameObject probe;
    public GameObject orbitalCannon;
    public GameObject miningDrill;
}

[CreateAssetMenu(fileName = "ProFxParticleCatalog", menuName = "EarthSmasher/ProFX Particle Catalog")]
public class ProFxParticleCatalog : ScriptableObject
{
    [Header("Meteors (not lasers)")]
    public GameObject meteorProjectile;
    public GameObject showerProjectile;
    public GameObject meteorTrail;
    public GameObject meteorImpact;
    public GameObject smallMeteorImpact;

    [Header("Explosions")]
    public GameObject blastMedium;
    public GameObject blastLarge;
    public GameObject blastCinematic;
    public GameObject blastArtillery;

    [Header("Cosmic anomaly")]
    public GameObject portalSwirl;
    public GameObject vortexSwirl;
    public GameObject vortexImpact;

    [Header("Fleet anti-UFO")]
    public GameObject ufoExplosion;
    public GameObject fleetLaserBeam;
    public GameObject fleetLaserMuzzle;
}
