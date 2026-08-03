using System.Collections;
using UnityEngine;

/// <summary>메테오 샤워: 짧은 시간에 여러 소행성이 지구를 타격.</summary>
public class MeteorShowerSystem : MonoBehaviour
{
    public static MeteorShowerSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;
    [SerializeField] Camera cam;
    [SerializeField] int count = 10;
    [SerializeField] float damage = 6f;

    public bool IsRunning { get; private set; }

    public static MeteorShowerSystem Ensure()
    {
        var s = FindObjectOfType<MeteorShowerSystem>();
        if (s != null)
            return s;
        return new GameObject("MeteorShowerSystem").AddComponent<MeteorShowerSystem>();
    }

    void Awake()
    {
        Instance = this;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (cam == null)
            cam = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryStart()
    {
        return TryStartAt(null);
    }

    public bool TryStartAt(Vector3? worldCenter)
    {
        if (IsRunning)
            return false;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;
        StartCoroutine(Run(worldCenter));
        return true;
    }

    IEnumerator Run(Vector3? worldCenter)
    {
        IsRunning = true;
        if (cam == null)
            cam = Camera.main;

        Vector3 face;
        if (worldCenter.HasValue)
            face = (worldCenter.Value - earth.transform.position).normalized;
        else
            face = cam != null
                ? (cam.transform.position - earth.transform.position).normalized
                : Random.onUnitSphere;

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = (face + Random.insideUnitSphere * 0.55f).normalized;
            Vector3 point = earth.transform.position + dir * earth.Radius;
            SpawnMeteor(point, dir);
            yield return new WaitForSecondsRealtime(Random.Range(0.12f, 0.28f));
        }

        yield return new WaitForSecondsRealtime(1.2f);
        IsRunning = false;
    }

    void SpawnMeteor(Vector3 point, Vector3 normal)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ShowerMeteor";
        go.transform.localScale = new Vector3(
            Random.Range(0.22f, 0.4f),
            Random.Range(0.18f, 0.34f),
            Random.Range(0.22f, 0.4f));
        Object.Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.32f, 0.24f, 0.18f), 0.2f);
        go.AddComponent<MeteorTrail>();
        var proj = go.AddComponent<MeteorProjectile>();
        proj.Launch(earth, point, normal, damage);
    }
}
