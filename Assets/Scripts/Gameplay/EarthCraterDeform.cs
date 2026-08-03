using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지구 메시를 판다. 같은 지점 반복 타격 시 누적.
///
/// HARD RULE: 런타임에 리메시/UV 재생성 금지 — vertices만 이동한다.
/// (UV를 다시 쓰면 day 맵이 깨져 지구 전체가 투명해진다)
/// 메시 밀도는 생성 시점에 EarthMeshBuilder가 한 번만 올린다.
/// </summary>
public class EarthCraterDeform : MonoBehaviour
{
    [SerializeField] MeshFilter crustFilter;
    [SerializeField] float mergeAngleDeg = 10f;
    [SerializeField] float maxDigDepth = 0.18f;
    [SerializeField] float minShellRadius = 0.36f;

    Mesh workingCrust;
    Vector3[] pristineVerts;
    int lockedVertexCount;
    int lockedUvCount;
    bool ready;
    bool meshDirty;
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

        // 초기화용 원본 지형 스냅샷 (mesh.vertices는 복사본을 돌려준다)
        pristineVerts = workingCrust.vertices;

        var col = GetComponent<MeshCollider>();
        if (col != null)
            col.sharedMesh = workingCrust;

        ready = true;
    }

    /// <summary>파낸 지형을 원래 구체로 되돌린다.</summary>
    public void RestoreShape()
    {
        sites.Clear();
        digCountSalt = 0;

        if (workingCrust == null || pristineVerts == null)
            return;
        if (pristineVerts.Length != workingCrust.vertexCount)
            return;

        workingCrust.vertices = pristineVerts;
        workingCrust.RecalculateBounds();
        meshDirty = true;
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
        float floor = Mathf.Clamp(shellFloor, 0.18f, 0.45f);
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

    /// <summary>
    /// 정점 수만 확인한다. 변형은 vertices만 건드리므로 uv/triangles는 바뀔 수 없고,
    /// mesh.uv 접근은 매번 전체 배열을 복사해 고밀도 메시에서 비용이 크다.
    /// (전체 UV 검증은 EnsureReady에서 한 번만)
    /// </summary>
    bool UvLockIntact(Mesh mesh)
    {
        return mesh != null && mesh.vertexCount == lockedVertexCount && lockedUvCount == lockedVertexCount;
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

    /// <summary>
    /// 법선 재계산은 고밀도 메시에서 가장 비싼 작업이라, 한 프레임에 여러 번 맞아도
    /// (운석우 등) 프레임당 한 번만 돌린다.
    /// </summary>
    void RefreshCollider()
    {
        meshDirty = true;
    }

    void LateUpdate()
    {
        if (!meshDirty || workingCrust == null)
            return;
        meshDirty = false;

        workingCrust.RecalculateNormals();

        var col = GetComponent<MeshCollider>();
        if (col != null)
        {
            col.sharedMesh = null;
            col.sharedMesh = workingCrust;
        }
    }

    /// <summary>구면 위에서 연속적인 값 노이즈 (0..1).</summary>
    static float SurfaceNoise(Vector3 n, float frequency, float seed)
    {
        float a = Mathf.PerlinNoise(n.x * frequency + seed, n.y * frequency + seed);
        float b = Mathf.PerlinNoise(n.y * frequency + seed + 31.4f, n.z * frequency + seed + 17.7f);
        float c = Mathf.PerlinNoise(n.z * frequency + seed + 57.2f, n.x * frequency + seed + 91.3f);
        return (a + b + c) / 3f;
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
        float noiseSeed = (seed & 0xFFFF) * 0.0137f;

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
            // 가장자리를 들쭉날쭉하게 — 고밀도 메시라야 살아난다
            float edgeNoise = 1f + 0.22f * (SurfaceNoise(n, 9.5f, noiseSeed) - 0.5f);
            float localAngle = craterRad * Mathf.Clamp(ellipse * wave * edgeNoise, 0.5f, 1.5f);

            float t = ang / Mathf.Max(1e-4f, localAngle);
            if (t > 1.45f)
                continue;

            float radialDelta = 0f;
            if (t <= 1f)
            {
                // 넓고 평평한 바닥 + 가파른 벽 (매끈한 눌림 방지)
                float wall = 1f - Mathf.SmoothStep(0.45f, 1f, t);
                float floorRough = 1f + 0.35f * (SurfaceNoise(n, 22f, noiseSeed + 5.3f) - 0.5f);
                float asym = 1f + 0.15f * Mathf.Sin(phi + p1);
                radialDelta -= depthFrac * len * wall * floorRough * depthBias * asym;

                if (rimFrac > 1e-5f)
                {
                    float rimCenter = 0.9f + 0.06f * Mathf.Sin(phi * 3f + p2);
                    float rim = Mathf.Exp(-Mathf.Pow((t - rimCenter) * 7f, 2f));
                    float rimJag = 0.6f + 0.8f * SurfaceNoise(n, 16f, noiseSeed + 11.1f);
                    radialDelta += rimFrac * len * rim * rimJag;
                }
            }
            else if (rimFrac > 1e-5f)
            {
                // 크레이터 밖으로 흩뿌려진 이젝타 능선
                float fade = 1f - Mathf.InverseLerp(1f, 1.45f, t);
                float ejecta = SurfaceNoise(n, 14f, noiseSeed + 3.7f) - 0.45f;
                radialDelta += rimFrac * len * 0.45f * fade * Mathf.Max(0f, ejecta);
            }

            if (Mathf.Abs(radialDelta) < 1e-7f)
                continue;

            verts[i] = n * Mathf.Max(minRadius, len + radialDelta);
            changed = true;
        }

        if (!changed)
            return;

        mesh.vertices = verts;
        // vertices만 갈아끼우므로 uv/triangles는 그대로다. 고밀도 메시에서 매 타격마다
        // 인덱스 버퍼를 다시 올리면 폰에서 눈에 띄게 끊긴다.
        mesh.RecalculateBounds();
        // 법선은 프레임당 한 번만 (LateUpdate에서 처리)
    }

    /// <summary>밖으로 뾰족하게 밀어내는 변형 (블랙홀 버그 연출을 무기로).</summary>
    static void DeformSpikeOut(
        Mesh mesh, Vector3 impactDir, float craterAngle, float heightFrac, int seed, float maxRadius)
    {
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
        mesh.RecalculateBounds();
    }
}
