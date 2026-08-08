using UnityEngine;

/// <summary>
/// Soviet 거대 드릴 — 표면에 수직으로 서 있고, 옛 Pepe 펀치처럼 메시만 깊게 파냄 (폭발 없음).
/// </summary>
public class MiningDrillRig : MonoBehaviour
{
    EarthPlanet earth;
    Vector3 point;
    Vector3 normal;
    Transform[] spinParts;
    Quaternion[] spinBase;
    float mountOffset;
    float age;
    float life = 12f;
    float digInterval = 0.2f;
    float nextDig;
    float spin;
    int digCount;

    public static void Spawn(EarthPlanet earth, Vector3 worldPoint, Vector3 worldNormal)
    {
        if (earth == null)
            return;

        for (int i = earth.transform.childCount - 1; i >= 0; i--)
        {
            var ch = earth.transform.GetChild(i);
            if (ch.name == "LavaPit")
                Object.Destroy(ch.gameObject);
        }

        var go = new GameObject("MiningDrill");
        var rig = go.AddComponent<MiningDrillRig>();
        rig.Begin(earth, worldPoint, worldNormal.normalized);
    }

    void Begin(EarthPlanet planet, Vector3 worldPoint, Vector3 worldNormal)
    {
        earth = planet;
        normal = worldNormal.normalized;
        point = earth.transform.position + normal * earth.Radius;

        // +Y = 지구 밖(법선), 드릴 비트는 표면(y≈0), 몸통은 +Y
        transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);

        var visual = MiningDrillVisuals.TryBuild(transform, earth.Radius);
        if (visual.root != null)
        {
            spinParts = visual.spinParts;
            mountOffset = ComputeMountOffset(visual.root);
        }
        else
        {
            BuildFallbackVisual(earth.Radius);
            Debug.LogWarning("[MiningDrill] Soviet drill prefab missing — primitive fallback.");
        }

        CacheSpinBase();
        PlaceOnSurface();
        nextDig = 0.08f;
        CameraShake.Shake(0.04f, 0.08f);
        BoreOnce(0f);
    }

    float ComputeMountOffset(Transform visualRoot)
    {
        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return earth.Radius * 0.002f;

        float maxY = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            var b = renderers[i].bounds;
            Vector3 topLocal = transform.InverseTransformPoint(b.max);
            maxY = Mathf.Max(maxY, topLocal.y);
        }

        // 부모 원점 = 표면 접점, 몸통이 +Y로 솟음
        return earth.Radius * 0.0015f;
    }

    void BuildFallbackVisual(float earthRadius)
    {
        float unit = earthRadius * 0.13f;
        mountOffset = earthRadius * 0.0015f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(body.GetComponent<Collider>());
        body.name = "DrillBody";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, unit * 1.05f, 0f);
        body.transform.localScale = new Vector3(unit * 0.7f, unit * 1.05f, unit * 0.7f);
        body.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(0.72f, 0.22f, 0.1f), 0.05f);

        var bitGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(bitGo.GetComponent<Collider>());
        bitGo.name = "Bit";
        bitGo.transform.SetParent(transform, false);
        bitGo.transform.localPosition = new Vector3(0f, -unit * 0.18f, 0f);
        bitGo.transform.localScale = new Vector3(unit * 0.55f, unit * 0.22f, unit * 0.55f);
        bitGo.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(0.55f, 0.55f, 0.58f), 0.15f);

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(head.GetComponent<Collider>());
        head.name = "Motor";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, unit * 2.35f, 0f);
        head.transform.localScale = new Vector3(unit * 1.1f, unit * 0.65f, unit * 1.1f);
        head.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(0.22f, 0.24f, 0.28f), 0.1f);

        spinParts = new[] { bitGo.transform };
    }

    void CacheSpinBase()
    {
        if (spinParts == null || spinParts.Length == 0)
            return;

        spinBase = new Quaternion[spinParts.Length];
        for (int i = 0; i < spinParts.Length; i++)
            spinBase[i] = spinParts[i] != null ? spinParts[i].localRotation : Quaternion.identity;
    }

    void PlaceOnSurface()
    {
        // 드릴은 지구 밖에 서 있음 — 안으로 가라앉히지 않음
        transform.position = point + normal * mountOffset;
    }

    void Update()
    {
        if (earth == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        float u = Mathf.Clamp01(age / life);
        spin += Time.deltaTime * (820f + digCount * 30f);

        PlaceOnSurface();

        if (spinParts != null && spinBase != null)
        {
            var spinRot = Quaternion.AngleAxis(spin, Vector3.up);
            for (int i = 0; i < spinParts.Length; i++)
            {
                if (spinParts[i] != null)
                    spinParts[i].localRotation = spinBase[i] * spinRot;
            }
        }

        if (age >= nextDig)
        {
            nextDig = age + digInterval;
            BoreOnce(u);
        }

        if (age >= life)
        {
            BoreOnce(1f);
            ApplyFinisherDig();
            CameraShake.Shake(0.1f, 0.12f);
            Destroy(gameObject);
        }
    }

    /// <summary>옛 Pepe 펀치 — DrillBore + 그을음, 폭발/먼지 없음.</summary>
    void BoreOnce(float progress)
    {
        digCount++;
        var deform = EarthCraterDeform.Ensure(earth);
        int priorHits = deform != null ? deform.GetSiteHitCount(point) : 0;

        float rad = Mathf.Lerp(0.06f, 0.22f, progress) + priorHits * 0.006f;
        float depth = Mathf.Lerp(0.05f, 0.28f, progress) + priorHits * 0.034f;
        float floor = Mathf.Lerp(0.34f, 0.16f, progress) - priorHits * 0.024f;
        floor = Mathf.Max(floor, 0.1f);

        if (deform != null)
            deform.DrillBore(point, rad, depth, floor, widenOnRepeat: false);

        EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, rad * 0.75f, 0.55f + progress * 0.25f);

        float depth01 = deform != null ? deform.GetSiteDepth01(point) : progress;
        if (depth01 > 0.18f && digCount % 3 == 0)
        {
            var scorch = EarthSurfaceScorch.Ensure(earth);
            scorch?.PaintDeepOreInterior(point, rad * 0.85f, depth01, point.GetHashCode() ^ digCount, lite: depth01 < 0.5f);
            if (depth01 > 0.55f && digCount % 6 == 0)
                scorch?.FlushTexture();
        }

        if (digCount % 4 == 0)
        {
            PopulationCasualtySystem.ApplyAt(
                earth,
                point,
                PopulationCasualtySystem.DigNormToDegrees(rad),
                0.08f,
                0.55f);
        }

        if (priorHits + 1 >= 10)
        {
            var core = earth.transform.Find("Core");
            if (core != null)
                core.gameObject.SetActive(true);
        }

        if (digCount % 3 == 0)
            CameraShake.Shake(0.022f + progress * 0.028f, 0.038f);
    }

    void ApplyFinisherDig()
    {
        var deform = EarthCraterDeform.Ensure(earth);
        deform?.DrillBore(point, 0.24f, 0.3f, 0.15f, widenOnRepeat: false);

        float depth01 = deform != null ? Mathf.Max(0.7f, deform.GetSiteDepth01(point)) : 0.85f;
        var scorch = EarthSurfaceScorch.Ensure(earth);
        scorch?.PaintDeepOreInterior(point, 0.2f, depth01, 991);
        scorch?.FlushTexture();
    }
}
