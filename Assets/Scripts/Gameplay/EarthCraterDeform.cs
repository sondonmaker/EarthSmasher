using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지구 메시를 판다. 같은 지점 반복 타격 시 누적.
/// 메시 UV는 절대 교체하지 않음 (텍스처/투명 깨짐 방지).
/// </summary>
public class EarthCraterDeform : MonoBehaviour
{
    [SerializeField] MeshFilter crustFilter;
    [SerializeField] float mergeAngleDeg = 10f;
    [SerializeField] float maxDigDepth = 0.16f;
    [SerializeField] float minShellRadius = 0.28f;

    Mesh workingCrust;
    Mesh workingOcean;
    bool ready;
    readonly List<DigSite> sites = new List<DigSite>();

    class DigSite
    {
        public Vector3 dir;
        public int hits;
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

        // 기존 메시만 복제 — UV/텍스처 그대로
        workingCrust = CloneWritableMesh(crustFilter, "EarthCrustDeform");

        var col = GetComponent<MeshCollider>();
        if (col != null && workingCrust != null)
            col.sharedMesh = workingCrust;

        workingOcean = CloneChildWritableMesh("Ocean");
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

    Mesh CloneChildWritableMesh(string childName)
    {
        var tf = transform.Find(childName);
        if (tf == null)
            return null;
        return CloneWritableMesh(tf.GetComponent<MeshFilter>(), childName + "Deform");
    }

    public void Stamp(Vector3 worldPoint, float radiusNorm, float depthNorm)
    {
        Dig(worldPoint, radiusNorm, depthNorm, false);
    }

    public void StampIrregular(Vector3 worldPoint, float radiusNorm, float depthNorm, int seed)
    {
        Dig(worldPoint, radiusNorm, depthNorm, false, seed);
    }

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

        float hitMul = 1f + (site.hits - 1) * (huge ? 0.55f : 0.45f);
        float depth = Mathf.Clamp(depthNorm * hitMul, 0.03f, maxDigDepth);
        float radius = Mathf.Clamp(radiusNorm * (1f + (site.hits - 1) * 0.06f), 0.05f, 0.35f);
        float rimH = depth * 0.4f;

        if (seed == 0)
            seed = HashDir(site.dir) ^ (site.hits * 7919);

        DeformMeshIrregular(workingCrust, site.dir, radius, depth, rimH, seed, minShellRadius);
        if (workingOcean != null)
            DeformMeshIrregular(workingOcean, site.dir, radius * 1.02f, depth * 1.02f, rimH * 0.35f, seed ^ 0x5f3759df, minShellRadius);

        RefreshCollider();
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

        var created = new DigSite { dir = dir, hits = 0 };
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
        float stretchA = Mathf.Lerp(0.75f, 1.25f, (float)rng.NextDouble());
        float stretchB = Mathf.Lerp(0.78f, 1.22f, (float)rng.NextDouble());
        float rot = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float n1 = Mathf.Lerp(0.08f, 0.2f, (float)rng.NextDouble());
        float n2 = Mathf.Lerp(0.04f, 0.12f, (float)rng.NextDouble());
        float p1 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p2 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        int h1 = 2 + rng.Next(0, 3);
        int h2 = 5 + rng.Next(0, 4);
        float depthBias = Mathf.Lerp(0.9f, 1.15f, (float)rng.NextDouble());

        Vector3 tAxis = Vector3.Cross(impactDir, Vector3.up);
        if (tAxis.sqrMagnitude < 1e-4f)
            tAxis = Vector3.Cross(impactDir, Vector3.right);
        tAxis.Normalize();
        Vector3 bAxis = Vector3.Cross(impactDir, tAxis).normalized;
        Vector3 axisA = (tAxis * Mathf.Cos(rot) + bAxis * Mathf.Sin(rot)).normalized;
        Vector3 axisB = (bAxis * Mathf.Cos(rot) - tAxis * Mathf.Sin(rot)).normalized;

        var verts = mesh.vertices;
        bool changed = false;
        float craterRad = Mathf.Clamp(craterAngle, 0.05f, 0.45f);

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
            float localAngle = craterRad * Mathf.Clamp(ellipse * wave, 0.55f, 1.45f);

            float t = ang / Mathf.Max(1e-4f, localAngle);
            if (t > 1.4f)
                continue;

            float radialDelta = 0f;
            if (t <= 1f)
            {
                float bowl = 1f - t;
                bowl = bowl * bowl * (3f - 2f * bowl);
                float asym = 1f + 0.15f * Mathf.Sin(phi + p1);
                radialDelta -= depthFrac * len * Mathf.Pow(bowl, 0.85f) * depthBias * asym;

                if (rimFrac > 1e-5f)
                {
                    float rimCenter = 0.86f + 0.08f * Mathf.Sin(phi * 3f + p2);
                    float rim = Mathf.Exp(-Mathf.Pow((t - rimCenter) * 5.2f, 2f));
                    radialDelta += rimFrac * len * rim;
                }
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
}
