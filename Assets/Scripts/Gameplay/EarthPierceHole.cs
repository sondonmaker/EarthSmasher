using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피어싱 레이저 구멍: 셰이더 clip으로 반대쪽이 보이게 + 용암 터널.
/// </summary>
public class EarthPierceHole : MonoBehaviour
{
    public static EarthPierceHole Ensure(EarthPlanet earth)
    {
        if (earth == null)
            return null;
        var h = earth.GetComponent<EarthPierceHole>();
        if (h == null)
            h = earth.gameObject.AddComponent<EarthPierceHole>();
        h.earth = earth;
        h.BindMaterial();
        return h;
    }

    struct Hole
    {
        public Vector3 origin;
        public Vector3 axis;
        public float radius;
        public GameObject tunnel;
        public GameObject rimA;
        public GameObject rimB;
    }

    EarthPlanet earth;
    readonly List<Hole> holes = new List<Hole>();
    Material crustMat;
    const int MaxHoles = 4;

    void BindMaterial()
    {
        if (earth == null)
            earth = GetComponent<EarthPlanet>();
        var rend = GetComponent<Renderer>();
        if (rend != null)
            crustMat = rend.material;
    }

    public void AddPierce(Vector3 entryWorld, Vector3 exitWorld, float radiusWorld)
    {
        BindMaterial();
        Vector3 center = earth.transform.position;
        Vector3 axis = (exitWorld - entryWorld).normalized;
        if (axis.sqrMagnitude < 1e-6f)
            axis = (entryWorld - center).normalized;

        // 최대 개수 초과 시 가장 오래된 것 제거
        while (holes.Count >= MaxHoles)
        {
            DestroyHoleVisuals(holes[0]);
            holes.RemoveAt(0);
        }

        float r = Mathf.Max(radiusWorld, earth.Radius * 0.12f);
        var hole = new Hole
        {
            origin = center,
            axis = axis,
            radius = r,
            tunnel = BuildLavaTunnel(center, axis, r),
            rimA = BuildLavaRim(entryWorld, (entryWorld - center).normalized, r * 1.35f),
            rimB = BuildLavaRim(exitWorld, (exitWorld - center).normalized, r * 1.35f)
        };
        holes.Add(hole);
        PushToShader();

        // 메시도 입/출구를 깊게 파서 구멍감 강화
        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
        {
            deform.DrillBore(entryWorld, 0.32f, 0.28f, 0.18f);
            deform.DrillBore(exitWorld, 0.32f, 0.28f, 0.18f);
        }
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(entryWorld, 0.14f, 0.95f);
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(exitWorld, 0.14f, 0.95f);

        var core = earth.transform.Find("Core");
        if (core != null)
            core.gameObject.SetActive(false); // 관통 시 코어가 구멍을 막지 않게
    }

    void PushToShader()
    {
        if (crustMat == null || !crustMat.HasProperty("_PierceCount"))
            BindMaterial();
        if (crustMat == null)
            return;

        crustMat.SetInt("_PierceCount", holes.Count);
        for (int i = 0; i < MaxHoles; i++)
        {
            if (i < holes.Count)
            {
                crustMat.SetVector("_PierceOrigin" + i, holes[i].origin);
                crustMat.SetVector("_PierceAxis" + i, holes[i].axis);
                crustMat.SetFloat("_PierceRadius" + i, holes[i].radius);
            }
            else
            {
                crustMat.SetVector("_PierceOrigin" + i, Vector4.zero);
                crustMat.SetVector("_PierceAxis" + i, Vector4.zero);
                crustMat.SetFloat("_PierceRadius" + i, 0f);
            }
        }
    }

    GameObject BuildLavaTunnel(Vector3 center, Vector3 axis, float radius)
    {
        var root = new GameObject("PierceLavaTunnel");
        root.transform.SetParent(earth.transform, true);
        root.transform.position = center;
        root.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);

        Vector3 ax = axis.normalized;
        Vector3 right = Vector3.Cross(ax, Vector3.up);
        if (right.sqrMagnitude < 1e-4f)
            right = Vector3.Cross(ax, Vector3.right);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, ax).normalized;

        // 구멍 벽에만 용암 점들 — 가운데는 비워서 반대쪽이 보임
        int rings = 10;
        int segs = 14;
        float blob = radius * 0.22f;
        for (int i = 0; i < rings; i++)
        {
            float u = (i + 0.5f) / rings;
            float along = Mathf.Lerp(-earth.Radius * 0.92f, earth.Radius * 0.92f, u);
            Vector3 ringCenter = center + ax * along;
            for (int s = 0; s < segs; s++)
            {
                float ang = (s / (float)segs) * Mathf.PI * 2f;
                Vector3 pos = ringCenter + (right * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * (radius * 0.95f);
                var bit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(bit.GetComponent<Collider>());
                bit.name = "LavaBit";
                bit.transform.SetParent(root.transform, true);
                bit.transform.position = pos;
                bit.transform.localScale = Vector3.one * blob;
                var rend = bit.GetComponent<Renderer>();
                float heat = 0.65f + 0.35f * Mathf.Sin(u * Mathf.PI);
                rend.material = RuntimeMaterial.Opaque(new Color(1f, 0.22f + 0.3f * heat, 0.04f), 3.2f + heat * 2.5f);
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        return root;
    }

    GameObject BuildLavaRim(Vector3 worldPos, Vector3 outward, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "PierceLavaRim";
        go.transform.SetParent(earth.transform, true);
        go.transform.position = worldPos - outward * (earth.Radius * 0.02f);
        float inv = 1f / Mathf.Max(1e-4f, earth.transform.lossyScale.x);
        // 납작한 용암 구덩이
        go.transform.localScale = new Vector3(radius * 2.1f * inv, radius * 0.55f * inv, radius * 2.1f * inv);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, outward);
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(1f, 0.3f, 0.05f), 4.5f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    void DestroyHoleVisuals(Hole h)
    {
        if (h.tunnel != null)
            Object.Destroy(h.tunnel);
        if (h.rimA != null)
            Object.Destroy(h.rimA);
        if (h.rimB != null)
            Object.Destroy(h.rimB);
    }

    void OnDestroy()
    {
        for (int i = 0; i < holes.Count; i++)
            DestroyHoleVisuals(holes[i]);
        holes.Clear();
        if (crustMat != null && crustMat.HasProperty("_PierceCount"))
            crustMat.SetInt("_PierceCount", 0);
    }
}
