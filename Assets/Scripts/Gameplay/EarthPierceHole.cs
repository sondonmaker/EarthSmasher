using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 피어싱 레이저 관통구.
///
/// 구멍은 오브젝트(로컬) 공간으로 저장한다. 월드 공간으로 두면 지구가 자전할 때
/// 구멍만 제자리에 남아 표면을 가로질러 흘러가고, 결국 사라진 것처럼 보인다.
/// </summary>
public class EarthPierceHole : MonoBehaviour
{
    public static EarthPierceHole Ensure(EarthPlanet earth)
    {
        if (earth == null)
            return null;
        var h = earth.GetComponent<EarthPierceHole>();
        if (h == null)
            h = earth.gameObject.AddComponent<EarthPierceHole>();
        h.earth = earth;
        h.BindMaterial();
        return h;
    }

    struct Hole
    {
        public Vector3 localAxis;
        public float localRadius;
        public GameObject tunnel;
    }

    [System.Serializable]
    public struct HoleSnapshot
    {
        public float ax, ay, az, radius;
    }

    EarthPlanet earth;
    readonly List<Hole> holes = new List<Hole>();
    Material crustMat;
    Vector4[] axisBuffer;

    // 셰이더 배열 크기와 반드시 같아야 한다 (EarthFromSpace.shader MAX_PIERCE)
    const int MaxHoles = 16;

    void BindMaterial()
    {
        if (earth == null)
            earth = GetComponent<EarthPlanet>();
        var rend = GetComponent<Renderer>();
        if (rend != null)
            crustMat = rend.material;
        if (axisBuffer == null)
            axisBuffer = new Vector4[MaxHoles];
    }

    float LocalShellRadius()
    {
        var col = earth.GetComponent<SphereCollider>();
        return col != null ? col.radius : 0.5f;
    }

    public void AddPierce(Vector3 entryWorld, Vector3 exitWorld, float radiusWorld)
    {
        BindMaterial();
        CleanupLegacyJunk();

        Vector3 center = earth.transform.position;
        Vector3 worldAxis = (exitWorld - entryWorld).normalized;
        if (worldAxis.sqrMagnitude < 1e-6f)
            worldAxis = (entryWorld - center).normalized;

        float shell = LocalShellRadius();
        float worldToLocal = shell / Mathf.Max(1e-4f, earth.Radius);
        float localRadius = Mathf.Clamp(radiusWorld, earth.Radius * 0.1f, earth.Radius * 0.28f) * worldToLocal;
        Vector3 localAxis = earth.transform.InverseTransformDirection(worldAxis).normalized;

        for (int i = 0; i < holes.Count; i++)
        {
            if (Vector3.Dot(holes[i].localAxis, localAxis) > 0.985f)
            {
                float mergedR = Mathf.Max(holes[i].localRadius, localRadius);
                if (holes[i].tunnel != null)
                    Destroy(holes[i].tunnel);

                holes[i] = new Hole
                {
                    localAxis = localAxis,
                    localRadius = mergedR,
                    tunnel = BuildMantleBore(localAxis, mergedR, shell)
                };
                PushToShader();
                FinishPierceSurface(entryWorld, exitWorld, mergedR / shell * 1.08f);
                return;
            }
        }

        while (holes.Count >= MaxHoles)
        {
            if (holes[0].tunnel != null)
                Destroy(holes[0].tunnel);
            holes.RemoveAt(0);
        }

        holes.Add(new Hole
        {
            localAxis = localAxis,
            localRadius = localRadius,
            tunnel = BuildMantleBore(localAxis, localRadius, shell)
        });
        PushToShader();
        FinishPierceSurface(entryWorld, exitWorld, localRadius / shell * 1.08f);
    }

    void FinishPierceSurface(Vector3 entryWorld, Vector3 exitWorld, float carveNorm)
    {
        CleanupLavaNearPierce(entryWorld, exitWorld, carveNorm * 1.35f);
        var scorch = EarthSurfaceScorch.Ensure(earth);
        scorch?.CarveOpening(entryWorld, carveNorm);
        scorch?.CarveOpening(exitWorld, carveNorm * 0.95f);
        ReapplyShader();
    }

    void CleanupLavaNearPierce(Vector3 entryWorld, Vector3 exitWorld, float radiusNorm)
    {
        float angleRad = Mathf.Asin(Mathf.Clamp(radiusNorm, 0.01f, 0.95f)) * 1.45f;
        float cosThreshold = Mathf.Cos(angleRad);
        Vector3 center = earth.transform.position;
        Vector3 entryDir = (entryWorld - center).normalized;
        Vector3 exitDir = (exitWorld - center).normalized;
        for (int i = earth.transform.childCount - 1; i >= 0; i--)
        {
            var ch = earth.transform.GetChild(i);
            if (!ch.name.StartsWith("LavaHit"))
                continue;

            var mf = ch.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                continue;

            Vector3 localCenter = mf.sharedMesh.bounds.center;
            Vector3 worldDir = earth.transform.TransformDirection(localCenter).normalized;
            if (Vector3.Dot(worldDir, entryDir) >= cosThreshold
                || Vector3.Dot(worldDir, exitDir) >= cosThreshold)
                Object.Destroy(ch.gameObject);
        }
    }

    /// <summary>
    /// 구멍을 감싸는 암반 터널. 안쪽은 뚫려 반대편이 보이고, 벽 두께로 지각이 두꺼워 보인다.
    /// </summary>
    GameObject BuildMantleBore(Vector3 localAxis, float innerRadius, float shell)
    {
        // 안쪽 반경 = 셰이더 clip 반경과 일치. 안쪽은 울퉁불퉁하게 하지 않아 시야를 막지 않음.
        float innerR = innerRadius;
        float outerR = innerR + shell * 0.028f;
        float halfLen = Mathf.Sqrt(Mathf.Max(1e-4f, shell * shell - outerR * outerR)) * 0.992f;

        var root = new GameObject("PierceMantleBore");
        root.transform.SetParent(earth.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localAxis);

        var mf = root.AddComponent<MeshFilter>();
        var mr = root.AddComponent<MeshRenderer>();
        mf.sharedMesh = BuildThickMantleTube(innerR, outerR, halfLen, 36, 16);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var shader = Shader.Find("EarthSmasher/MantlePierce");
        Material mat;
        if (shader != null)
        {
            mat = new Material(shader);
            mat.SetColor("_Color", new Color(0.11f, 0.07f, 0.05f, 1f));
            mat.SetColor("_MoltenColor", new Color(0.55f, 0.14f, 0.04f, 1f));
            mat.SetFloat("_Emission", 0.12f);
        }
        else
        {
            mat = RuntimeMaterial.Opaque(new Color(0.14f, 0.08f, 0.06f), 0.2f);
        }
        mr.material = mat;
        return root;
    }

    /// <summary>속이 빈 원통 벽 (inner→outer). 끝 링 캡 없음 — 시야가 막히지 않게.</summary>
    static Mesh BuildThickMantleTube(float innerR, float outerR, float halfLen, int segments, int rings)
    {
        int sliceCount = rings + 1;
        int ringStride = segments * 2;
        int wallVertCount = sliceCount * ringStride;
        var verts = new Vector3[wallVertCount];
        var norms = new Vector3[wallVertCount];
        var uvs = new Vector2[wallVertCount];
        var tris = new List<int>(rings * segments * 12);

        for (int y = 0; y < sliceCount; y++)
        {
            float v = y / (float)rings;
            float py = Mathf.Lerp(-halfLen, halfLen, v);
            for (int i = 0; i < segments; i++)
            {
                float u = i / (float)segments;
                float ang = u * Mathf.PI * 2f;
                // 안쪽은 매끈 — clip 구멍 안으로 튀어나와 막히지 않게
                float ir = innerR;
                float orad = outerR * (0.985f + 0.015f * Mathf.PerlinNoise(u * 2.2f, v * 1.4f));
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);

                int iIdx = y * ringStride + i;
                int oIdx = y * ringStride + segments + i;

                verts[iIdx] = new Vector3(c * ir, py, s * ir);
                norms[iIdx] = new Vector3(-c, 0f, -s);
                uvs[iIdx] = new Vector2(u * 4f, v * 3f);

                verts[oIdx] = new Vector3(c * orad, py, s * orad);
                norms[oIdx] = new Vector3(c, 0f, s);
                uvs[oIdx] = new Vector2(u * 4f, v * 3f);
            }
        }

        for (int y = 0; y < rings; y++)
        {
            for (int i = 0; i < segments; i++)
            {
                int iNext = (i + 1) % segments;
                int i0 = y * ringStride + i;
                int i1 = y * ringStride + iNext;
                int i2 = (y + 1) * ringStride + i;
                int i3 = (y + 1) * ringStride + iNext;

                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);

                int o0 = y * ringStride + segments + i;
                int o1 = y * ringStride + segments + iNext;
                int o2 = (y + 1) * ringStride + segments + i;
                int o3 = (y + 1) * ringStride + segments + iNext;

                tris.Add(o0); tris.Add(o1); tris.Add(o2);
                tris.Add(o1); tris.Add(o3); tris.Add(o2);
            }
        }

        var mesh = new Mesh { name = "PierceMantleBore" };
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>관통구와 터널을 전부 지운다 (지구 상태 초기화).</summary>
    public void ClearAll()
    {
        for (int i = 0; i < holes.Count; i++)
        {
            if (holes[i].tunnel != null)
                Destroy(holes[i].tunnel);
        }
        holes.Clear();

        BindMaterial();
        PushToShader();
    }

    public void ExportSnapshots(System.Collections.Generic.List<HoleSnapshot> dst)
    {
        if (dst == null)
            return;
        dst.Clear();
        for (int i = 0; i < holes.Count; i++)
        {
            var h = holes[i];
            dst.Add(new HoleSnapshot
            {
                ax = h.localAxis.x,
                ay = h.localAxis.y,
                az = h.localAxis.z,
                radius = h.localRadius
            });
        }
    }

    public void RestoreSnapshots(System.Collections.Generic.IReadOnlyList<HoleSnapshot> src)
    {
        ClearAll();
        if (src == null || src.Count == 0)
            return;

        BindMaterial();
        float shell = LocalShellRadius();
        for (int i = 0; i < src.Count && holes.Count < MaxHoles; i++)
        {
            var s = src[i];
            var localAxis = new Vector3(s.ax, s.ay, s.az);
            if (localAxis.sqrMagnitude < 1e-6f)
                continue;
            localAxis.Normalize();

            holes.Add(new Hole
            {
                localAxis = localAxis,
                localRadius = Mathf.Max(0.001f, s.radius),
                tunnel = BuildMantleBore(localAxis, Mathf.Max(0.001f, s.radius), shell)
            });
        }
        PushToShader();
    }

    /// <summary>예전 버전이 남긴 노란 용암 메시/빔 잔재 정리. 기존 관통구는 건드리지 않는다.</summary>
    void CleanupLegacyJunk()
    {
        for (int i = earth.transform.childCount - 1; i >= 0; i--)
        {
            var ch = earth.transform.GetChild(i);
            string n = ch.name;
            if (n.StartsWith("PierceLava") || n.StartsWith("PierceRockTunnel")
                || n == "PierceMantle" || n == "PierceCore"
                || n == "LavaPit" || n == "LavaBit")
                Object.Destroy(ch.gameObject);
        }
    }

    void PushToShader()
    {
        var rend = earth != null ? earth.GetComponent<Renderer>() : null;
        if (rend != null)
            crustMat = rend.material;
        if (crustMat == null)
            return;

        for (int i = 0; i < MaxHoles; i++)
        {
            if (i < holes.Count)
            {
                Vector3 a = holes[i].localAxis;
                axisBuffer[i] = new Vector4(a.x, a.y, a.z, holes[i].localRadius);
            }
            else
            {
                axisBuffer[i] = Vector4.zero;
            }
        }

        crustMat.SetVectorArray("_PierceAxes", axisBuffer);
        crustMat.SetInt("_PierceCount", holes.Count);
        crustMat.SetFloat("_PierceEdge", LocalShellRadius() * 0.045f);
        crustMat.SetColor("_MoltenColor", new Color(1f, 0.28f, 0.04f, 1f));
    }

    /// <summary>스코치 텍스처 갱신 등으로 머티리얼이 바뀐 뒤 관통구 셰이더 값을 다시 밀어 넣는다.</summary>
    public void ReapplyShader()
    {
        BindMaterial();
        PushToShader();
    }

    void OnDestroy()
    {
        for (int i = 0; i < holes.Count; i++)
        {
            if (holes[i].tunnel != null)
                Destroy(holes[i].tunnel);
        }
        holes.Clear();
        if (crustMat != null)
            crustMat.SetInt("_PierceCount", 0);
    }
}
