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
    [SerializeField] float maxDigDepth = 0.38f;
    [SerializeField] float minShellRadius = 0.15f; // 코어 근처까지 — 얕은 껍질 벗김 방지

    Mesh workingCrust;
    Mesh workingMantle;
    MeshFilter mantleFilter;
    Vector3[] pristineVerts;
    Vector3[] pristineMantleVerts;
    int lockedVertexCount;
    int lockedUvCount;
    bool ready;
    bool meshDirty;
    bool mantleDirty;
    int digCountSalt;
    readonly List<DigSite> sites = new List<DigSite>();

    Material mantleOriginalMat;

    public int DigSiteCount => sites.Count;

    class DigSite
    {
        public Vector3 dir;
        public int hits;
        public float dug;
        public float pepeFloorR;
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
        if (ready && workingCrust != null)
            return;

        ready = false;
        workingCrust = null;

        ResolveCrustFilter();
        if (crustFilter == null)
        {
            Debug.LogWarning("[EarthCraterDeform] No crust MeshFilter — mesh dig disabled.");
            return;
        }

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

        if (workingMantle != null && pristineMantleVerts != null
            && pristineMantleVerts.Length == workingMantle.vertexCount)
        {
            workingMantle.vertices = pristineMantleVerts;
            workingMantle.RecalculateBounds();
            mantleDirty = true;
        }

        Transform mantle = transform.Find("Mantle");
        if (mantle != null)
        {
            var rend = mantle.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.enabled = true;
                if (mantleOriginalMat != null)
                    rend.sharedMaterial = mantleOriginalMat;
            }
        }

        RestorePepeBoreVisuals();
    }

    public bool TryExportVertices(out Vector3[] verts)
    {
        EnsureReady();
        if (workingCrust == null)
        {
            verts = null;
            return false;
        }

        verts = workingCrust.vertices;
        return verts != null && verts.Length > 0;
    }

    public bool TryImportVertices(Vector3[] verts)
    {
        EnsureReady();
        if (workingCrust == null || verts == null || verts.Length != workingCrust.vertexCount)
            return false;

        workingCrust.vertices = verts;
        workingCrust.RecalculateBounds();
        meshDirty = true;

        var col = GetComponent<MeshCollider>();
        if (col != null)
            col.sharedMesh = workingCrust;
        return true;
    }

    /// <summary>0~1 — 메시가 얼마나 판/깎였는지.</summary>
    public float SampleCrustDamage01()
    {
        EnsureReady();
        if (workingCrust == null || pristineVerts == null)
            return 0f;

        Vector3[] verts = workingCrust.vertices;
        if (verts == null || verts.Length == 0)
            return 0f;

        int step = Mathf.Max(1, verts.Length / 9000);
        float sum = 0f;
        int n = 0;
        for (int i = 0; i < verts.Length; i += step)
        {
            float baseMag = pristineVerts[i].magnitude;
            if (baseMag < 1e-5f)
                continue;
            float shrink = Mathf.Clamp01((baseMag - verts[i].magnitude) / (baseMag * 0.2f));
            sum += shrink;
            n++;
        }

        float avg = n > 0 ? sum / n : 0f;
        float siteBoost = Mathf.InverseLerp(0f, 36f, sites.Count) * 0.38f;
        return Mathf.Clamp01(avg * 1.2f + siteBoost);
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

    /// <summary>고양이 할퀴기 — 표면을 따라 긴 홈을 여러 줄 파낸다.</summary>
    public void ScratchGrooves(Vector3 worldPoint, Vector3 worldNormal, int slashes, float lengthNorm, float depthNorm, int seed = 0, int maxStepsPerSlash = 0)
    {
        EnsureReady();
        if (workingCrust == null || !UvLockIntact(workingCrust))
            return;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return;

        Vector3 center = local.normalized;
        Vector3 tangent = Vector3.Cross(center, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(center, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(center, tangent).normalized;

        if (seed == 0)
            seed = HashDir(center) ^ 0xCA7;

        slashes = Mathf.Clamp(slashes, 2, 6);
        float spread = Mathf.Clamp(lengthNorm, 0.06f, 0.22f);
        float depth = Mathf.Clamp(depthNorm, 0.015f, 0.06f);
        float grooveW = Mathf.Clamp(spread * 0.18f, 0.012f, 0.035f);
        int steps = maxStepsPerSlash > 0
            ? maxStepsPerSlash
            : Mathf.Max(8, Mathf.RoundToInt(spread * 120f));

        var rng = new System.Random(seed);
        float baseAngle = Mathf.Lerp(-28f, -8f, (float)rng.NextDouble());

        for (int i = 0; i < slashes; i++)
        {
            float ang = baseAngle + i * (56f / Mathf.Max(1, slashes - 1));
            Vector3 slashAxis = (Quaternion.AngleAxis(ang, bitangent) * tangent).normalized;
            Vector3 grooveEnd = (center + slashAxis * spread * 1.15f).normalized;

            for (int s = 0; s < steps; s++)
            {
                float t = steps <= 1 ? 0f : s / (float)(steps - 1);
                Vector3 dir = Vector3.Slerp(center, grooveEnd, t).normalized;
                float falloff = 1f - t * 0.35f;
                DeformVerticesOnly(
                    workingCrust,
                    dir,
                    grooveW * (0.85f + 0.15f * (1f - t)),
                    depth * falloff,
                    0f,
                    seed + i * 131 + s * 17,
                    minShellRadius);
            }
        }

        if (!UvLockIntact(workingCrust))
        {
            ready = false;
            workingCrust = null;
            return;
        }

        RefreshCollider();
    }

    /// <summary>평행 발톱 — scratchAxis 방향으로 clawCount 줄의 긴 홈.</summary>
    public void ScratchGroovesParallel(
        Vector3 worldPoint, Vector3 worldNormal, Vector3 scratchAxisWorld,
        int clawCount, float lengthNorm, float spreadNorm, float depthNorm,
        int seed = 0, int maxStepsPerSlash = 0)
    {
        EnsureReady();
        if (workingCrust == null || !UvLockIntact(workingCrust))
            return;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return;

        Vector3 center = local.normalized;
        Vector3 axis = Vector3.ProjectOnPlane(transform.InverseTransformDirection(scratchAxisWorld), center);
        if (axis.sqrMagnitude < 1e-4f)
            axis = Vector3.Cross(center, Vector3.up);
        axis.Normalize();
        Vector3 spreadDir = Vector3.Cross(center, axis).normalized;

        if (seed == 0)
            seed = HashDir(center) ^ 0xCA71;

        clawCount = Mathf.Clamp(clawCount, 2, 5);
        float length = Mathf.Clamp(lengthNorm, 0.08f, 0.24f);
        float laneSpread = Mathf.Clamp(spreadNorm, 0.015f, 0.055f);
        float depth = Mathf.Clamp(depthNorm, 0.018f, 0.075f);
        float grooveW = Mathf.Clamp(length * 0.14f, 0.014f, 0.042f);
        int steps = maxStepsPerSlash > 0
            ? maxStepsPerSlash
            : Mathf.Max(10, Mathf.RoundToInt(length * 140f));

        for (int i = 0; i < clawCount; i++)
        {
            float lane = (i - (clawCount - 1) * 0.5f) * laneSpread;
            Vector3 startDir = (center + spreadDir * lane).normalized;
            Vector3 endDir = (center + spreadDir * lane + axis * length).normalized;

            for (int s = 0; s < steps; s++)
            {
                float t = steps <= 1 ? 0f : s / (float)(steps - 1);
                Vector3 dir = Vector3.Slerp(startDir, endDir, t).normalized;
                float falloff = 1f - t * 0.25f;
                DeformVerticesOnly(
                    workingCrust,
                    dir,
                    grooveW * (0.9f + 0.1f * (1f - t)),
                    depth * falloff,
                    0f,
                    seed + i * 173 + s * 19,
                    minShellRadius);
            }
        }

        if (!UvLockIntact(workingCrust))
        {
            ready = false;
            workingCrust = null;
            return;
        }

        RefreshCollider();
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

        float hitMul = 1f + (site.hits - 1) * (huge ? 0.85f : 0.7f);
        float depth = Mathf.Clamp(depthNorm * hitMul, 0.035f, maxDigDepth);
        float radius = Mathf.Clamp(radiusNorm * (1f + (site.hits - 1) * 0.1f), 0.05f, 0.42f);
        float rimH = depth * Mathf.Lerp(0.45f, 0.25f, Mathf.Clamp01((site.hits - 1) / 5f));
        float minR = Mathf.Lerp(0.32f, minShellRadius, Mathf.Clamp01((site.hits - 1) / 6f));

        if (seed == 0)
            seed = HashDir(site.dir) ^ (site.hits * 7919);

        site.dug = Mathf.Min(maxDigDepth, site.dug + depth * 0.55f);

        // crust만 변형. Ocean/Clouds는 건드리지 않음 (레이어·투명 이슈 방지).
        DeformVerticesOnly(workingCrust, site.dir, radius, depth, rimH, seed, minR);

        if (!UvLockIntact(workingCrust))
        {
            Debug.LogError("[EarthCraterDeform] Dig corrupted UVs — further dig disabled.");
            ready = false;
            workingCrust = null;
            return 0;
        }

        RefreshCollider();
        if (site.hits >= 3 || site.dug > 0.14f)
            RevealCore();

        return site.hits;
    }

    /// <summary>블랙홀용: 안쪽으로만 파냄. 림/누적 타격 없음 (스파이크 방지).</summary>
    public void CarveHole(Vector3 worldPoint, float radiusNorm, float depthNorm)
    {
        DrillBore(worldPoint, radiusNorm, depthNorm, minShellRadius);
    }

    /// <summary>홀드 빔 — 표면을 따라 얕은 홈을 이어 파낸다.</summary>
    public void DigGrooveSegment(Vector3 fromWorld, Vector3 toWorld, float radiusNorm, float depthNorm, int seed = 0)
    {
        EnsureReady();
        if (workingCrust == null || !UvLockIntact(workingCrust))
            return;

        Vector3 center = transform.position;
        Vector3 a = (fromWorld - center).normalized;
        Vector3 b = (toWorld - center).normalized;
        if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f)
        {
            DrillBore(toWorld, radiusNorm, depthNorm, minShellRadius);
            return;
        }

        float angleDeg = Vector3.Angle(a, b);
        int steps = Mathf.Clamp(Mathf.CeilToInt(angleDeg / 1.6f), 1, 28);
        float radius = Mathf.Clamp(radiusNorm, 0.006f, 0.04f);
        float depth = Mathf.Clamp(depthNorm, 0.006f, 0.045f);
        if (seed == 0)
            seed = HashDir(a) ^ HashDir(b);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 dir = Vector3.Slerp(a, b, t).normalized;
            float fall = 1f - t * 0.1f;
            DeformVerticesOnly(workingCrust, dir, radius, depth * fall, 0f, seed + i * 41, minShellRadius);
        }

        if (!UvLockIntact(workingCrust))
        {
            ready = false;
            workingCrust = null;
            return;
        }

        RefreshCollider();
    }

    /// <summary>같은 지점 반복 타격 누적 횟수 (없으면 0).</summary>
    public int GetSiteHitCount(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return 0;
        var site = FindSite(local.normalized);
        return site != null ? site.hits : 0;
    }

    /// <summary>0~1 — 이 지점이 얼마나 깊게 파였는지.</summary>
    public float GetSiteDepth01(Vector3 worldPoint)
    {
        int hits = GetSiteHitCount(worldPoint);
        return hits <= 0 ? 0f : Mathf.Clamp01((hits - 1) / 9f);
    }

    /// <summary>
    /// 드릴/블랙홀: 림 없이 안쪽으로만. shellFloor가 낮을수록 더 깊은 구멍.
    /// 같은 지점이면 Pepe 펀치처럼 점점 더 깊게 파인다.
    /// </summary>
    public int DrillBore(Vector3 worldPoint, float radiusNorm, float depthNorm, float shellFloor, bool widenOnRepeat = true)
    {
        EnsureReady();
        if (workingCrust == null || !UvLockIntact(workingCrust))
            return 0;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return 0;

        DigSite site = FindOrCreateSite(local.normalized);
        site.hits++;
        site.dir = Vector3.Slerp(site.dir, local.normalized, 0.35f).normalized;

        float hitMul = 1f + (site.hits - 1) * (widenOnRepeat ? 0.38f : 0.58f);
        float depthCap = maxDigDepth + (widenOnRepeat ? 0.06f : 0.24f);
        float depth = Mathf.Clamp(depthNorm * hitMul, 0.02f, depthCap);
        float radius = widenOnRepeat
            ? Mathf.Clamp(radiusNorm * (1f + (site.hits - 1) * 0.05f), 0.05f, 0.36f)
            : Mathf.Clamp(radiusNorm * (1f + (site.hits - 1) * 0.012f), 0.04f, 0.24f);
        float floor = ResolveDigFloor(shellFloor, site.hits, minShellRadius);
        site.dug = Mathf.Min(maxDigDepth, site.dug + depth * 0.55f);
        digCountSalt++;
        DeformVerticesOnly(workingCrust, site.dir, radius, depth, 0f, HashDir(site.dir) ^ digCountSalt, floor);

        if (!UvLockIntact(workingCrust))
        {
            Debug.LogError("[EarthCraterDeform] DrillBore corrupted UVs.");
            ready = false;
            workingCrust = null;
            return 0;
        }

        if (site.hits >= 3 || site.dug > 0.14f)
            RevealCore();

        RefreshCollider();
        return site.hits;
    }

    /// <summary>
    /// Pepe 펀치 — 한 패스 bore. 바닥 반경(pepeFloorR)은 같은 자리에서 절대 올라가지 않음.
    ///
    /// 설계 요약 (EarthCrack):
    /// - PepeBoreDig: 중심=floorR, rim=현재 vertex 반경 (wallPower로 가파른 shaft).
    /// - pepeFloorR: DigSite에 저장, 타격마다 Min()으로만 갱신 → 반복 타격 시 더 깊어짐.
    /// - Mantle 렌더러는 파기 시작 시 숨김(회색 껍질 방지), Core 발광으로 용암 바닥.
    /// - 텍스처 BurnAt/PaintDeepOreInterior 사용 안 함 — 메시 변형만.
    /// - MemePepeUnit: DoFlurryPunch/EndFlurry에서 PepePunch(hit, progress) 호출.
    /// </summary>
    public int PepePunch(Vector3 worldPoint, float progress01)
    {
        EnsureReady();
        if (workingCrust == null || !UvLockIntact(workingCrust))
            return 0;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f)
            return 0;

        DigSite site = FindOrCreateSite(local.normalized);
        site.hits++;
        site.dir = Vector3.Slerp(site.dir, local.normalized, 0.4f).normalized;

        progress01 = Mathf.Clamp01(progress01);
        float shellR = GetTypicalShellRadius();
        float coreR = shellR * 0.22f;
        float minFloor = coreR * 0.38f;

        if (site.pepeFloorR <= 0f)
            site.pepeFloorR = shellR;

        float wantFloor = Mathf.Lerp(shellR * 0.28f, minFloor, progress01);
        wantFloor -= (site.hits - 1) * shellR * 0.09f;
        wantFloor = Mathf.Max(minFloor, wantFloor);
        site.pepeFloorR = Mathf.Min(site.pepeFloorR, wantFloor);

        float mouthRad = Mathf.Lerp(0.074f, 0.11f, progress01);
        mouthRad *= Mathf.Min(1.2f, 1f + (site.hits - 1) * 0.011f);
        const float wallPower = 3.45f;

        digCountSalt++;
        PepeBoreDig(workingCrust, site.dir, mouthRad, site.pepeFloorR, wallPower);

        EnsureMantleReady();
        if (workingMantle != null && UvLockIntact(workingMantle))
        {
            float mantleR = GetTypicalShellRadius(pristineMantleVerts);
            float mantleFloor = site.pepeFloorR * (mantleR / Mathf.Max(1e-4f, shellR));
            PepeBoreDig(workingMantle, site.dir, mouthRad * 1.03f, mantleFloor, wallPower * 0.96f);
            mantleDirty = true;
        }

        site.dug = (shellR - site.pepeFloorR) / Mathf.Max(1e-4f, shellR);

        if (!UvLockIntact(workingCrust))
        {
            ready = false;
            workingCrust = null;
            return 0;
        }

        UpdatePepeBoreVisuals(site.pepeFloorR, shellR, coreR, minFloor);
        RefreshCollider();
        return site.hits;
    }

    static void PepeBoreDig(Mesh mesh, Vector3 axis, float mouthRad, float floorR, float wallPower)
    {
        if (mesh == null || mouthRad < 1e-5f)
            return;

        axis = axis.normalized;
        var verts = mesh.vertices;
        bool changed = false;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            float len = v.magnitude;
            if (len < 1e-6f)
                continue;

            Vector3 n = v / len;
            float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(n, axis), -1f, 1f));
            if (ang > mouthRad)
                continue;

            float t = ang / mouthRad;
            float goal = Mathf.Lerp(floorR, len, Mathf.Pow(t, wallPower));
            if (goal >= len - 1e-6f)
                continue;

            verts[i] = n * goal;
            changed = true;
        }

        if (!changed)
            return;

        mesh.vertices = verts;
        mesh.RecalculateBounds();
    }

    void UpdatePepeBoreVisuals(float floorR, float shellR, float coreR, float minFloor)
    {
        Transform mantle = transform.Find("Mantle");
        if (mantle != null)
        {
            var rend = mantle.GetComponent<Renderer>();
            if (rend != null)
            {
                if (mantleOriginalMat == null)
                    mantleOriginalMat = rend.sharedMaterial;
                rend.enabled = floorR >= shellR * 0.97f;
            }
        }

        RevealCoreForPepe(floorR, shellR, coreR, minFloor);
    }

    void RestorePepeBoreVisuals()
    {
        mantleOriginalMat = null;

        Transform mantle = transform.Find("Mantle");
        if (mantle != null)
        {
            var rend = mantle.GetComponent<Renderer>();
            if (rend != null)
                rend.enabled = true;
        }

        Transform core = transform.Find("Core");
        if (core != null)
        {
            var rend = core.GetComponent<Renderer>();
            if (rend != null)
                rend.SetPropertyBlock(null);
        }
    }

    static bool IsDecorLayerMesh(MeshFilter mf)
    {
        string n = mf.gameObject.name;
        return n.IndexOf("Cloud", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Ocean", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Atmos", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Aurora", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Halo", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void RevealCoreForPepe(float floorR, float shellR, float coreR, float minFloor)
    {
        Transform core = transform.Find("Core");
        if (core == null)
            return;

        core.gameObject.SetActive(true);

        float span = Mathf.Max(1e-4f, shellR - minFloor);
        float dug01 = Mathf.Clamp01((shellR - floorR) / span);
        core.localScale = Vector3.one * Mathf.Lerp(0.24f, 0.68f, dug01);

        var rend = core.GetComponent<Renderer>();
        if (rend != null)
        {
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            float glow = Mathf.Lerp(1.2f, 4f, dug01);
            mpb.SetColor("_EmissionColor", new Color(1.5f, 0.42f, 0.07f) * glow);
            if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_Color"))
                mpb.SetColor("_Color", Color.Lerp(new Color(0.55f, 0.22f, 0.1f), new Color(1f, 0.38f, 0.08f), dug01));
            rend.SetPropertyBlock(mpb);
        }
    }

    void EnsureMantleReady()
    {
        if (workingMantle != null)
            return;

        if (mantleFilter == null)
        {
            Transform mantle = transform.Find("Mantle");
            if (mantle != null)
                mantleFilter = mantle.GetComponent<MeshFilter>();
        }

        if (mantleFilter == null || mantleFilter.sharedMesh == null)
            return;

        workingMantle = CloneWritableMeshPreserveUv(mantleFilter, "EarthMantleDeform");
        if (workingMantle == null)
            return;

        pristineMantleVerts = workingMantle.vertices;
    }

    void ResolveCrustFilter()
    {
        if (crustFilter != null)
            return;

        crustFilter = GetComponent<MeshFilter>();
        if (crustFilter != null && crustFilter.sharedMesh != null)
            return;

        var planet = GetComponent<EarthPlanet>();
        if (planet != null && planet.CrustRenderer != null)
        {
            crustFilter = planet.CrustRenderer.GetComponent<MeshFilter>();
            if (crustFilter != null && crustFilter.sharedMesh != null)
                return;
        }

        MeshFilter best = null;
        int bestVerts = 0;
        var filters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            var mf = filters[i];
            if (mf == null || mf.sharedMesh == null || IsDecorLayerMesh(mf))
                continue;
            int vc = mf.sharedMesh.vertexCount;
            if (vc > bestVerts)
            {
                bestVerts = vc;
                best = mf;
            }
        }

        crustFilter = best;
    }

    float GetTypicalShellRadius()
    {
        return GetTypicalShellRadius(pristineVerts);
    }

    static float GetTypicalShellRadius(Vector3[] verts)
    {
        if (verts != null && verts.Length > 0)
        {
            float sum = 0f;
            int count = 0;
            int step = Mathf.Max(1, verts.Length / 64);
            for (int i = 0; i < verts.Length; i += step)
            {
                sum += verts[i].magnitude;
                count++;
            }

            if (count > 0)
                return sum / count;
        }

        return 0.5f;
    }

    /// <summary>깊게 파이면 안쪽 Mantle/Core 레이어가 보이도록.</summary>
    void RevealCore()
    {
        Transform mantle = transform.Find("Mantle");
        if (mantle != null && !mantle.gameObject.activeSelf)
            mantle.gameObject.SetActive(true);

        Transform core = transform.Find("Core");
        if (core != null && !core.gameObject.activeSelf)
            core.gameObject.SetActive(true);
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

    DigSite FindSite(Vector3 dir)
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
        return best;
    }

    DigSite FindOrCreateSite(Vector3 dir)
    {
        var existing = FindSite(dir);
        if (existing != null)
            return existing;

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
        if (workingCrust != null && meshDirty)
        {
            meshDirty = false;
            workingCrust.RecalculateNormals();

            if (crustFilter != null && crustFilter.sharedMesh != workingCrust)
                crustFilter.mesh = workingCrust;

            var col = GetComponent<MeshCollider>();
            if (col != null)
            {
                col.sharedMesh = null;
                col.sharedMesh = workingCrust;
            }
        }

        if (workingMantle != null && mantleDirty)
        {
            mantleDirty = false;
            workingMantle.RecalculateNormals();

            if (mantleFilter != null && mantleFilter.sharedMesh != workingMantle)
                mantleFilter.mesh = workingMantle;
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

    /// <summary>반복 타격 시 바닥 반경을 점점 낮춰 지구 안쪽으로 깊게 파낸다.</summary>
    static float ResolveDigFloor(float shellFloor, int hits, float minShell)
    {
        float start = Mathf.Max(shellFloor, 0.32f);
        float progressive = Mathf.Lerp(start, minShell, Mathf.Clamp01((hits - 1) / 6f));
        float stepped = shellFloor - (hits - 1) * 0.028f;
        return Mathf.Clamp(Mathf.Min(progressive, stepped), minShell, 0.45f);
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
        Mesh mesh, Vector3 impactDir, float craterAngle, float depthFrac, float rimFrac, int seed, float minRadius,
        bool deepBore = false)
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
                float wallSteep = deepBore ? 0.26f : 0.38f;
                float boostPeak = deepBore ? 2.1f : 1.45f;
                float wall = 1f - Mathf.SmoothStep(wallSteep, 1f, t);
                float centerBoost = Mathf.Lerp(boostPeak, 1f, t);
                float floorRough = 1f + 0.35f * (SurfaceNoise(n, 22f, noiseSeed + 5.3f) - 0.5f);
                float asym = 1f + 0.15f * Mathf.Sin(phi + p1);
                radialDelta -= depthFrac * len * wall * floorRough * depthBias * asym * centerBoost;

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
