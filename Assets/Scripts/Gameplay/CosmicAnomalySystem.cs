using System.Collections;
using UnityEngine;

public enum CosmicAnomalyKind
{
    BlackHole,
    Vortex,
    SpikeErupt
}

/// <summary>블랙홀 / 보텍스 / 스파이크 분출.</summary>
public class CosmicAnomalySystem : MonoBehaviour
{
    public static CosmicAnomalySystem Instance { get; private set; }

    [SerializeField] Camera cam;
    [SerializeField] EarthPlanet earth;

    public bool IsAiming => false;
    public CosmicAnomalyKind AimKind { get; private set; }

    public static CosmicAnomalySystem Ensure()
    {
        var s = FindObjectOfType<CosmicAnomalySystem>();
        if (s != null)
            return s;
        return new GameObject("CosmicAnomalySystem").AddComponent<CosmicAnomalySystem>();
    }

    void Awake()
    {
        Instance = this;
        if (cam == null)
            cam = Camera.main;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SpawnAt(CosmicAnomalyKind kind, Vector3 point, Vector3 normal)
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        AimKind = kind;

        switch (kind)
        {
            case CosmicAnomalyKind.BlackHole:
                StartCoroutine(RunBlackHole(point, normal));
                break;
            case CosmicAnomalyKind.SpikeErupt:
                StartCoroutine(RunSpikeErupt(point, normal));
                break;
            default:
                StartCoroutine(RunVortex(point, normal));
                break;
        }
    }

    /// <summary>작은 블랙홀 → 점점 커지며 그 자리 지표를 안으로 삼킴.</summary>
    IEnumerator RunBlackHole(Vector3 point, Vector3 normal)
    {
        if (earth == null)
            yield break;

        // 표면 바로 위 작은 점에서 시작
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BlackHole";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = point + normal * (earth.Radius * 0.02f);
        go.transform.localScale = Vector3.one * 0.04f;

        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.01f, 0.01f, 0.02f), 0f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(ring.GetComponent<Collider>());
        ring.name = "Accretion";
        ring.transform.SetParent(go.transform, false);
        ring.transform.localScale = Vector3.one * 1.55f;
        ring.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(new Color(0.7f, 0.25f, 1f, 0.4f));
        ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        CameraShake.Shake(0.1f, 0.2f);

        var deform = EarthCraterDeform.Ensure(earth);
        var scorch = EarthSurfaceScorch.Ensure(earth);

        const float life = 5.0f;
        float t = 0f;
        float lastCarve = -1f;
        while (t < life)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / life);
            // 작 → 큼 (끝에서 약간 유지)
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.75f));
            float size = Mathf.Lerp(0.04f, earth.Radius * 0.55f, grow);
            go.transform.localScale = Vector3.one * size;
            // 점점 표면 안으로 파고듦
            go.transform.position = point + normal * (earth.Radius * Mathf.Lerp(0.02f, -0.08f, grow));
            go.transform.Rotate(normal, 90f * Time.deltaTime, Space.World);

            // 간헐 carve — Dig 누적/림 없이 안쪽으로만
            if (deform != null && t - lastCarve > 0.12f)
            {
                lastCarve = t;
                float rad = Mathf.Lerp(0.05f, 0.26f, grow);
                float depth = Mathf.Lerp(0.02f, 0.18f, grow * grow);
                deform.CarveHole(point, rad, depth);
                if (scorch != null)
                    scorch.BurnAt(point, 0.02f + 0.04f * grow, 0.9f);
            }

            yield return null;
        }

        // 마지막 한 번 더 깊게
        deform?.CarveHole(point, 0.28f, 0.2f);
        CameraShake.Shake(0.14f, 0.22f);
        Object.Destroy(go);
    }

    /// <summary>지표가 삐죽 솟는 스파이크 분출 (옛 블랙홀 버그 연출).</summary>
    IEnumerator RunSpikeErupt(Vector3 point, Vector3 normal)
    {
        if (earth == null)
            yield break;

        CameraShake.Shake(0.18f, 0.3f);

        // 짧은 경고 섬광
        var flash = new GameObject("SpikeFlash");
        flash.transform.position = point + normal * 0.3f;
        var light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.35f, 0.15f);
        light.intensity = 10f;
        light.range = earth.Radius * 2f;
        flash.AddComponent<ImpactFlashFade>().Begin(0.5f);
        Object.Destroy(flash, 0.8f);

        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
        {
            // 단계적으로 더 높이 솟아오름
            deform.SpikeErupt(point, 0.12f, 0.25f, 11);
            yield return new WaitForSecondsRealtime(0.12f);
            deform.SpikeErupt(point, 0.16f, 0.45f, 22);
            yield return new WaitForSecondsRealtime(0.12f);
            deform.SpikeErupt(point, 0.2f, 0.75f, 33);
        }

        EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, 0.05f, 0.7f);
        CameraShake.Shake(0.12f, 0.2f);
    }

    IEnumerator RunVortex(Vector3 point, Vector3 normal)
    {
        var root = new GameObject("Vortex");
        root.transform.position = point + normal * 0.2f;
        root.transform.rotation = Quaternion.LookRotation(normal);

        for (int i = 0; i < 5; i++)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float r = 0.25f + i * 0.18f;
            ring.transform.localScale = new Vector3(r, 0.02f, r);
            ring.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(
                new Color(0.25f, 0.85f, 1f, 0.45f - i * 0.06f));
        }

        CameraShake.Shake(0.08f, 0.2f);
        var deform = EarthCraterDeform.Ensure(earth);
        float t = 0f;
        float last = -1f;
        while (t < 3.8f)
        {
            t += Time.deltaTime;
            root.transform.Rotate(normal, 220f * Time.deltaTime, Space.World);
            float pulse = 1f + 0.08f * Mathf.Sin(t * 8f);
            root.transform.localScale = Vector3.one * pulse;
            if (deform != null && t - last > 0.25f)
            {
                last = t;
                deform.CarveHole(point, 0.08f, 0.03f);
            }
            yield return null;
        }

        Object.Destroy(root);
    }
}
