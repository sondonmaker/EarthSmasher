using UnityEngine;

/// <summary>
/// 지구 지각/바다/구름 메시를 움푹 파서 크레이터 지형을 남긴다.
/// </summary>
public class EarthCraterDeform : MonoBehaviour
{
    [SerializeField] MeshFilter crustFilter;
    [SerializeField] int lonSegments = 128;
    [SerializeField] int latSegments = 64;

    Mesh workingCrust;
    Mesh workingOcean;
    Mesh workingClouds;
    bool ready;

    public static EarthCraterDeform Ensure(EarthPlanet earth)
    {
        if (earth == null)
            return null;
        var d = earth.GetComponent<EarthCraterDeform>();
        if (d == null)
            d = earth.gameObject.AddComponent<EarthCraterDeform>();
        d.EnsureReady();
        return d;
    }

    public void EnsureReady()
    {
        if (ready)
            return;

        if (crustFilter == null)
            crustFilter = GetComponent<MeshFilter>();
        if (crustFilter == null)
            return;

        workingCrust = BuildUvSphere(lonSegments, latSegments);
        workingCrust.name = "EarthCrustDeform";
        crustFilter.mesh = workingCrust; // instance — sharedMesh면 변형이 안 먹을 수 있음

        var col = GetComponent<MeshCollider>();
        if (col != null)
            col.sharedMesh = workingCrust;

        workingOcean = ReplaceChildMesh("Ocean");
        workingClouds = ReplaceChildMesh("Clouds");

        ready = true;
    }

    Mesh ReplaceChildMesh(string childName)
    {
        var tf = transform.Find(childName);
        if (tf == null)
            return null;
        var mf = tf.GetComponent<MeshFilter>();
        if (mf == null)
            return null;
        var m = BuildUvSphere(lonSegments, latSegments);
        m.name = childName + "Deform";
        mf.mesh = m;
        return m;
    }

    /// <param name="radiusNorm">지구 반지름 대비 크레이터 반경</param>
    /// <param name="depthNorm">지구 반지름 대비 깊이 (클수록 깊게 파임)</param>
    public void Stamp(Vector3 worldPoint, float radiusNorm, float depthNorm)
    {
        StampIrregular(worldPoint, radiusNorm, depthNorm, worldPoint.GetHashCode());
    }

    public void StampIrregular(Vector3 worldPoint, float radiusNorm, float depthNorm, int seed)
    {
        EnsureReady();
        if (workingCrust == null)
            return;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return;

        Vector3 dir = local.normalized;
        float craterAngle = Mathf.Clamp(radiusNorm * 1.0f, 0.06f, 0.5f);
        float depth = Mathf.Clamp(depthNorm, 0.02f, 0.12f);
        float rimH = depth * 0.55f;

        DeformMeshIrregular(workingCrust, dir, craterAngle, depth, rimH, seed);
        if (workingOcean != null)
            DeformMeshIrregular(workingOcean, dir, craterAngle * 1.02f, depth * 1.05f, rimH * 0.4f, seed ^ 0x5f3759df);
    }

    static void DeformMeshIrregular(Mesh mesh, Vector3 impactDir, float craterAngle, float depthFrac, float rimFrac, int seed)
    {
        var rng = new System.Random(seed);
        float stretchA = Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble());
        float stretchB = Mathf.Lerp(0.75f, 1.25f, (float)rng.NextDouble());
        float rot = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float n1 = Mathf.Lerp(0.1f, 0.26f, (float)rng.NextDouble());
        float n2 = Mathf.Lerp(0.05f, 0.14f, (float)rng.NextDouble());
        float p1 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p2 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        int h1 = 2 + rng.Next(0, 3);
        int h2 = 5 + rng.Next(0, 4);
        float depthBias = Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble());

        Vector3 tAxis = Vector3.Cross(impactDir, Vector3.up);
        if (tAxis.sqrMagnitude < 1e-4f)
            tAxis = Vector3.Cross(impactDir, Vector3.right);
        tAxis.Normalize();
        Vector3 bAxis = Vector3.Cross(impactDir, tAxis).normalized;
        Vector3 axisA = (tAxis * Mathf.Cos(rot) + bAxis * Mathf.Sin(rot)).normalized;
        Vector3 axisB = (bAxis * Mathf.Cos(rot) - tAxis * Mathf.Sin(rot)).normalized;

        var verts = mesh.vertices;
        bool changed = false;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            float len = v.magnitude;
            if (len < 1e-6f)
                continue;

            Vector3 n = v / len;
            float dot = Mathf.Clamp(Vector3.Dot(n, impactDir), -1f, 1f);
            float ang = Mathf.Acos(dot);

            // 충격점 기준 방위각 → 타원/노이즈로 유효 반경 변형
            Vector3 tangential = n - impactDir * dot;
            float phi = 0f;
            if (tangential.sqrMagnitude > 1e-8f)
            {
                tangential.Normalize();
                phi = Mathf.Atan2(Vector3.Dot(tangential, axisB), Vector3.Dot(tangential, axisA));
            }

            float ellipse = stretchA * Mathf.Cos(phi) * Mathf.Cos(phi)
                          + stretchB * Mathf.Sin(phi) * Mathf.Sin(phi);
            float wave = 1f + n1 * Mathf.Sin(h1 * phi + p1) + n2 * Mathf.Sin(h2 * phi + p2);
            float localAngle = craterAngle * Mathf.Clamp(ellipse * wave, 0.55f, 1.5f);

            float t = ang / Mathf.Max(1e-4f, localAngle);
            if (t > 1.45f)
                continue;

            float radialDelta = 0f;
            if (t <= 1f)
            {
                float bowl = 1f - t;
                bowl = bowl * bowl * (3f - 2f * bowl);
                // 한쪽이 더 깊은 비대칭 분지
                float asym = 1f + 0.18f * Mathf.Sin(phi + p1);
                radialDelta -= depthFrac * len * Mathf.Pow(bowl, 0.85f) * depthBias * asym;

                float rimCenter = 0.82f + 0.1f * Mathf.Sin(phi * 3f + p2);
                float rim = Mathf.Exp(-Mathf.Pow((t - rimCenter) * 5.2f, 2f));
                radialDelta += rimFrac * len * rim * (0.85f + 0.3f * Mathf.Sin(phi * 2f + p1));
            }
            else
            {
                float u = 1f - (t - 1f) / 0.45f;
                if (u > 0f)
                    radialDelta += rimFrac * len * 0.3f * u * u * (0.7f + 0.4f * Mathf.Sin(phi * 4f + p2));
            }

            if (Mathf.Abs(radialDelta) < 1e-7f)
                continue;

            verts[i] = n * Mathf.Max(0.05f, len + radialDelta);
            changed = true;
        }

        if (!changed)
            return;

        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    public static Mesh BuildUvSphere(int lonSeg, int latSeg)
    {
        lonSeg = Mathf.Clamp(lonSeg, 24, 180);
        latSeg = Mathf.Clamp(latSeg, 12, 90);

        int vertCount = (lonSeg + 1) * (latSeg + 1);
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        // EarthGeo / 텍스처 페인팅과 동일한 위경도→방향 규약
        int i = 0;
        for (int y = 0; y <= latSeg; y++)
        {
            float v = y / (float)latSeg;
            for (int x = 0; x <= lonSeg; x++)
            {
                float u = x / (float)lonSeg;
                EarthGeo.UvToLatLon(u, v, out float lat, out float lon);
                Vector3 p = EarthGeo.LatLonToDirection(lat, lon);
                verts[i] = p * 0.5f;
                norms[i] = p;
                uvs[i] = new Vector2(u, v);
                i++;
            }
        }

        var tris = new int[lonSeg * latSeg * 6];
        int t = 0;
        for (int y = 0; y < latSeg; y++)
        for (int x = 0; x < lonSeg; x++)
        {
            int i0 = y * (lonSeg + 1) + x;
            int i1 = i0 + lonSeg + 1;
            tris[t++] = i0;
            tris[t++] = i1;
            tris[t++] = i0 + 1;
            tris[t++] = i0 + 1;
            tris[t++] = i1;
            tris[t++] = i1 + 1;
        }

        var mesh = new Mesh { name = "HiSphere" };
        if (vertCount > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }
}
