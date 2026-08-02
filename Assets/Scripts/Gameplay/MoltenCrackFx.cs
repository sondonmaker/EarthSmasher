using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 용암 원 밖으로 뻗는 빛나는 균열 — 텍스처 + 구면 발광 리본.
/// </summary>
public class MoltenCrackFx : MonoBehaviour
{
    public static IEnumerator Play(EarthPlanet earth, Vector3 worldImpact, float craterRadiusNorm)
    {
        if (earth == null)
            yield break;

        var fx = earth.GetComponent<MoltenCrackFx>();
        if (fx == null)
            fx = earth.gameObject.AddComponent<MoltenCrackFx>();

        yield return fx.Run(worldImpact, craterRadiusNorm);
    }

    IEnumerator Run(Vector3 worldImpact, float craterRadiusNorm)
    {
        var scorch = EarthSurfaceScorch.Ensure(GetComponent<EarthPlanet>());
        float R = GetComponent<EarthPlanet>().Radius;
        Vector3 center = transform.position;
        Vector3 n = (worldImpact - center).normalized;

        // 웨이브마다 더 멀리 갈라짐
        yield return SpawnWave(scorch, worldImpact, n, center, R, craterRadiusNorm, 0.6f, 1.45f, 12, true);
        yield return Wait(0.22f);
        yield return SpawnWave(scorch, worldImpact, n, center, R, craterRadiusNorm, 0.7f, 2.15f, 16, true);
        CameraShake.Shake(0.5f, 0.5f);
        yield return Wait(0.26f);
        yield return SpawnWave(scorch, worldImpact, n, center, R, craterRadiusNorm, 0.82f, 2.85f, 14, false);
        yield return Wait(0.2f);
    }

    IEnumerator SpawnWave(
        EarthSurfaceScorch scorch,
        Vector3 worldImpact,
        Vector3 impactN,
        Vector3 center,
        float R,
        float craterRadiusNorm,
        float startFrac,
        float endMul,
        int branches,
        bool hot)
    {
        if (scorch != null)
            scorch.PaintMoltenFissures(worldImpact, craterRadiusNorm, startFrac, endMul, branches);

        // 3D 발광 리본 (레퍼런스처럼 빛나 보이게)
        var paths = BuildBranchPaths(impactN, craterRadiusNorm, startFrac, endMul, branches);
        var root = new GameObject("MoltenCrackWave");
        root.transform.SetParent(transform, false);

        for (int i = 0; i < paths.Count; i++)
            SpawnRibbon(root.transform, paths[i], R, hot, i);

        yield return null;
    }

    List<List<Vector3>> BuildBranchPaths(Vector3 impactN, float craterR, float startFrac, float endMul, int branches)
    {
        var list = new List<List<Vector3>>();
        Vector3 t = Vector3.Cross(impactN, Vector3.up);
        if (t.sqrMagnitude < 1e-4f)
            t = Vector3.Cross(impactN, Vector3.right);
        t.Normalize();
        Vector3 b = Vector3.Cross(impactN, t).normalized;

        float startAng = craterR * startFrac * 1.1f;
        float endAng = craterR * endMul * 1.1f;
        float baseYaw = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < branches; i++)
        {
            var path = new List<Vector3>();
            float yaw = baseYaw + (Mathf.PI * 2f * i / branches) + Random.Range(-0.25f, 0.25f);
            float ang = startAng;
            float dir = yaw;
            bool fork = Random.value > 0.5f;
            float forkAt = Random.Range(0.35f, 0.7f);
            int steps = Mathf.RoundToInt(Mathf.Lerp(18f, 36f, (endAng - startAng)));

            for (int s = 0; s <= steps; s++)
            {
                float u = s / (float)steps;
                if (u > 0f)
                {
                    dir += Random.Range(-0.2f, 0.2f);
                    if (fork && u > forkAt)
                        dir += Random.Range(-0.45f, 0.45f);
                    ang += (endAng - startAng) / steps * Random.Range(0.85f, 1.2f);
                }

                float a = Mathf.Min(ang, endAng);
                Vector3 d = (impactN * Mathf.Cos(a)
                    + t * (Mathf.Cos(dir) * Mathf.Sin(a))
                    + b * (Mathf.Sin(dir) * Mathf.Sin(a))).normalized;
                path.Add(d);
            }

            // 가끔 짧은 갈래
            if (fork && path.Count > 8)
            {
                var side = new List<Vector3>();
                int mid = path.Count / 2;
                float sideDir = dir + Random.Range(0.6f, 1.1f) * (Random.value > 0.5f ? 1f : -1f);
                float a0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(path[mid], impactN), -1f, 1f));
                for (int s = 0; s < path.Count / 3; s++)
                {
                    float a = a0 + (endAng - a0) * (s / (float)(path.Count / 3));
                    sideDir += Random.Range(-0.15f, 0.15f);
                    Vector3 d = (impactN * Mathf.Cos(a)
                        + t * (Mathf.Cos(sideDir) * Mathf.Sin(a))
                        + b * (Mathf.Sin(sideDir) * Mathf.Sin(a))).normalized;
                    side.Add(d);
                }
                if (side.Count >= 2)
                    list.Add(side);
            }

            if (path.Count >= 2)
                list.Add(path);
        }

        return list;
    }

    void SpawnRibbon(Transform parent, List<Vector3> dirs, float planetR, bool hot, int seed)
    {
        if (dirs == null || dirs.Count < 2)
            return;

        // Earth 로컬 메시 반지름 ≈ 0.5
        const float localR = 0.5f;
        float halfW = localR * (hot ? 0.018f : 0.012f) * Random.Range(0.8f, 1.3f);

        var go = new GameObject("MoltenRibbon");
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildRibbonMesh(dirs, halfW, localR * 1.007f);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateGlowMat(hot);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var pulse = go.AddComponent<MoltenGlowPulse>();
        pulse.Init(mr.material, hot ? 2.4f : 1.4f, seed * 0.37f);
    }

    static Mesh BuildRibbonMesh(List<Vector3> unitDirs, float halfWidth, float radius)
    {
        int n = unitDirs.Count;
        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var norms = new Vector3[n * 2];

        for (int i = 0; i < n; i++)
        {
            Vector3 d = unitDirs[i].normalized;
            Vector3 prev = i > 0 ? unitDirs[i - 1] : unitDirs[i];
            Vector3 next = i < n - 1 ? unitDirs[i + 1] : unitDirs[i];
            Vector3 tangent = (next - prev).normalized;
            if (tangent.sqrMagnitude < 1e-6f)
                tangent = Vector3.Cross(d, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(d, tangent).normalized;
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.Cross(d, Vector3.right).normalized;

            verts[i * 2] = (d * radius) + side * halfWidth;
            verts[i * 2 + 1] = (d * radius) - side * halfWidth;
            // re-project slightly onto sphere shell for cleaner look
            verts[i * 2] = verts[i * 2].normalized * radius;
            verts[i * 2 + 1] = verts[i * 2 + 1].normalized * radius;
            norms[i * 2] = d;
            norms[i * 2 + 1] = d;
            float v = i / (float)(n - 1);
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        var tris = new int[(n - 1) * 6];
        int ti = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 2;
            int b = a + 1;
            int c = a + 2;
            int d = a + 3;
            tris[ti++] = a;
            tris[ti++] = c;
            tris[ti++] = b;
            tris[ti++] = b;
            tris[ti++] = c;
            tris[ti++] = d;
        }

        var mesh = new Mesh { name = "MoltenRibbon" };
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material CreateGlowMat(bool hot)
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
        }
        Color tint = hot ? new Color(1f, 0.55f, 0.15f) : new Color(0.95f, 0.35f, 0.08f);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        mat.EnableKeyword("_EMISSION");
        if (emit != null && mat.HasProperty("_EmissionMap"))
            mat.SetTexture("_EmissionMap", emit);
        mat.SetColor("_EmissionColor", (hot ? new Color(2.2f, 0.7f, 0.12f) : new Color(1.4f, 0.35f, 0.06f)));
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.55f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.2f);
        return mat;
    }

    IEnumerator Wait(float sec)
    {
        float left = sec;
        while (left > 0f)
        {
            float sim = WorldStatusHud.Instance != null ? Mathf.Max(0.05f, WorldStatusHud.Instance.SimSpeed) : 1f;
            left -= Time.unscaledDeltaTime * sim;
            yield return null;
        }
    }
}

public class MoltenGlowPulse : MonoBehaviour
{
    Material mat;
    Color baseEmit;
    float phase;

    public void Init(Material m, float intensity, float phaseOffset)
    {
        mat = m;
        phase = phaseOffset;
        baseEmit = mat != null && mat.HasProperty("_EmissionColor")
            ? mat.GetColor("_EmissionColor")
            : new Color(1.5f, 0.4f, 0.08f) * intensity;
    }

    void Update()
    {
        if (mat == null)
            return;
        float p = 0.75f + 0.35f * Mathf.Sin(Time.time * 4.2f + phase);
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", baseEmit * p);
    }
}
