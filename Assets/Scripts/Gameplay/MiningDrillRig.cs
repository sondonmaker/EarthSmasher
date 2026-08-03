using UnityEngine;

/// <summary>
/// 클릭 지점에 드릴 소환 → 회전하며 파고듦.
/// 같은 자리를 메테오처럼 반복 Dig 해서 계속 깊어짐.
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
    float digInterval = 0.28f;
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
        point = worldPoint;

        // 표면 바로 위 — 로컬 Y가 법선(밖→안 드릴)
        transform.position = point + normal * (earth.Radius * 0.08f);
        transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);

        // 샤프트
        var shaftGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(shaftGo.GetComponent<Collider>());
        shaftGo.name = "Shaft";
        shaftGo.transform.SetParent(transform, false);
        shaftGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        shaftGo.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
        shaftGo.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.45f, 0.48f, 0.52f), 0.25f);
        shaft = shaftGo.transform;

        // 드릴 비트 (뾰족)
        var bitGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(bitGo.GetComponent<Collider>());
        bitGo.name = "Bit";
        bitGo.transform.SetParent(transform, false);
        bitGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        bitGo.transform.localScale = new Vector3(0.22f, 0.45f, 0.22f);
        bitGo.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.75f, 0.55f, 0.2f), 1.2f);
        bit = bitGo.transform;

        // 상단 모터 하우징
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(head.GetComponent<Collider>());
        head.name = "Motor";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        head.transform.localScale = new Vector3(0.28f, 0.18f, 0.28f);
        head.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
            new Color(0.25f, 0.28f, 0.32f), 0.15f);

        nextDig = 0.15f;
        CameraShake.Shake(0.05f, 0.1f);

        // 첫 타공
        DigOnce(true);
    }

    void Update()
    {
        if (earth == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        spin += Time.deltaTime * (720f + digCount * 40f);

        // 비트 회전 + 파고들며 점점 아래로
        float sink = Mathf.Clamp01(age / life) * (earth.Radius * 0.12f);
        transform.position = point + normal * (earth.Radius * 0.06f - sink);

        if (bit != null)
            bit.localRotation = Quaternion.Euler(0f, spin, 0f);
        if (shaft != null)
            shaft.localRotation = Quaternion.Euler(0f, spin * 0.85f, 0f);

        // 진동
        float buzz = Mathf.Sin(age * 55f) * 0.008f * earth.Radius;
        transform.position += normal * buzz;

        if (age >= nextDig)
        {
            nextDig = age + digInterval;
            DigOnce(false);
        }

        if (age >= life)
        {
            // 마지막 한 방 더 깊게
            DigOnce(true);
            CameraShake.Shake(0.1f, 0.15f);
            Destroy(gameObject);
        }
    }

    void DigOnce(bool stronger)
    {
        digCount++;
        // 메테오 임팩트와 동일 경로 → 같은 자리 누적 파임 + 용암 흉터
        ImpactCrater.Spawn(earth.transform, point, normal, stronger ? 0.7f : 0.42f);

        if (digCount % 2 == 0)
            CameraShake.Shake(0.035f, 0.06f);
    }
}
