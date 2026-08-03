using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지구 메시를 판다. 같은 지점 반복 타격 시 누적.
///
/// HARD RULE: 지구 메시를 절대 리메시/UV 재생성하지 않는다.
/// (BuildUvSphere 등으로 교체하면 day 맵 UV가 깨져 전체가 투명·이상해 보임)
/// 허용: 기존 Unity Sphere 메시 복제 후 vertices만 이동.
/// </summary>
public class EarthCraterDeform : MonoBehaviour
{
    [SerializeField] MeshFilter crustFilter;
    [SerializeField] float mergeAngleDeg = 10f;
    [SerializeField] float maxDigDepth = 0.12f;
    [SerializeField] float minShellRadius = 0.36f;

    Mesh workingCrust;
    int lockedVertexCount;
    int lockedUvCount;
    bool ready;
    int digCountSalt;
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

        // 기존 메시만 복제 — UV/삼각형 레이아웃 그대로. 새 구체 생성 금지.
        workingCrust = CloneWritableMeshPreserveUv(crustFilter, "EarthCrustDeform");
        if (workingCrust == null)
            return;

        lockedVertexCount = workingCrust.vertexCount;
        lockedUvCount = workingCrust.uv != null ? workingCrust.uv.Length : 0;
        if (lockedUvCount == 0 || lockedUvCount != lockedVertexCount)
        {
            Debug.LogError("[EarthCraterDeform] Earth mesh missing UVs — dig disabled to avoid transparent planet.");
            workingCrust = null;
            return;
        }

        var col = GetComponent<MeshCollider>();
        if (col != null)
            col.sharedMesh = workingCrust;

        ready = true;
    }

    /// <summary>기존 MeshFilter 메시만 Instantiate. UV 재작성/리메시 절대 금지.</summary>
    static Mesh CloneWritableMeshPreserveUv(MeshFilter mf, string name)
    {
        if (mf == null || mf.sharedMesh == null)
            return null;

        Mesh src = mf.sharedMesh;
        // 안전: 소스에 UV가 없으면 복제하지 않음
        if (src.uv == null || src.uv.Length == 0 || src.uv.Length != src.vertexCount)
        {
            Debug.LogError("[EarthCraterDeform] Refusing clone — source mesh has broken UVs.");
            return null;
        }

        var m = Object.Instantiate(src);
        m.name = name;
        m.MarkDynamic();
        // UV는 Instantiate로 이미 복사됨. 절대 m.uv = ... 하지 말 것.
        mf.mesh = m;
        return m;
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

        // UV 잠금 깨지면 즉시 중단 (투명 지구 방지)
        if (!UvLockIntact(workingCrust))
        {
            Debug.LogError("[EarthCraterDeform] UV lock broken — dig aborted.");
            return 0;
        }

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return 0;

        Vector3 dir = local.normalized;
        DigSite site = FindOrCreateSite(dir);
        site.hits++;
        site.dir = Vector3.Slerp(site.dir, dir, 0.35f).normalized;

        float hitMul = 1f + (site.hits - 1) * (huge ? 0.4f : 0.32f);
        float depth = Mathf.Clamp(depthNorm * hitMul, 0.02f, maxDigDepth);
        float radius = Mathf.Clamp(radiusNorm * (1f + (site.hits - 1) * 0.05f), 0.05f, 0.28f);
        float rimH = depth * 0.35f;

        if (seed == 0)
            seed = HashDir(site.dir) ^ (site.hits * 7919);

        // crust만 변형. Ocean/Clouds는 건드리지 않음 (레이어·투명 이슈 방지).
        DeformVerticesOnly(workingCrust, site.dir, radius, depth, rimH, seed, minShellRadius);

        if (!UvLockIntact(workingCrust))
        {
            Debug.LogError("[EarthCraterDeform] Dig corrupted UVs — further dig disabled.");
            ready = false;
            workingCrust = null;
            return 0;
        }

        RefreshCollider();
        return site.hits;
    }

    /// <summary>블랙홀용: 안쪽으로만 파냄. 림/누적 타격 없음 (스파이크 방지).</summary>
    public void CarveHole(Vector3 worldPoint, float radiusNorm, float depthNorm)
    {
        DrillBore(worldPoint, radiusNorm, depthNorm, minShellRadius);
    }

    /// <summary>
    /// 드릴/블랙홀: 림 없이 안쪽으로만. shellFloor가 낮을수록 더 깊은 구멍.
    /// </summary>
    public void DrillBore(Vector3 worldPoint, float radiusNorm, float depthNorm, float shellFloor)
    {
        EnsureReady();
        if (workingCrust == null || !UvLockIntact(workingCrust))
            return;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return;

        float depth = Mathf.Clamp(depthNorm, 0.02f, 0.28f);
        float radius = Mathf.Clamp(radiusNorm, 0.05f, 0.34f);
        float floor = Mathf.Clamp(shellFloor, 0.18f, minShellRadius);
        digCountSalt++;
        // rimFrac = 0 → 절대 바깥으로 솟지 않음
        DeformVerticesOnly(workingCrust, local.normalized, radius, depth, 0f, HashDir(local.normalized) ^ digCountSalt, floor);

        if (!UvLockIntact(workingCrust))
        {
            Debug.LogError("[EarthCraterDeform] DrillBore corrupted UVs.");
            ready = false;
            workingCrust = null;
            return;
        }

        RefreshCollider();
    }

    /// <summary>의도적 스파이크: 지표가 밖으로 삐죽 솟아오름.</summary>
    public void SpikeErupt(Vector3 worldPoint, float radiusNorm, float heightNorm, int seed = 0)
    {
        EnsureReady();
        if (workingCrust == null || !UvLockIntact(workingCrust))
            return;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return;

        if (seed == 0)
            seed = HashDir(local.normalized) ^ 0x51CE;

        float height = Mathf.Clamp(heightNorm, 0.08f, 0.85f);
        float radius = Mathf.Clamp(radiusNorm, 0.06f, 0.28f);
        DeformSpikeOut(workingCrust, local.normalized, radius, height, seed, 1.85f);

        if (!UvLockIntact(workingCrust))
        {
            Debug.LogError("[EarthCraterDeform] SpikeErupt corrupted UVs.");
            ready = false;
            workingCrust = null;
            return;
        }

        RefreshCollider();
    }

    bool UvLockIntact(Mesh mesh)
    {
        if (mesh == null)
            return false;
        if (mesh.vertexCount != lockedVertexCount)
            return false;
        var uv = mesh.uv;
        return uv != null && uv.Length == lockedUvCount && uv.Length == mesh.vertexCount;
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

    /// <summary>vertices만 이동. uv/triangles/normals 레이아웃 교체 금지.</summary>
    static void DeformVerticesOnly(
        Mesh mesh, Vector3 impactDir, float craterAngle, float depthFrac, float rimFrac, int seed, float minRadius)
    {
        // UV 스냅샷 — 변형 후 반드시 복원 (실수로 깨지는 것 방지)
        Vector2[] uvLock = mesh.uv;
        int[] triLock = mesh.triangles;

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
        float craterRad = Mathf.Clamp(craterAngle, 0.05f, 0.4f);

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
        // UV/triangles 강제 유지 — 투명 지구 방지의 핵심
        mesh.uv = uvLock;
        mesh.triangles = triLock;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    /// <summary>밖으로 뾰족하게 밀어내는 변형 (블랙홀 버그 연출을 무기로).</summary>
    static void DeformSpikeOut(
        Mesh mesh, Vector3 impactDir, float craterAngle, float heightFrac, int seed, float maxRadius)
    {
        Vector2[] uvLock = mesh.uv;
        int[] triLock = mesh.triangles;

        var rng = new System.Random(seed);
        float rot = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        int lobes = 3 + rng.Next(0, 4);
        float lobeSharp = Mathf.Lerp(2.2f, 4.5f, (float)rng.NextDouble());
        float tipBias = Mathf.Lerp(0.55f, 1.15f, (float)rng.NextDouble());

        Vector3 tAxis = Vector3.Cross(impactDir, Vector3.up);
        if (tAxis.sqrMagnitude < 1e-4f)
            tAxis = Vector3.Cross(impactDir, Vector3.right);
        tAxis.Normalize();
        Vector3 bAxis = Vector3.Cross(impactDir, tAxis).normalized;
        Vector3 axisA = (tAxis * Mathf.Cos(rot) + bAxis * Mathf.Sin(rot)).normalized;
        Vector3 axisB = (bAxis * Mathf.Cos(rot) - tAxis * Mathf.Sin(rot)).normalized;

        var verts = mesh.vertices;
        bool changed = false;
        float craterRad = Mathf.Clamp(craterAngle, 0.05f, 0.35f);

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            float len = v.magnitude;
            if (len < 1e-6f)
                continue;

            Vector3 n = v / len;
            float dot = Mathf.Clamp(Vector3.Dot(n, impactDir), -1f, 1f);
            float ang = Mathf.Acos(dot);
            float t = ang / Mathf.Max(1e-4f, craterRad);
            if (t > 1.25f)
                continue;

            Vector3 tangential = n - impactDir * dot;
            float phi = 0f;
            if (tangential.sqrMagnitude > 1e-8f)
            {
                tangential.Normalize();
                phi = Mathf.Atan2(Vector3.Dot(tangential, axisB), Vector3.Dot(tangential, axisA));
            }

            float fall = 1f - Mathf.Clamp01(t);
            fall = fall * fall * (3f - 2f * fall);

            // 로브마다 뾰족한 가시
            float lobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(0.5f * lobes * phi)), lobeSharp);
            float spike = fall * (0.25f + 0.75f * lobe) * tipBias;
            // 중심도 약간 솟아 기둥처럼
            float core = fall * fall * 0.35f;
            float radialDelta = heightFrac * len * (spike + core);

            if (radialDelta < 1e-6f)
                continue;

            verts[i] = n * Mathf.Min(maxRadius, len + radialDelta);
            changed = true;
        }

        if (!changed)
            return;

        mesh.vertices = verts;
        mesh.uv = uvLock;
        mesh.triangles = triLock;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }
}
