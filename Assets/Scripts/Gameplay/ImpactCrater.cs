using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 충돌 자국: 공을 깊게 파지 않고, 표면 위에 얕은 크레이터 스카 + 텍스처.
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

        float radiusNorm = Mathf.Clamp(size * 0.12f, 0.04f, 0.16f);
        Apply(earth, worldPoint, normal, radiusNorm, false);
    }

    public static void SpawnHuge(EarthPlanet earth, Vector3 worldPoint, float radiusNorm = 0.2f, float depthNorm = 0.18f)
    {
        if (earth == null)
            return;
        Vector3 normal = (worldPoint - earth.transform.position).normalized;
        Apply(earth, worldPoint, normal, Mathf.Clamp(radiusNorm, 0.12f, 0.26f), true);
    }

    static void Apply(EarthPlanet earth, Vector3 worldPoint, Vector3 normal, float radiusNorm, bool huge)
    {
        if (earth == null)
            return;

        normal = normal.normalized;

        // 깊게 파면 "플라스틱 공 찌그러짐"이 되므로 아주 얕게만 / 또는 스킵
        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
            deform.Stamp(worldPoint, radiusNorm * 0.85f, huge ? 0.035f : 0.018f);

        SpawnSurfaceScar(earth.transform, worldPoint, normal, radiusNorm, huge);

        var scorch = EarthSurfaceScorch.Ensure(earth);
        if (scorch != null)
            scorch.PaintImpactCrater(worldPoint, radiusNorm * 1.2f);
    }

    /// <summary>
    /// 표면 위에 얹는 얕은 스카 (분지 구멍 X — 테두리+바닥 디칼).
    /// </summary>
    static void SpawnSurfaceScar(Transform earth, Vector3 worldPoint, Vector3 normal, float radiusNorm, bool huge)
    {
        Vector3 localN = earth.InverseTransformDirection(normal).normalized;
        if (localN.sqrMagnitude < 1e-6f)
            localN = Vector3.up;

        const float meshR = 0.5f;
        float rad = meshR * radiusNorm;

        var root = new GameObject(huge ? "CraterScarHuge" : "CraterScar");
        root.transform.SetParent(earth, false);
        // 표면보다 아주 살짝 밖 — 클리핑/파란 속살 노출 방지
        root.transform.localPosition = localN * (meshR * 1.004f);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localN);

        // 1) 어두운 바닥 디스크 (얇음)
        CreateDisk(root.transform, "Floor", rad * 0.92f, meshR * 0.004f, 0f, CreateFloorMaterial(huge));

        // 2) 테두리 링 (이젝타) — 약간 띄움
        CreateRing(root.transform, "Rim", rad * 0.72f, rad * 1.08f, meshR * 0.01f, meshR * 0.006f, CreateRimMaterial(huge));

        // 3) 안쪽 용암 링 (얇은 발광) — 선 낙서 말고 고리 텍스처
        CreateRing(root.transform, "LavaRing", rad * 0.55f, rad * 0.78f, meshR * 0.006f, meshR * 0.003f, CreateLavaRingMaterial(huge));
    }

    static void CreateDisk(Transform parent, string name, float radius, float thickness, float y, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(radius * 2f, thickness, radius * 2f);
        var rend = go.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void CreateRing(Transform parent, string name, float inner, float outer, float height, float y, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildRingMesh(48, inner, outer, height);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
    }

    static Mesh BuildRingMesh(int segments, float inner, float outer, float height)
    {
        segments = Mathf.Clamp(segments, 16, 96);
        // top ring: outer + inner, bottom ignored (flat)
        int vCount = (segments + 1) * 2;
        var verts = new Vector3[vCount];
        var uvs = new Vector2[vCount];
        var norms = new Vector3[vCount];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float a = t * Mathf.PI * 2f;
            float c = Mathf.Cos(a);
            float s = Mathf.Sin(a);
            verts[i] = new Vector3(c * outer, height, s * outer);
            verts[i + segments + 1] = new Vector3(c * inner, 0f, s * inner);
            uvs[i] = new Vector2(t, 1f);
            uvs[i + segments + 1] = new Vector2(t, 0f);
            norms[i] = Vector3.up;
            norms[i + segments + 1] = Vector3.up;
        }

        var tris = new int[segments * 6];
        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int o0 = i;
            int o1 = i + 1;
            int i0 = i + segments + 1;
            int i1 = i + 1 + segments + 1;
            tris[ti++] = o0;
            tris[ti++] = o1;
            tris[ti++] = i0;
            tris[ti++] = o1;
            tris[ti++] = i1;
            tris[ti++] = i0;
        }

        var mesh = new Mesh { name = "CraterRing" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.normals = norms;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    static Material CreateFloorMaterial(bool huge)
    {
        var mat = NewLit();
        var rock = Resources.Load<Texture2D>("Impact/rock_color");
        var lava = Resources.Load<Texture2D>("Impact/lava_color");
        if (rock != null)
        {
            mat.mainTexture = rock;
            SetMainTex(mat, rock);
        }
        SetColor(mat, huge ? new Color(0.25f, 0.16f, 0.12f) : new Color(0.2f, 0.14f, 0.1f));
        if (lava != null && mat.HasProperty("_EmissionMap"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", lava);
            mat.SetColor("_EmissionColor", new Color(0.55f, 0.18f, 0.04f) * (huge ? 0.9f : 0.45f));
        }
        SetGloss(mat, 0.15f);
        return mat;
    }

    static Material CreateRimMaterial(bool huge)
    {
        var mat = NewLit();
        var rock = Resources.Load<Texture2D>("Impact/rock_color");
        if (rock != null)
        {
            mat.mainTexture = rock;
            SetMainTex(mat, rock);
        }
        SetColor(mat, new Color(0.45f, 0.38f, 0.32f));
        SetGloss(mat, 0.12f);
        return mat;
    }

    static Material CreateLavaRingMaterial(bool huge)
    {
        var mat = NewLit();
        var lava = Resources.Load<Texture2D>("Impact/lava_color");
        var emit = Resources.Load<Texture2D>("Impact/lava_emission");
        if (lava != null)
        {
            mat.mainTexture = lava;
            SetMainTex(mat, lava);
        }
        SetColor(mat, new Color(1f, 0.55f, 0.25f));
        mat.EnableKeyword("_EMISSION");
        if (emit != null && mat.HasProperty("_EmissionMap"))
            mat.SetTexture("_EmissionMap", emit);
        mat.SetColor("_EmissionColor", new Color(1.2f, 0.4f, 0.08f) * (huge ? 1.4f : 0.85f));
        SetGloss(mat, 0.4f);
        return mat;
    }

    static Material NewLit()
    {
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        return new Material(shader);
    }

    static void SetMainTex(Material mat, Texture tex)
    {
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", tex);
    }

    static void SetColor(Material mat, Color c)
    {
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", c);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        mat.color = c;
    }

    static void SetGloss(Material mat, float g)
    {
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", g);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", g);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.05f);
    }
}
