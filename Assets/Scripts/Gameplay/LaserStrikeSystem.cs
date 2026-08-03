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
        Vector3 origin = point + normal * (earth.Radius * 4f);
        Vector3 exitBeyond = antipode - normal * (earth.Radius * 1.5f);

        // 레퍼런스처럼 푸른 레이저 (코어/글로우/아우터)
        float holeR = earth.Radius * 0.16f;
        var core = MakeBeam("PierceBeamCore", origin, exitBeyond, new Color(0.85f, 0.98f, 1f), holeR * 0.55f, 8f);
        var glow = MakeBeam("PierceBeamGlow", origin, exitBeyond, new Color(0.15f, 0.55f, 1f), holeR * 1.05f, 5f);
        var outer = MakeBeam("PierceBeamOuter", origin, exitBeyond, new Color(0.2f, 0.45f, 1f), holeR * 1.45f, 2.2f, true);

        CameraShake.Shake(0.25f, 0.4f);

        // 즉시 깔끔한 원통 관통 + 가장자리 용암(셰이더)
        EarthPierceHole.Ensure(earth)?.AddPierce(point, antipode, holeR);

        float t = 0f;
        const float hold = 2.2f;
        while (t < hold)
        {
            t += Time.deltaTime;
            float pulse = 1f + 0.06f * Mathf.Sin(t * 28f);
            AlignBeam(core.transform, origin, exitBeyond, holeR * 0.55f * pulse);
            AlignBeam(glow.transform, origin, exitBeyond, holeR * 1.05f * pulse);
            AlignBeam(outer.transform, origin, exitBeyond, holeR * 1.45f);

            // 스파크
            if (Time.frameCount % 2 == 0)
                SpawnBlueSpark(Vector3.Lerp(origin, exitBeyond, Random.value), holeR * 0.4f);

            yield return null;
        }

        // 짧게 남았다가 페이드
        float fade = 0.7f;
        t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            float a = 1f - t / fade;
            SetBeamAlpha(core, a);
            SetBeamAlpha(glow, a * 0.8f);
            SetBeamAlpha(outer, a * 0.5f);
            yield return null;
        }

        if (core != null) Destroy(core);
        if (glow != null) Destroy(glow);
        if (outer != null) Destroy(outer);
        CameraShake.Shake(0.12f, 0.2f);
    }

    void SpawnBlueSpark(Vector3 pos, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "PierceSpark";
        go.transform.position = pos + Random.insideUnitSphere * size;
        go.transform.localScale = Vector3.one * (size * Random.Range(0.08f, 0.2f));
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.4f, 0.8f, 1f), 6f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go, 0.18f);
    }

    static void SetBeamAlpha(GameObject beam, float a)
    {
        if (beam == null)
            return;
        var rend = beam.GetComponent<Renderer>();
        if (rend == null || rend.material == null)
            return;
        var c = rend.material.color;
        c.a = Mathf.Clamp01(a);
        if (rend.material.HasProperty("_Color"))
            rend.material.SetColor("_Color", c);
        else
            rend.material.color = c;
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

    static GameObject MakeBeam(string name, Vector3 from, Vector3 to, Color color, float width, float emission = 4f, bool soft = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = name;
        var rend = go.GetComponent<Renderer>();
        if (soft)
            rend.material = RuntimeMaterial.UnlitTransparent(new Color(color.r, color.g, color.b, 0.35f));
        else
            rend.material = RuntimeMaterial.Opaque(color, emission);
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
