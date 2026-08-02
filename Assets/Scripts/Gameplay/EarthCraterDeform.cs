using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지구 메시를 깊게 판다. 같은 지점 반복 타격 시 누적되어 Solar Smash처럼 깊어짐.
/// </summary>
public class EarthCraterDeform : MonoBehaviour
{
    [SerializeField] MeshFilter crustFilter;
    [SerializeField] int lonSegments = 96;
    [SerializeField] int latSegments = 48;
    [SerializeField] float mergeAngleDeg = 10f;
    [SerializeField] float maxDigDepth = 0.32f;
    [SerializeField] float minShellRadius = 0.155f; // 코어(≈0.21) 근처까지

    Mesh workingCrust;
    Mesh workingOcean;
    Mesh workingClouds;
    bool ready;
    readonly List<DigSite> sites = new List<DigSite>();

    class DigSite
    {
        public Vector3 dir;
        public int hits;
        public float dug;
    }

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

        // UV 유지(텍스처 깨짐 방지) + 파임용으로 쓰기 좋게 복제
        workingCrust = CloneWritableMesh(crustFilter, "EarthCrustDig");
        // 기본 Sphere는 정점이 적어 깊게 파면 각져 보임 → 가능하면 고밀도 구로 교체하되 UV는 Unity식 유지
        if (workingCrust != null && workingCrust.vertexCount < 2000)
        {
            workingCrust = BuildUnityStyleSphere(lonSegments, latSegments);
            workingCrust.name = "EarthCrustDig";
            crustFilter.mesh = workingCrust;
        }

        var col = GetComponent<MeshCollider>();
        if (col != null && workingCrust != null)
            col.sharedMesh = workingCrust;

        workingOcean = UpgradeChildMesh("Ocean");
        workingClouds = UpgradeChildMesh("Clouds");

        ready = true;
    }

    Mesh CloneWritableMesh(MeshFilter mf, string name)
    {
        if (mf == null || mf.sharedMesh == null)
            return null;
        var m = Object.Instantiate(mf.sharedMesh);
        m.name = name;
        m.MarkDynamic();
        mf.mesh = m;
        return m;
    }

    Mesh UpgradeChildMesh(string childName)
    {
        var tf = transform.Find(childName);
        if (tf == null)
            return null;
        var mf = tf.GetComponent<MeshFilter>();
        if (mf == null)
            return null;
        if (mf.sharedMesh != null && mf.sharedMesh.vertexCount >= 2000)
            return CloneWritableMesh(mf, childName + "Dig");

        var m = BuildUnityStyleSphere(lonSegments, latSegments);
        m.name = childName + "Dig";
        mf.mesh = m;
        return m;
    }

    /// <summary>호환용.</summary>
    public void Stamp(Vector3 worldPoint, float radiusNorm, float depthNorm)
    {
        Dig(worldPoint, radiusNorm, depthNorm, false);
    }

    public void StampIrregular(Vector3 worldPoint, float radiusNorm, float depthNorm, int seed)
    {
        Dig(worldPoint, radiusNorm, depthNorm, false, seed);
    }

    /// <returns>해당 지점 누적 타격 횟수</returns>
    public int Dig(Vector3 worldPoint, float radiusNorm, float depthNorm, bool huge, int seed = 0)
    {
        EnsureReady();
        if (workingCrust == null)
            return 0;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return 0;

        Vector3 dir = local.normalized;
        DigSite site = FindOrCreateSite(dir);
        site.hits++;
        site.dir = Vector3.Slerp(site.dir, dir, 0.35f).normalized;

        // 같은 곳 여러 번 → 점점 더 깊게
        float hitMul = 1f + (site.hits - 1) * (huge ? 0.85f : 0.7f);
        float depth = Mathf.Clamp(depthNorm * hitMul, 0.035f, maxDigDepth);
        float radius = Mathf.Clamp(radiusNorm * (1f + (site.hits - 1) * 0.1f), 0.05f, 0.42f);
        float rimH = depth * Mathf.Lerp(0.45f, 0.25f, Mathf.Clamp01((site.hits - 1) / 5f));
        float minR = Mathf.Lerp(0.32f, minShellRadius, Mathf.Clamp01((site.hits - 1) / 5f));

        if (seed == 0)
            seed = HashDir(site.dir) ^ (site.hits * 7919);

        site.dug = Mathf.Min(maxDigDepth, site.dug + depth * 0.55f);

        DeformMeshIrregular(workingCrust, site.dir, radius, depth, rimH, seed, minR);
        if (workingOcean != null)
            DeformMeshIrregular(workingOcean, site.dir, radius * 1.02f, depth * 1.05f, rimH * 0.35f, seed ^ 0x5f3759df, minR);
        if (workingClouds != null && site.hits >= 2)
            DeformMeshIrregular(workingClouds, site.dir, radius * 0.9f, depth * 0.35f, 0f, seed ^ 12345, 0.35f);

        RefreshCollider();
        if (site.hits >= 3 || site.dug > 0.14f)
            RevealCore();

        return site.hits;
    }

    DigSite FindOrCreateSite(Vector3 dir)
    {
        float mergeCos = Mathf.Cos(mergeAngleDeg * Mathf.Deg2Rad);
        DigSite best = null;
        float bestDot = mergeCos;
        for (int i = 0; i < sites.Count; i++)
        {
            float d = Vector3.Dot(sites[i].dir, dir);
            if (d >= bestDot)
            {
                bestDot = d;
                best = sites[i];
            }
        }
        if (best != null)
            return best;

        var created = new DigSite { dir = dir, hits = 0, dug = 0f };
        sites.Add(created);
        return created;
    }

    void RefreshCollider()
    {
        var col = GetComponent<MeshCollider>();
        if (col == null || workingCrust == null)
            return;
        col.sharedMesh = null;
        col.sharedMesh = workingCrust;
    }

    void RevealCore()
    {
        var planet = GetComponent<EarthPlanet>();
        if (planet == null)
            return;
        // EarthPlanet이 core 참조를 갖고 있으면 활성화
        var core = transform.Find("Core");
        if (core != null && !core.gameObject.activeSelf)
            core.gameObject.SetActive(true);
    }

    static int HashDir(Vector3 d)
    {
        unchecked
        {
            int h = d.x.GetHashCode();
            h = (h * 397) ^ d.y.GetHashCode();
            h = (h * 397) ^ d.z.GetHashCode();
            return h == 0 ? 17 : h;
        }
    }

    static void DeformMeshIrregular(
        Mesh mesh, Vector3 impactDir, float craterAngle, float depthFrac, float rimFrac, int seed, float minRadius)
    {
        var rng = new System.Random(seed);
        float stretchA = Mathf.Lerp(0.72f, 1.28f, (float)rng.NextDouble());
        float stretchB = Mathf.Lerp(0.75f, 1.25f, (float)rng.NextDouble());
        float rot = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float n1 = Mathf.Lerp(0.1f, 0.26f, (float)rng.NextDouble());
        float n2 = Mathf.Lerp(0.05f, 0.14f, (float)rng.NextDouble());
        float p1 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p2 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        int h1 = 2 + rng.Next(0, 3);
        int h2 = 5 + rng.Next(0, 4);
        float depthBias = Mathf.Lerp(0.9f, 1.25f, (float)rng.NextDouble());

        Vector3 tAxis = Vector3.Cross(impactDir, Vector3.up);
        if (tAxis.sqrMagnitude < 1e-4f)
            tAxis = Vector3.Cross(impactDir, Vector3.right);
        tAxis.Normalize();
        Vector3 bAxis = Vector3.Cross(impactDir, tAxis).normalized;
        Vector3 axisA = (tAxis * Mathf.Cos(rot) + bAxis * Mathf.Sin(rot)).normalized;
        Vector3 axisB = (bAxis * Mathf.Cos(rot) - tAxis * Mathf.Sin(rot)).normalized;

        var verts = mesh.vertices;
        bool changed = false;
        float craterRad = Mathf.Clamp(craterAngle, 0.05f, 0.55f);

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            float len = v.magnitude;
            if (len < 1e-6f)
                continue;

            Vector3 n = v / len;
            float dot = Mathf.Clamp(Vector3.Dot(n, impactDir), -1f, 1f);
            float ang = Mathf.Acos(dot);

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
            float localAngle = craterRad * Mathf.Clamp(ellipse * wave, 0.55f, 1.5f);

            float t = ang / Mathf.Max(1e-4f, localAngle);
            if (t > 1.5f)
                continue;

            float radialDelta = 0f;
            if (t <= 1f)
            {
                float bowl = 1f - t;
                bowl = bowl * bowl * (3f - 2f * bowl);
                float asym = 1f + 0.18f * Mathf.Sin(phi + p1);
                // 중심을 더 깊게 — Solar Smash 느낌
                float centerBoost = Mathf.Lerp(1.35f, 1f, t);
                radialDelta -= depthFrac * len * Mathf.Pow(bowl, 0.75f) * depthBias * asym * centerBoost;

                if (rimFrac > 1e-5f)
                {
                    float rimCenter = 0.86f + 0.08f * Mathf.Sin(phi * 3f + p2);
                    float rim = Mathf.Exp(-Mathf.Pow((t - rimCenter) * 5.2f, 2f));
                    radialDelta += rimFrac * len * rim * (0.85f + 0.3f * Mathf.Sin(phi * 2f + p1));
                }
            }
            else if (rimFrac > 1e-5f)
            {
                float u = 1f - (t - 1f) / 0.5f;
                if (u > 0f)
                    radialDelta += rimFrac * len * 0.28f * u * u;
            }

            if (Mathf.Abs(radialDelta) < 1e-7f)
                continue;

            verts[i] = n * Mathf.Max(minRadius, len + radialDelta);
            changed = true;
        }

        if (!changed)
            return;

        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    /// <summary>
    /// Unity 기본 Sphere와 같은 UV: u=Atan2(x,z), v=위도.
    /// day/clouds 텍스처 정렬 유지 + 파임용 고밀도.
    /// </summary>
    public static Mesh BuildUnityStyleSphere(int lonSeg, int latSeg)
    {
        lonSeg = Mathf.Clamp(lonSeg, 48, 160);
        latSeg = Mathf.Clamp(latSeg, 24, 80);

        int vertCount = (lonSeg + 1) * (latSeg + 1);
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        int i = 0;
        for (int y = 0; y <= latSeg; y++)
        {
            float v = y / (float)latSeg;
            float pitch = (v - 0.5f) * Mathf.PI; // -90..90
            float cy = Mathf.Sin(pitch);
            float cr = Mathf.Cos(pitch);
            for (int x = 0; x <= lonSeg; x++)
            {
                float u = x / (float)lonSeg;
                float yaw = u * Mathf.PI * 2f;
                // Unity sphere-ish: +Z at u≈0 when using cosZ/sinX… use Atan2(x,z) compatible
                var p = new Vector3(Mathf.Sin(yaw) * cr, cy, Mathf.Cos(yaw) * cr);
                verts[i] = p * 0.5f;
                norms[i] = p;
                float uu = Mathf.Atan2(p.x, p.z) / (Mathf.PI * 2f);
                if (uu < 0f) uu += 1f;
                float vv = Mathf.Asin(Mathf.Clamp(p.y, -1f, 1f)) / Mathf.PI + 0.5f;
                uvs[i] = new Vector2(uu, vv);
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

        var mesh = new Mesh { name = "DigSphere" };
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
