using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 충돌: 메시 변형 + AmbientCG 용암/암석 텍스처 분지 (선 낙서 없음).
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

        SpawnBowl(earth.transform, normal, radiusNorm, depthNorm, huge);

        var scorch = EarthSurfaceScorch.Ensure(earth);
        if (scorch != null)
            scorch.PaintImpactCrater(worldPoint, radiusNorm * 1.15f);
    }

    static void SpawnBowl(Transform earth, Vector3 normal, float radiusNorm, float depthNorm, bool huge)
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

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildBowlMesh(48, bowlRad, bowlDepth);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateLavaRockMaterial(huge);
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;
    }

    static Material CreateLavaRockMaterial(bool huge)
    {
        var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
        var lava = Resources.Load<Texture2D>("Impact/lava_color");
        var emit = Resources.Load<Texture2D>("Impact/lava_emission");
        var rock = Resources.Load<Texture2D>("Impact/rock_color");

        Texture2D main = lava != null ? lava : rock;
        if (main != null)
        {
            mat.mainTexture = main;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", main);
        }

        Color tint = huge
            ? new Color(1f, 0.55f, 0.35f)
            : new Color(0.85f, 0.55f, 0.4f);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);

        if (emit != null && mat.HasProperty("_EmissionMap"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", emit);
            mat.SetColor("_EmissionColor", new Color(1.4f, 0.55f, 0.12f) * (huge ? 1.6f : 1.0f));
        }
        else
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.05f) * (huge ? 1.2f : 0.6f));
        }

        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.35f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.1f);
        return mat;
    }

    static Mesh BuildBowlMesh(int segments, float radius, float depth)
    {
        segments = Mathf.Clamp(segments, 16, 96);
        int rings = 12;
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
                // 용암 텍스처가 분지 안에 자연스럽게
                uvs[vi] = new Vector2(0.5f + x / (radius * 2.2f), 0.5f + z / (radius * 2.2f));
                norms[vi] = Vector3.up;
                vi++;
            }
        }

        var tris = new int[rings * segments * 6];
        int ti = 0;
        for (int r = 0; r < rings; r++)
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
