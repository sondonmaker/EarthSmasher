using System.Collections;
using UnityEngine;

public enum PlanetLaserKind
{
    Fire,      // 1 화염
    Ice,       // 2 얼음
    Pierce,    // 3 관통 (반대쪽까지)
    Plasma,    // 4 플라즈마
    Lightning  // 5 번개
}

/// <summary>5번 메뉴: 레이저. 클릭 지점으로 발사.</summary>
public class LaserStrikeSystem : MonoBehaviour
{
    public static LaserStrikeSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;
    [SerializeField] Camera cam;

    public static LaserStrikeSystem Ensure()
    {
        var s = FindObjectOfType<LaserStrikeSystem>();
        if (s != null)
            return s;
        return new GameObject("LaserStrikeSystem").AddComponent<LaserStrikeSystem>();
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

    public void FireAt(PlanetLaserKind kind, Vector3 worldPoint, Vector3 normal)
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return;
        if (cam == null)
            cam = Camera.main;

        normal = normal.normalized;
        Vector3 point = earth.transform.position + normal * earth.Radius;

        switch (kind)
        {
            case PlanetLaserKind.Fire:
                StartCoroutine(FireBeam(point, normal, new Color(1f, 0.35f, 0.05f), 0.09f, 1.6f, true));
                break;
            case PlanetLaserKind.Ice:
                StartCoroutine(IceBeam(point, normal));
                break;
            case PlanetLaserKind.Pierce:
                StartCoroutine(PierceBeam(point, normal));
                break;
            case PlanetLaserKind.Plasma:
                StartCoroutine(FireBeam(point, normal, new Color(0.85f, 0.25f, 1f), 0.11f, 1.9f, true));
                break;
            case PlanetLaserKind.Lightning:
                StartCoroutine(LightningBurst(point, normal));
                break;
        }
    }

    Vector3 BeamOrigin(Vector3 target, Vector3 normal)
    {
        if (cam != null)
        {
            Vector3 fromCam = cam.transform.position;
            // 카메라에서 타겟 쪽으로, 지구 바깥에서 시작
            Vector3 dir = (target - fromCam).normalized;
            return target - dir * (earth.Radius * 2.8f);
        }
        return target + normal * (earth.Radius * 2.5f);
    }

    IEnumerator FireBeam(Vector3 point, Vector3 normal, Color color, float width, float hold, bool dig)
    {
        Vector3 origin = BeamOrigin(point, normal);
        var beam = MakeBeam("FireLaser", origin, point, color, width);
        CameraShake.Shake(0.08f, 0.12f);

        float t = 0f;
        float digTimer = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            digTimer += Time.deltaTime;
            AlignBeam(beam.transform, origin, point, width * (0.85f + 0.15f * Mathf.Sin(t * 40f)));

            if (dig && digTimer > 0.12f)
            {
                digTimer = 0f;
                EarthCraterDeform.Ensure(earth)?.DrillBore(point, 0.1f, 0.06f, 0.3f);
                EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, 0.04f, color.r > 0.7f ? 0.85f : 0.55f);
            }
            yield return null;
        }

        // 마무리 강한 한 방
        if (dig)
        {
            EarthCraterDeform.Ensure(earth)?.DrillBore(point, 0.14f, 0.1f, 0.26f);
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, 0.06f, 0.9f);
        }
        CameraShake.Shake(0.1f, 0.15f);
        if (beam != null)
            Destroy(beam);
    }

    IEnumerator IceBeam(Vector3 point, Vector3 normal)
    {
        Vector3 origin = BeamOrigin(point, normal);
        Color ice = new Color(0.45f, 0.85f, 1f);
        var beam = MakeBeam("IceLaser", origin, point, ice, 0.08f);
        CameraShake.Shake(0.05f, 0.1f);

        // 서리 덮개
        var frost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(frost.GetComponent<Collider>());
        frost.name = "FrostPatch";
        frost.transform.position = point + normal * (earth.Radius * 0.01f);
        float s = earth.Radius * 0.12f;
        frost.transform.localScale = Vector3.one * s;
        frost.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(new Color(0.75f, 0.95f, 1f, 0.55f));

        float t = 0f;
        while (t < 1.8f)
        {
            t += Time.deltaTime;
            AlignBeam(beam.transform, origin, point, 0.08f);
            float grow = Mathf.Lerp(0.08f, 0.2f, t / 1.8f) * earth.Radius;
            frost.transform.localScale = Vector3.one * grow;
            if (Time.frameCount % 5 == 0)
            {
                // 얕게만 — 얼음은 녹아내리듯 표면 자국
                EarthCraterDeform.Ensure(earth)?.DrillBore(point, 0.08f, 0.025f, 0.34f);
                EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, 0.05f, 0.25f); // 밝게 변색 느낌
            }
            yield return null;
        }

        if (beam != null)
            Destroy(beam);
        if (frost != null)
            Destroy(frost, 2.5f);
    }

    IEnumerator PierceBeam(Vector3 point, Vector3 normal)
    {
        Vector3 center = earth.transform.position;
        Vector3 antipode = center - normal * earth.Radius;
        Vector3 origin = point + normal * (earth.Radius * 3.2f);
        Vector3 exit = antipode - normal * (earth.Radius * 0.8f);

        Color col = new Color(1f, 0.95f, 0.6f);
        var beam = MakeBeam("PierceLaser", origin, exit, col, 0.22f);
        CameraShake.Shake(0.2f, 0.35f);

        float t = 0f;
        const float hold = 2.4f;
        int steps = 0;
        while (t < hold)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / hold);
            float w = Mathf.Lerp(0.12f, 0.32f, u);
            AlignBeam(beam.transform, origin, exit, w);

            if (Time.frameCount % 3 == 0)
            {
                steps++;
                // 입구
                EarthCraterDeform.Ensure(earth)?.DrillBore(point, Mathf.Lerp(0.12f, 0.28f, u), Mathf.Lerp(0.08f, 0.26f, u), Mathf.Lerp(0.3f, 0.18f, u));
                // 출구(반대쪽)
                EarthCraterDeform.Ensure(earth)?.DrillBore(antipode, Mathf.Lerp(0.1f, 0.26f, u), Mathf.Lerp(0.07f, 0.24f, u), Mathf.Lerp(0.3f, 0.18f, u));
                // 중간축 몇 점 — 터널감
                for (int i = 1; i <= 3; i++)
                {
                    float a = i / 4f;
                    Vector3 mid = Vector3.Lerp(point, antipode, a);
                    EarthCraterDeform.Ensure(earth)?.DrillBore(mid, 0.1f * u, 0.12f * u, 0.2f);
                }

                EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, 0.08f * u, 0.95f);
                EarthSurfaceScorch.Ensure(earth)?.BurnAt(antipode, 0.08f * u, 0.95f);
            }
            yield return null;
        }

        // 최종 관통
        EarthCraterDeform.Ensure(earth)?.DrillBore(point, 0.3f, 0.28f, 0.18f);
        EarthCraterDeform.Ensure(earth)?.DrillBore(antipode, 0.3f, 0.28f, 0.18f);
        var core = earth.transform.Find("Core");
        if (core != null)
            core.gameObject.SetActive(true);

        CameraShake.Shake(0.28f, 0.4f);
        if (beam != null)
            Destroy(beam);
    }

    IEnumerator LightningBurst(Vector3 point, Vector3 normal)
    {
        CameraShake.Shake(0.12f, 0.2f);
        Vector3 origin = BeamOrigin(point, normal);

        for (int bolt = 0; bolt < 7; bolt++)
        {
            Vector3 jitter = (normal + Random.insideUnitSphere * 0.35f).normalized;
            Vector3 hit = earth.transform.position + jitter * earth.Radius;
            var beam = MakeBeam("Lightning", origin + Random.insideUnitSphere * (earth.Radius * 0.3f), hit,
                new Color(1f, 0.95f, 0.4f), 0.035f);
            EarthCraterDeform.Ensure(earth)?.DrillBore(hit, 0.06f, 0.04f, 0.32f);
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, 0.025f, 0.6f);
            Destroy(beam, 0.12f);
            yield return new WaitForSecondsRealtime(0.05f);
        }

        // 중심 타격
        var main = MakeBeam("LightningMain", origin, point, new Color(1f, 1f, 0.7f), 0.07f);
        EarthCraterDeform.Ensure(earth)?.DrillBore(point, 0.12f, 0.08f, 0.28f);
        yield return new WaitForSecondsRealtime(0.25f);
        if (main != null)
            Destroy(main);
    }

    static GameObject MakeBeam(string name, Vector3 from, Vector3 to, Color color, float width)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = name;
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(color, 4f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AlignBeam(go.transform, from, to, width);
        return go;
    }

    static void AlignBeam(Transform beam, Vector3 from, Vector3 to, float width)
    {
        if (beam == null)
            return;
        Vector3 mid = (from + to) * 0.5f;
        float len = Vector3.Distance(from, to) * 0.5f;
        beam.position = mid;
        beam.up = (to - from).normalized;
        beam.localScale = new Vector3(width, Mathf.Max(0.01f, len), width);
    }
}
