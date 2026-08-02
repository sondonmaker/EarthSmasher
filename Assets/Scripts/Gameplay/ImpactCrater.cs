using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 충돌 지점: 메시를 파고 + 분지 + 용암 크랙 텍스처.
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

        float radiusNorm = Mathf.Clamp(size * 0.14f, 0.05f, 0.2f);
        float depthNorm = Mathf.Clamp(size * 0.09f, 0.05f, 0.16f);

        Apply(earth, worldPoint, normal, radiusNorm, depthNorm, false);
    }

    public static void SpawnHuge(EarthPlanet earth, Vector3 worldPoint, float radiusNorm = 0.24f, float depthNorm = 0.18f)
    {
        if (earth == null)
            return;
        Vector3 normal = (worldPoint - earth.transform.position).normalized;
        Apply(earth, worldPoint, normal, radiusNorm, depthNorm, true);
    }

    static void Apply(EarthPlanet earth, Vector3 worldPoint, Vector3 normal, float radiusNorm, float depthNorm, bool huge)
    {
        if (earth == null)
            return;

        normal = normal.normalized;

        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
            deform.Stamp(worldPoint, radiusNorm, depthNorm);

        SpawnBowl(earth.transform, worldPoint, normal, radiusNorm, depthNorm, huge);
        SpawnLavaVeins(earth.transform, worldPoint, normal, radiusNorm, huge);

        var scorch = EarthSurfaceScorch.Ensure(earth);
        if (scorch != null)
        {
            scorch.PaintImpactCrater(worldPoint, radiusNorm * 1.15f);
            scorch.PaintLavaCracks(worldPoint, radiusNorm * 1.25f, huge ? 22 : 14);
            scorch.CrackAt(worldPoint, radiusNorm * 1.1f, huge ? 14 : 9);
        }
    }

    static void SpawnBowl(Transform earth, Vector3 worldPoint, Vector3 normal, float radiusNorm, float depthNorm, bool huge)
    {
        Vector3 localN = earth.InverseTransformDirection(normal).normalized;
        if (localN.sqrMagnitude < 1e-6f)
            localN = Vector3.up;

        const float meshR = 0.5f;
        float bowlRad = meshR * radiusNorm * 1.35f;
        float bowlDepth = meshR * depthNorm * 1.6f;

        var go = new GameObject(huge ? "CraterBowlHuge" : "CraterBowl");
        go.transform.SetParent(earth, false);
        go.transform.localPosition = localN * (meshR - bowlDepth * 0.15f);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localN);
        go.transform.localScale = Vector3.one;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildBowlMesh(40, bowlRad, bowlDepth);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateBowlMaterial(huge);
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;

        go.AddComponent<CraterLavaPulse>().Init(mr.material, huge ? 1.4f : 0.7f);
    }

    /// <summary>분지에서 바깥으로 뻗는 발광 용암 정맥 (얇은 쿼드).</summary>
    static void SpawnLavaVeins(Transform earth, Vector3 worldPoint, Vector3 normal, float radiusNorm, bool huge)
    {
        Vector3 localN = earth.InverseTransformDirection(normal).normalized;
        if (localN.sqrMagnitude < 1e-6f)
            localN = Vector3.up;

        const float meshR = 0.5f;
        int count = huge ? 14 : 8;
        float len = meshR * radiusNorm * (huge ? 1.6f : 1.25f);

        var root = new GameObject("LavaVeins");
        root.transform.SetParent(earth, false);
        root.transform.localPosition = localN * (meshR * 1.002f);
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localN);

        for (int i = 0; i < count; i++)
        {
            float yaw = (i / (float)count) * 360f + Random.Range(-12f, 12f);
            var vein = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vein.name = "LavaVein";
            Object.Destroy(vein.GetComponent<Collider>());
            vein.transform.SetParent(root.transform, false);

            float L = len * Random.Range(0.55f, 1.1f);
            float W = meshR * radiusNorm * Random.Range(0.04f, 0.09f);
            vein.transform.localRotation = Quaternion.Euler(0f, yaw, Random.Range(-8f, 8f));
            vein.transform.localPosition = vein.transform.localRotation * new Vector3(0f, 0.002f, L * 0.45f);
            vein.transform.localScale = new Vector3(W, meshR * 0.012f, L);

            var rend = vein.GetComponent<Renderer>();
            rend.shadowCastingMode = ShadowCastingMode.Off;
            var mat = RuntimeMaterial.Opaque(new Color(1f, 0.45f, 0.08f), 2.8f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.4f, 0.05f) * 3.2f);
            }
            rend.material = mat;
            vein.AddComponent<CraterLavaPulse>().Init(mat, Random.Range(1.5f, 3.5f));
        }
    }

    static Material CreateBowlMaterial(bool huge)
    {
        var mat = RuntimeMaterial.Opaque(new Color(0.18f, 0.08f, 0.04f), huge ? 1.2f : 0.55f);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.28f, 0.04f) * (huge ? 2.2f : 1.1f));
        }
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.35f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.08f);
        return mat;
    }

    static Mesh BuildBowlMesh(int segments, float radius, float depth)
    {
        segments = Mathf.Clamp(segments, 16, 96);
        int rings = 10;

        int vertCount = (rings + 1) * (segments + 1);
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        int vi = 0;
        for (int r = 0; r <= rings; r++)
        {
            float rt = r / (float)rings;
            float rad = radius * rt;
            float y = -depth * (1f - rt * rt);
            if (rt > 0.85f)
            {
                float rimT = (rt - 0.85f) / 0.15f;
                y += depth * 0.22f * rimT * rimT;
            }

            for (int s = 0; s <= segments; s++)
            {
                float a = (s / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * rad;
                float z = Mathf.Sin(a) * rad;
                verts[vi] = new Vector3(x, y, z);
                uvs[vi] = new Vector2(s / (float)segments, rt);
                norms[vi] = new Vector3(x, depth * 0.5f, z).normalized;
                vi++;
            }
        }

        var tris = new int[rings * segments * 6];
        int ti = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int i0 = r * (segments + 1) + s;
                int i1 = i0 + segments + 1;
                tris[ti++] = i0;
                tris[ti++] = i0 + 1;
                tris[ti++] = i1;
                tris[ti++] = i0 + 1;
                tris[ti++] = i1 + 1;
                tris[ti++] = i1;
            }
        }

        var mesh = new Mesh { name = "CraterBowl" };
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var n = mesh.normals;
        for (int i = 0; i < n.Length; i++)
            n[i] = -n[i];
        mesh.normals = n;
        return mesh;
    }
}

/// <summary>용암 발광 펄스.</summary>
public class CraterLavaPulse : MonoBehaviour
{
    Material mat;
    float baseMul = 1f;
    Color baseEmit;
    float t;

    public void Init(Material m, float intensity)
    {
        mat = m;
        baseMul = intensity;
        if (mat != null && mat.HasProperty("_EmissionColor"))
            baseEmit = mat.GetColor("_EmissionColor");
        else
            baseEmit = new Color(1f, 0.35f, 0.05f) * intensity;
    }

    void Update()
    {
        if (mat == null)
            return;
        t += Time.deltaTime;
        float pulse = 0.75f + 0.35f * Mathf.Sin(t * 3.2f + GetInstanceID() * 0.01f);
        Color c = baseEmit * pulse;
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", c);
    }
}
