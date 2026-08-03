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

        float scorchR = localRadius / shell * 1.1f;
        var scorch = EarthSurfaceScorch.Ensure(earth);
        scorch?.BurnAt(entryWorld, scorchR, 0.92f);
        scorch?.BurnAt(exitWorld, scorchR, 0.92f);
    }

    /// <summary>
    /// 구멍을 감싸는 암반 터널. 안쪽은 뚫려 반대편이 보이고, 벽 두께로 지각이 두꺼워 보인다.
    /// </summary>
    GameObject BuildMantleBore(Vector3 localAxis, float innerRadius, float shell)
    {
        float innerR = innerRadius * 0.99f;
        // 구멍 둘레만 감싸는 벽 — 행성을 덮어버리지 않도록 얇게
        float outerR = Mathf.Min(innerR * 1.32f, shell * 0.55f);
        // 터널 끝이 표면 밖으로 튀어나오지 않게
        float halfLen = Mathf.Sqrt(Mathf.Max(1e-4f, shell * shell - outerR * outerR)) * 0.995f;

        var root = new GameObject("PierceMantleBore");
        root.transform.SetParent(earth.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;
        root.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localAxis);

        var mf = root.AddComponent<MeshFilter>();
        var mr = root.AddComponent<MeshRenderer>();
        mf.sharedMesh = BuildThickMantleTube(innerR, outerR, halfLen, 40, 18);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var shader = Shader.Find("EarthSmasher/MantlePierce");
        Material mat;
        if (shader != null)
        {
            mat = new Material(shader);
            mat.SetColor("_Color", new Color(0.13f, 0.08f, 0.06f, 1f));
            mat.SetColor("_MoltenColor", new Color(1f, 0.3f, 0.05f, 1f));
            mat.SetFloat("_Emission", 0.5f);
        }
        else
        {
            mat = RuntimeMaterial.Opaque(new Color(0.17f, 0.09f, 0.06f), 0.35f);
        }
        mr.material = mat;
        return root;
    }

    /// <summary>속이 빈 두꺼운 원통 (inner→outer). 안쪽 벽 + 바깥 벽 + 양쪽 링 캡.</summary>
    static Mesh BuildThickMantleTube(float innerR, float outerR, float halfLen, int segments, int rings)
    {
        int sliceCount = rings + 1;
        int ringStride = segments * 2;
        int wallVertCount = sliceCount * ringStride;
        int capVertCount = segments * 4;
        var verts = new Vector3[wallVertCount + capVertCount];
        var norms = new Vector3[verts.Length];
        var uvs = new Vector2[verts.Length];
        var tris = new List<int>(rings * segments * 12 + segments * 12);

        for (int y = 0; y < sliceCount; y++)
        {
            float v = y / (float)rings;
            float py = Mathf.Lerp(-halfLen, halfLen, v);
            float belly = 1f - 0.06f * Mathf.Sin(v * Mathf.PI);
            for (int i = 0; i < segments; i++)
            {
                float u = i / (float)segments;
                float ang = u * Mathf.PI * 2f;
                float jag = 1f + 0.06f * (Mathf.PerlinNoise(u * 5.5f + 2.1f, v * 3.7f) * 2f - 1f);
                float ir = innerR * belly * jag;
                float orad = outerR * (0.97f + 0.03f * Mathf.PerlinNoise(u * 2.2f, v * 1.4f));
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

        int capBase = wallVertCount;
        for (int end = 0; end < 2; end++)
        {
            float py = end == 0 ? -halfLen : halfLen;
            float ny = end == 0 ? -1f : 1f;
            int baseIdx = capBase + end * segments * 2;
            int wallY = end == 0 ? 0 : rings;

            for (int i = 0; i < segments; i++)
            {
                float u = i / (float)segments;
                Vector3 inner = verts[wallY * ringStride + i];
                Vector3 outer = verts[wallY * ringStride + segments + i];
                inner.y = py;
                outer.y = py;

                int ii = baseIdx + i;
                int oi = baseIdx + segments + i;
                verts[ii] = inner;
                verts[oi] = outer;
                norms[ii] = new Vector3(0f, ny, 0f);
                norms[oi] = new Vector3(0f, ny, 0f);
                uvs[ii] = new Vector2(u * 4f, 0f);
                uvs[oi] = new Vector2(u * 4f, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int iNext = (i + 1) % segments;
                int ii = baseIdx + i;
                int inx = baseIdx + iNext;
                int oi = baseIdx + segments + i;
                int onx = baseIdx + segments + iNext;
                if (end == 0)
                {
                    tris.Add(ii); tris.Add(oi); tris.Add(inx);
                    tris.Add(inx); tris.Add(oi); tris.Add(onx);
                }
                else
                {
                    tris.Add(ii); tris.Add(inx); tris.Add(oi);
                    tris.Add(inx); tris.Add(onx); tris.Add(oi);
                }
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
        if (crustMat == null)
            BindMaterial();
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
        crustMat.SetFloat("_PierceEdge", LocalShellRadius() * 0.07f);
        crustMat.SetColor("_MoltenColor", new Color(1f, 0.28f, 0.04f, 1f));
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
