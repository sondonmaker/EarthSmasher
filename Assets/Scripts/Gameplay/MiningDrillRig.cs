using UnityEngine;

/// <summary>
/// 클릭 지점에 드릴 소환 → 회전하며 파고듦.
/// 림(테두리 융기) 없이 안쪽으로만 파서 구멍이 생김.
/// </summary>
public class MiningDrillRig : MonoBehaviour
{
    EarthPlanet earth;
    Vector3 point;
    Vector3 normal;
    Transform bit;
    Transform shaft;
    Transform lavaPit;
    float age;
    float life = 14f;
    float digInterval = 0.22f;
    float nextDig;
    float spin;
    int digCount;

    public static void Spawn(EarthPlanet earth, Vector3 worldPoint, Vector3 worldNormal)
    {
        if (earth == null)
            return;

        var go = new GameObject("MiningDrill");
        var rig = go.AddComponent<MiningDrillRig>();
        rig.Begin(earth, worldPoint, worldNormal.normalized);
    }

    void Begin(EarthPlanet planet, Vector3 worldPoint, Vector3 worldNormal)
    {
        earth = planet;
        normal = worldNormal.normalized;
        // 표면으로 스냅
        point = earth.transform.position + normal * earth.Radius;

        // 로컬 Y = 바깥 법선. 비트는 -Y 방향으로 파고듦
        transform.position = point + normal * (earth.Radius * 0.1f);
        transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);

        var shaftGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(shaftGo.GetComponent<Collider>());
        shaftGo.name = "Shaft";
        shaftGo.transform.SetParent(transform, false);
        shaftGo.transform.localPosition = new Vector3(0f, 0.32f, 0f);
        shaftGo.transform.localScale = new Vector3(0.1f, 0.32f, 0.1f);
        shaftGo.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.45f, 0.48f, 0.52f), 0.25f);
        shaft = shaftGo.transform;

        var bitGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(bitGo.GetComponent<Collider>());
        bitGo.name = "Bit";
        bitGo.transform.SetParent(transform, false);
        bitGo.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        bitGo.transform.localScale = new Vector3(0.2f, 0.42f, 0.2f);
        bitGo.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.85f, 0.55f, 0.15f), 1.4f);
        bit = bitGo.transform;

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(head.GetComponent<Collider>());
        head.name = "Motor";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        head.transform.localScale = new Vector3(0.26f, 0.16f, 0.26f);
        head.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.25f, 0.28f, 0.32f), 0.15f);

        // 구멍 안을 채울 용암 핏 (표면에 붙지 않고 살짝 안쪽)
        var pit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(pit.GetComponent<Collider>());
        pit.name = "LavaPit";
        pit.transform.SetParent(earth.transform, true);
        pit.transform.position = point - normal * (earth.Radius * 0.02f);
        pit.transform.localScale = Vector3.one * (earth.Radius * 0.08f);
        pit.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(1f, 0.25f, 0.05f), 3.5f);
        lavaPit = pit.transform;

        nextDig = 0.1f;
        CameraShake.Shake(0.05f, 0.1f);
        BoreOnce(0.1f, 0.06f, 0.34f);
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
        spin += Time.deltaTime * (780f + digCount * 35f);

        // 드릴이 구멍 속으로 내려감
        float sink = u * (earth.Radius * 0.18f);
        transform.position = point + normal * (earth.Radius * 0.09f - sink);

        if (bit != null)
            bit.localRotation = Quaternion.Euler(0f, spin, 0f);
        if (shaft != null)
            shaft.localRotation = Quaternion.Euler(0f, spin * 0.9f, 0f);

        float buzz = Mathf.Sin(age * 60f) * 0.004f * earth.Radius;
        transform.position += normal * buzz;

        // 용암 핏도 구멍과 함께 커지고 안으로
        if (lavaPit != null)
        {
            float pitR = earth.Radius * Mathf.Lerp(0.08f, 0.22f, u);
            lavaPit.position = point - normal * (earth.Radius * Mathf.Lerp(0.02f, 0.14f, u));
            lavaPit.localScale = Vector3.one * pitR;
        }

        if (age >= nextDig)
        {
            nextDig = age + digInterval;
            float rad = Mathf.Lerp(0.1f, 0.26f, u);
            float depth = Mathf.Lerp(0.05f, 0.2f, u);
            float floor = Mathf.Lerp(0.34f, 0.2f, u); // 점점 더 깊은 바닥
            BoreOnce(rad, depth, floor);
        }

        if (age >= life)
        {
            BoreOnce(0.28f, 0.22f, 0.2f);
            CameraShake.Shake(0.12f, 0.18f);
            // 드릴은 사라지고 용암 구멍은 남김
            Destroy(gameObject);
        }
    }

    void BoreOnce(float radiusNorm, float depthNorm, float shellFloor)
    {
        digCount++;
        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
            deform.DrillBore(point, radiusNorm, depthNorm, shellFloor);

        EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, radiusNorm * 0.9f, 0.75f);

        // 코어가 보이도록 (깊게 팠을 때)
        if (digCount >= 8)
        {
            var core = earth.transform.Find("Core");
            if (core != null && !core.gameObject.activeSelf)
                core.gameObject.SetActive(true);
        }

        if (digCount % 2 == 0)
            CameraShake.Shake(0.03f, 0.05f);
    }

    void OnDestroy()
    {
        // lavaPit은 지구 자식으로 남겨 구멍 시각 유지
    }
}
