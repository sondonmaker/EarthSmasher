using UnityEngine;

/// <summary>
/// 임팩트 지점 그을린 자국 — 주황 공이 아니라 납작한 검은 크레이터.
/// </summary>
public class ImpactCrater : MonoBehaviour
{
    public static void Spawn(Transform earth, Vector3 worldPoint, Vector3 normal, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "CraterScorch";
        Object.Destroy(go.GetComponent<Collider>());

        go.transform.SetParent(earth, true);
        // 표면에 살짝 파묻히게
        float s = Mathf.Clamp(size * 0.55f, 0.18f, 0.38f);
        go.transform.position = worldPoint - normal * (s * 0.35f);
        go.transform.rotation = Quaternion.LookRotation(normal);
        // 납작한 접시 형태
        go.transform.localScale = new Vector3(s, s * 0.22f, s);

        // 어두운 그을음 (발광 거의 없음)
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.04f, 0.03f, 0.025f), 0f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
