using UnityEngine;

/// <summary>
/// 클릭 지점에 드릴 소환 → 회전하며 지표를 안쪽으로 뚫음.
/// (용암 스피어/풍선 없음 — DrillBore만 사용)
/// </summary>
public class MiningDrillRig : MonoBehaviour
{
    EarthPlanet earth;
    Vector3 point;
    Vector3 normal;
    Transform bit;
    Transform shaft;
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

        // 이전 잘못된 용암 풍선 정리
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

        // 드릴 크기는 지구 대비 작게 (월드 단위)
        float R = earth.Radius;
        float unit = R * 0.045f;

        transform.position = point + normal * (unit * 2.2f);
        transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);

        var shaftGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(shaftGo.GetComponent<Collider>());
        shaftGo.name = "Shaft";
        shaftGo.transform.SetParent(transform, false);
        shaftGo.transform.localPosition = new Vector3(0f, unit * 1.6f, 0f);
        shaftGo.transform.localScale = new Vector3(unit * 0.55f, unit * 1.5f, unit * 0.55f);
        shaftGo.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.4f, 0.42f, 0.46f), 0.2f);
        shaft = shaftGo.transform;

        var bitGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(bitGo.GetComponent<Collider>());
        bitGo.name = "Bit";
        bitGo.transform.SetParent(transform, false);
        bitGo.transform.localPosition = new Vector3(0f, -unit * 0.2f, 0f);
        bitGo.transform.localScale = new Vector3(unit * 1.1f, unit * 0.9f, unit * 1.1f);
        bitGo.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.55f, 0.55f, 0.58f), 0.35f);
        bit = bitGo.transform;

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(tip.GetComponent<Collider>());
        tip.name = "Tip";
        tip.transform.SetParent(bit, false);
        tip.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        tip.transform.localScale = new Vector3(0.85f, 0.55f, 0.85f);
        tip.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.7f, 0.45f, 0.15f), 0.8f);

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(head.GetComponent<Collider>());
        head.name = "Motor";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, unit * 3.4f, 0f);
        head.transform.localScale = new Vector3(unit * 1.6f, unit * 0.9f, unit * 1.6f);
        head.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.22f, 0.24f, 0.28f), 0.1f);

        nextDig = 0.08f;
        CameraShake.Shake(0.04f, 0.08f);
        BoreOnce(0f);
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
        spin += Time.deltaTime * (900f + digCount * 40f);

        float R = earth.Radius;
        float unit = R * 0.045f;
        // 파고들수록 드릴이 지표 아래로
        float sink = u * (R * 0.14f);
        transform.position = point + normal * (unit * 2.0f - sink);

        if (bit != null)
            bit.localRotation = Quaternion.Euler(0f, spin, 0f);
        if (shaft != null)
            shaft.localRotation = Quaternion.Euler(0f, spin * 0.85f, 0f);

        transform.position += normal * (Mathf.Sin(age * 70f) * unit * 0.08f);

        if (age >= nextDig)
        {
            nextDig = age + digInterval;
            BoreOnce(u);
        }

        if (age >= life)
        {
            BoreOnce(1f);
            CameraShake.Shake(0.1f, 0.14f);
            Destroy(gameObject);
        }
    }

    void BoreOnce(float progress)
    {
        digCount++;
        // 안쪽으로만 — 풍선/림 없음
        float rad = Mathf.Lerp(0.09f, 0.24f, progress);
        float depth = Mathf.Lerp(0.06f, 0.24f, progress);
        float floor = Mathf.Lerp(0.32f, 0.2f, progress);

        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
            deform.DrillBore(point, rad, depth, floor);

        EarthSurfaceScorch.Ensure(earth)?.BurnAt(point, rad * 0.85f, 0.7f);

        if (digCount % 4 == 0)
        {
            PopulationCasualtySystem.ApplyAt(
                earth,
                point,
                PopulationCasualtySystem.DigNormToDegrees(rad),
                0.08f,
                0.55f);
        }

        if (digCount >= 10)
        {
            var core = earth.transform.Find("Core");
            if (core != null)
                core.gameObject.SetActive(true);
        }

        if (digCount % 3 == 0)
            CameraShake.Shake(0.025f, 0.04f);
    }
}
