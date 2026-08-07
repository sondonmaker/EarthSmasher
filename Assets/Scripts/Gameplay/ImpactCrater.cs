using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 충돌: 불규칙한 크레이터 지형 + 자연스러운 용암 흉터 (완벽한 원 금지).
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

    static void Apply(EarthPlanet earth, Vector3 worldPoint, Vector3 normal, float radiusNorm, bool huge, bool lavaScar = true)
    {
        if (earth == null)
            return;

        normal = normal.normalized;
        int seed = HashSeed(worldPoint, radiusNorm);

        var deform = EarthCraterDeform.Ensure(earth);
        int hits = 1;
        if (deform != null)
            hits = deform.Dig(worldPoint, radiusNorm, huge ? 0.09f : 0.045f, huge, seed);

        if (lavaScar)
            SpawnLavaScar(earth.transform, normal, radiusNorm * (1f + (hits - 1) * 0.08f), huge, seed);

        var scorch = EarthSurfaceScorch.Ensure(earth);
        if (scorch != null)
            scorch.PaintImpactCrater(worldPoint, radiusNorm * 1.15f, seed);
    }

    /// <summary>운석 타격과 동일 — 지형 Dig + 용암 흉터 + 크레이터 텍스처.</summary>
    public static void ApplyStrike(EarthPlanet earth, Vector3 worldPoint, Vector3 normal, float radiusNorm, bool lavaScar = true)
    {
        if (earth == null)
            return;
        Apply(earth, worldPoint, normal, Mathf.Clamp(radiusNorm, 0.028f, 0.16f), false, lavaScar);
    }

    static int HashSeed(Vector3 p, float r)
    {
        unchecked
        {
            int h = p.x.GetHashCode();
            h = (h * 397) ^ p.y.GetHashCode();
            h = (h * 397) ^ p.z.GetHashCode();
            h = (h * 397) ^ r.GetHashCode();
            return h == 0 ? 17 : h;
        }
    }

    /// <summary>구면 위 불규칙 용암 흉터 — 타원 + 들쭉날쭉한 가장자리.</summary>
    static void SpawnLavaScar(Transform earth, Vector3 normal, float radiusNorm, bool huge, int seed)
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
        mf.sharedMesh = BuildIrregularScar(48, 16, localN, meshR, ang, huge ? 0.012f : 0.008f, seed);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateLavaFillMaterial(huge);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
    }

    /// <summary>
    /// 타원형 + 각도별 반경 노이즈로 들쭉날쭉한 구면 흉터.
    /// </summary>
    static Mesh BuildIrregularScar(int seg, int rings, Vector3 impactDir, float radius, float angleRad, float bowlInset, int seed)
    {
        seg = Mathf.Clamp(seg, 24, 80);
        rings = Mathf.Clamp(rings, 8, 28);
        impactDir.Normalize();

        var rng = new System.Random(seed);
        float stretchA = Mathf.Lerp(0.72f, 1.28f, (float)rng.NextDouble());
        float stretchB = Mathf.Lerp(0.75f, 1.22f, (float)rng.NextDouble());
        // 한쪽으로 더 길게 (충돌 각/분출 방향 느낌)
        if (rng.NextDouble() > 0.5)
            stretchA *= 1.15f;
        else
            stretchB *= 1.12f;

        float rot = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float n1 = Mathf.Lerp(0.12f, 0.28f, (float)rng.NextDouble());
        float n2 = Mathf.Lerp(0.06f, 0.16f, (float)rng.NextDouble());
        float n3 = Mathf.Lerp(0.03f, 0.1f, (float)rng.NextDouble());
        float p1 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p2 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p3 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        int h1 = 2 + rng.Next(0, 3);
        int h2 = 5 + rng.Next(0, 4);
        int h3 = 9 + rng.Next(0, 6);

        // 바깥 들쭉날쭉 — 섹터별 추가 지터
        var edgeJitter = new float[seg + 1];
        for (int s = 0; s <= seg; s++)
            edgeJitter[s] = Mathf.Lerp(0.82f, 1.22f, (float)rng.NextDouble());
        // 이웃과 살짝 블렌드해서 너무 스파이크 나지 않게
        for (int pass = 0; pass < 2; pass++)
        {
            var tmp = (float[])edgeJitter.Clone();
            for (int s = 0; s < seg; s++)
            {
                float a = tmp[(s + seg - 1) % seg];
                float b = tmp[s];
                float c = tmp[(s + 1) % seg];
                edgeJitter[s] = b * 0.5f + a * 0.25f + c * 0.25f;
            }
            edgeJitter[seg] = edgeJitter[0];
        }

        Vector3 tangent = Vector3.Cross(impactDir, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(impactDir, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(impactDir, tangent).normalized;

        // 회전된 타원 축
        Vector3 axisA = (tangent * Mathf.Cos(rot) + bitangent * Mathf.Sin(rot)).normalized;
        Vector3 axisB = (bitangent * Mathf.Cos(rot) - tangent * Mathf.Sin(rot)).normalized;

        int vertCount = 1 + rings * (seg + 1);
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        float centerR = radius - bowlInset * RandomRange(rng, 0.85f, 1.25f);
        verts[0] = impactDir * centerR;
        norms[0] = impactDir;
        uvs[0] = new Vector2(0.5f, 0.5f);

        int vi = 1;
        for (int r = 1; r <= rings; r++)
        {
            float rt = r / (float)rings;
            bool outer = r == rings;

            for (int s = 0; s <= seg; s++)
            {
                float u = s / (float)seg;
                float phi = u * Mathf.PI * 2f;

                float ellipse = stretchA * Mathf.Cos(phi) * Mathf.Cos(phi)
                              + stretchB * Mathf.Sin(phi) * Mathf.Sin(phi);
                float wave = 1f
                    + n1 * Mathf.Sin(h1 * phi + p1)
                    + n2 * Mathf.Sin(h2 * phi + p2)
                    + n3 * Mathf.Sin(h3 * phi + p3);
                float edge = outer ? edgeJitter[s] : Mathf.Lerp(1f, edgeJitter[s], rt * rt);
                float radMul = Mathf.Clamp(ellipse * wave * edge, 0.55f, 1.55f);

                float a = angleRad * rt * radMul;
                float inset = bowlInset * (1f - rt * rt) * RandomRange(rng, 0.9f, 1.15f);
                // 중심이 살짝 비대칭으로 더 깊게
                float asym = 1f + 0.12f * Mathf.Sin(phi + p1) * (1f - rt);
                float rr = radius - inset * asym;
                float ringSin = Mathf.Sin(a);
                float ringCos = Mathf.Cos(a);

                Vector3 dir = (impactDir * ringCos
                    + axisA * (Mathf.Cos(phi) * ringSin)
                    + axisB * (Mathf.Sin(phi) * ringSin)).normalized;
                verts[vi] = dir * rr;
                norms[vi] = dir;

                float ru = 0.5f + 0.5f * rt * radMul * Mathf.Cos(phi);
                float rv = 0.5f + 0.5f * rt * radMul * Mathf.Sin(phi);
                uvs[vi] = new Vector2(ru, rv);
                vi++;
            }
        }

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

        var mesh = new Mesh { name = "LavaIrregularScar" };
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    static float RandomRange(System.Random rng, float a, float b)
    {
        return Mathf.Lerp(a, b, (float)rng.NextDouble());
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
            // 타일 깨서 원형 패턴 느낌 줄이기
            mat.mainTextureScale = new Vector2(
                Random.Range(1.6f, 2.8f),
                Random.Range(1.6f, 2.8f));
            mat.mainTextureOffset = new Vector2(Random.value, Random.value);
        }

        Color tint = new Color(1f, 0.45f, 0.2f);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);

        mat.EnableKeyword("_EMISSION");
        if (emit != null && mat.HasProperty("_EmissionMap"))
            mat.SetTexture("_EmissionMap", emit);
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
