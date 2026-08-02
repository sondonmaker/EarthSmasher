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
        EnsureReady();
        if (workingCrust == null)
            return;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return;

        Vector3 dir = local.normalized;
        // 얕은 눌림만 — 깊게 파면 속이 비고 플라스틱 공처럼 보임
        float craterAngle = Mathf.Clamp(radiusNorm * 0.95f, 0.05f, 0.45f);
        float depth = Mathf.Clamp(depthNorm, 0.008f, 0.045f);
        float rimH = depth * 0.9f;

        DeformMesh(workingCrust, dir, craterAngle, depth, rimH);
        // Ocean/Cloud는 변형하지 않음 (파란 속살·구멍 노출 방지)
    }

    static void DeformMesh(Mesh mesh, Vector3 impactDir, float craterAngle, float depthFrac, float rimFrac)
    {
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
            float t = ang / craterAngle;
            if (t > 1.4f)
                continue;

            float radialDelta = 0f;
            if (t <= 1f)
            {
                // 깊은 분지 (중앙이 강하게 들어감)
                float bowl = 1f - t;
                bowl = bowl * bowl * (3f - 2f * bowl); // smoothstep-ish
                radialDelta -= depthFrac * len * Mathf.Pow(bowl, 0.85f);

                // 테두리 융기 — 실루엣용
                float rim = Mathf.Exp(-Mathf.Pow((t - 0.88f) * 5.5f, 2f));
                radialDelta += rimFrac * len * rim;
            }
            else
            {
                float u = 1f - (t - 1f) / 0.4f;
                if (u > 0f)
                    radialDelta += rimFrac * len * 0.35f * u * u;
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

        int i = 0;
        for (int y = 0; y <= latSeg; y++)
        {
            float v = y / (float)latSeg;
            float pitch = (v - 0.5f) * Mathf.PI;
            float cy = Mathf.Sin(pitch);
            float cr = Mathf.Cos(pitch);
            for (int x = 0; x <= lonSeg; x++)
            {
                float u = x / (float)lonSeg;
                float yaw = u * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(yaw) * cr, cy, Mathf.Sin(yaw) * cr);
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
