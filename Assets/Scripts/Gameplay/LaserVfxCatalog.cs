using UnityEngine;

[CreateAssetMenu(fileName = "LaserVfxCatalog", menuName = "EarthSmasher/Laser VFX Catalog")]
public class LaserVfxCatalog : ScriptableObject
{
    [Header("Beam / sustained")]
    public GameObject fireBeam;
    public GameObject iceBeam;
    public GameObject pierceBeam;
    public GameObject plasmaBeam;
    public GameObject lightningBeam;

    [Header("Impact / burst")]
    public GameObject fireImpact;
    public GameObject iceImpact;
    public GameObject pierceImpact;
    public GameObject plasmaImpact;
    public GameObject lightningImpact;
    public GameObject sparks;
}
