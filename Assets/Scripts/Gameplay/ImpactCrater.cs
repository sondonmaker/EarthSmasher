using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 충돌: 움푹 파인 지형 + 맞은 면은 AmbientCG 용암으로 둥글게 채움.
/// </summary>
public class ImpactCrater : MonoBehaviour
{
    public static void Spawn(Transform earthTf, Vector3 worldPoint, Vector3 normal, float size)
    {
        if (earthTf == null)
            return;

        var earth = earthTf.GetComponent<EarthPlanet>();
        if (earth == null)
            earth = earthTf.GetComponentInParent<EarthPlanet>();

        float radiusNorm = Mathf.Clamp(size * 0.12f, 0.045f, 0.16f);
        Apply(earth, worldPoint, normal, radiusNorm, false);
    }

    public static void SpawnHuge(EarthPlanet earth, Vector3 worldPoint, float radiusNorm = 0.2f, float depthNorm = 0.1f)
    {
        if (earth == null)
            return;
        Vector3 normal = (worldPoint - earth.transform.position).normalized;
        Apply(earth, worldPoint, normal, Mathf.Clamp(radiusNorm, 0.14f, 0.24f), true);
    }

    static void Apply(EarthPlanet earth, Vector3 worldPoint, Vector3 normal, float radiusNorm, bool huge)
    {
        if (earth == null)
            return;

        normal = normal.normalized;

        // 움푹 파기 (지각+바다 같이 — 파란 속살 안 보이게)
        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
            deform.Stamp(worldPoint, radiusNorm, huge ? 0.09f : 0.045f);

        // 맞은 부분 = 구면에 붙는 둥근 용암 패치
        SpawnLavaCap(earth.transform, normal, radiusNorm, huge);

        var scorch = EarthSurfaceScorch.Ensure(earth);
        if (scorch != null)
            scorch.PaintImpactCrater(worldPoint, radiusNorm * 1.15f);
    }

    /// <summary>
    /// 지구 곡면을 따라가는 원형 용암 캡 (평평한 원판 클리핑 없음).
    /// </summary>
    static void SpawnLavaCap(Transform earth, Vector3 normal, float radiusNorm, bool huge)
    {
        Vector3 localN = earth.InverseTransformDirection(normal).normalized;
        if (localN.sqrMagnitude < 1e-6f)
            localN = Vector3.up;

        const float meshR = 0.5f;
        float ang = Mathf.Clamp(radiusNorm * 1.05f, 0.08f, 0.35f);

        var go = new GameObject(huge ? "LavaHitHuge" : "LavaHit");
        go.transform.SetParent(earth, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var mf = go.AddComponent<MeshFilter>();
        // 분지 안쪽에 살짝 들어가게 + 테두리는 표면 근처
        mf.sharedMesh = BuildSphericalCap(40, 14, localN, meshR, ang, huge ? 0.012f : 0.008f);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateLavaFillMaterial(huge);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
    }

    /// <summary>
    /// impactDir 주변 원뿔 각도의 구면 캡. bowlInset만큼 중심으로 갈수록 더 들어감.
    /// </summary>
    static Mesh BuildSphericalCap(int seg, int rings, Vector3 impactDir, float radius, float angleRad, float bowlInset)
    {
        seg = Mathf.Clamp(seg, 16, 72);
        rings = Mathf.Clamp(rings, 6, 24);
        impactDir.Normalize();

        // basis
        Vector3 tangent = Vector3.Cross(impactDir, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(impactDir, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(impactDir, tangent).normalized;

        int vertCount = 1 + rings * (seg + 1);
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        // center
        float centerR = radius - bowlInset;
        verts[0] = impactDir * centerR;
        norms[0] = impactDir;
        uvs[0] = new Vector2(0.5f, 0.5f);

        int vi = 1;
        for (int r = 1; r <= rings; r++)
        {
            float rt = r / (float)rings;
            float a = angleRad * rt;
            // 가장자리는 덜 파고, 중심은 더 팜
            float inset = bowlInset * (1f - rt * rt);
            float rr = radius - inset;
            float ringSin = Mathf.Sin(a);
            float ringCos = Mathf.Cos(a);

            for (int s = 0; s <= seg; s++)
            {
                float u = s / (float)seg;
                float phi = u * Mathf.PI * 2f;
                Vector3 dir = (impactDir * ringCos
                    + tangent * (Mathf.Cos(phi) * ringSin)
                    + bitangent * (Mathf.Sin(phi) * ringSin)).normalized;
                verts[vi] = dir * rr;
                norms[vi] = dir;
                // 원형 UV — 용암 텍스처가 둥글게
                float ru = 0.5f + 0.5f * rt * Mathf.Cos(phi);
                float rv = 0.5f + 0.5f * rt * Mathf.Sin(phi);
                uvs[vi] = new Vector2(ru, rv);
                vi++;
            }
        }

        // tris: center fan + rings
        var tris = new int[seg * 3 + (rings - 1) * seg * 6];
        int ti = 0;
        for (int s = 0; s < seg; s++)
        {
            tris[ti++] = 0;
            tris[ti++] = 1 + s;
            tris[ti++] = 1 + s + 1;
        }
        for (int r = 0; r < rings - 1; r++)
        {
            int row0 = 1 + r * (seg + 1);
            int row1 = 1 + (r + 1) * (seg + 1);
            for (int s = 0; s < seg; s++)
            {
                int i0 = row0 + s;
                int i1 = row0 + s + 1;
                int j0 = row1 + s;
                int j1 = row1 + s + 1;
                tris[ti++] = i0;
                tris[ti++] = j0;
                tris[ti++] = i1;
                tris[ti++] = i1;
                tris[ti++] = j0;
                tris[ti++] = j1;
            }
        }

        var mesh = new Mesh { name = "LavaSphericalCap" };
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        // 바깥에서 보이도록 법선 지구 바깥쪽 유지 (이미 dir)
        return mesh;
    }

    static Material CreateLavaFillMaterial(bool huge)
    {
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);

        var lava = Resources.Load<Texture2D>("Impact/lava_color");
        var emit = Resources.Load<Texture2D>("Impact/lava_emission");

        if (lava != null)
        {
            mat.mainTexture = lava;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", lava);
        }

        Color tint = new Color(1f, 0.45f, 0.2f);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);

        mat.EnableKeyword("_EMISSION");
        if (emit != null && mat.HasProperty("_EmissionMap"))
            mat.SetTexture("_EmissionMap", emit);
        // 스크린샷처럼 붉게 빛나는 맞은 면
        mat.SetColor("_EmissionColor", new Color(1.6f, 0.35f, 0.06f) * (huge ? 2.0f : 1.25f));

        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.45f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.45f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.15f);

        return mat;
    }
}
