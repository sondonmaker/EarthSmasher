using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 고밀도 지구 메시 생성기.
///
/// Unity 기본 Sphere는 24세그먼트(=15°/세그먼트)라 크레이터 하나가 폴리곤보다 작아
/// 아무리 파도 "바람 빠진 공"처럼 눌리기만 한다. 그래서 같은 UV 매핑을 유지한 채
/// 훨씬 촘촘한 구를 만든다.
///
/// 텍스처가 틀어지지 않도록, 기준 메시(Unity Sphere)의 UV 배치를 먼저 역산해서
/// 동일한 매핑으로 생성한다. (경도 오프셋 / 좌우·상하 반전 자동 감지)
/// </summary>
public static class EarthMeshBuilder
{
    public struct UvMapping
    {
        public float lonOffset;   // 0..1
        public bool lonMirrored;
        public bool latFlipped;

        public static UvMapping Default => new UvMapping
        {
            lonOffset = 0f,
            lonMirrored = false,
            latFlipped = false
        };
    }

    /// <summary>기준 메시의 UV 규칙을 역산한다.</summary>
    public static UvMapping Calibrate(Mesh reference)
    {
        var map = UvMapping.Default;
        if (reference == null)
            return map;

        Vector3[] verts = reference.vertices;
        Vector2[] uvs = reference.uv;
        if (verts == null || uvs == null || verts.Length == 0 || uvs.Length != verts.Length)
            return map;

        // 경도 오프셋은 원형 평균으로 (0/1 경계 seam 때문에 산술 평균은 부정확)
        float sinDirect = 0f, cosDirect = 0f;
        float sinMirror = 0f, cosMirror = 0f;
        float latDirectErr = 0f, latFlipErr = 0f;
        int samples = 0;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 p = verts[i].normalized;
            if (Mathf.Abs(p.y) > 0.94f)
                continue; // 극점은 u가 의미 없음

            float lat = Mathf.Asin(Mathf.Clamp(p.y, -1f, 1f)) * Mathf.Rad2Deg;
            float lon = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
            float eu = Mathf.Repeat((lon + 180f) / 360f, 1f);
            float ev = (lat + 90f) / 180f;

            float kDirect = Mathf.Repeat(uvs[i].x - eu, 1f) * Mathf.PI * 2f;
            sinDirect += Mathf.Sin(kDirect);
            cosDirect += Mathf.Cos(kDirect);

            float kMirror = Mathf.Repeat(uvs[i].x + eu, 1f) * Mathf.PI * 2f;
            sinMirror += Mathf.Sin(kMirror);
            cosMirror += Mathf.Cos(kMirror);

            latDirectErr += Mathf.Abs(uvs[i].y - ev);
            latFlipErr += Mathf.Abs(uvs[i].y - (1f - ev));
            samples++;
        }

        if (samples == 0)
            return map;

        float offDirect = Mathf.Repeat(Mathf.Atan2(sinDirect, cosDirect) / (Mathf.PI * 2f), 1f);
        float offMirror = Mathf.Repeat(Mathf.Atan2(sinMirror, cosMirror) / (Mathf.PI * 2f), 1f);

        // 어느 가설이 더 잘 맞는지 잔차로 판정
        float errDirect = 0f, errMirror = 0f;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 p = verts[i].normalized;
            if (Mathf.Abs(p.y) > 0.94f)
                continue;
            float lon = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
            float eu = Mathf.Repeat((lon + 180f) / 360f, 1f);
            errDirect += CircularDistance(uvs[i].x, Mathf.Repeat(eu + offDirect, 1f));
            errMirror += CircularDistance(uvs[i].x, Mathf.Repeat(-eu + offMirror, 1f));
        }

        map.lonMirrored = errMirror < errDirect;
        map.lonOffset = SnapOffset(map.lonMirrored ? offMirror : offDirect);
        map.latFlipped = latFlipErr < latDirectErr;
        return map;
    }

    /// <summary>Unity 기본 구의 오프셋은 0 / 0.25 / 0.5 같은 값이라 반올림해 오차를 없앤다.</summary>
    static float SnapOffset(float offset)
    {
        float snapped = Mathf.Round(offset * 4f) / 4f;
        return Mathf.Abs(Mathf.DeltaAngle(offset * 360f, snapped * 360f)) < 6f
            ? Mathf.Repeat(snapped, 1f)
            : offset;
    }

    static float CircularDistance(float a, float b)
    {
        float d = Mathf.Abs(Mathf.Repeat(a - b, 1f));
        return Mathf.Min(d, 1f - d);
    }

    /// <summary>
    /// 텍스처 공간(u,v) 격자로 구를 만든다. seam이 u=0/1에 정확히 놓여 이음매가 깔끔하다.
    /// </summary>
    public static Mesh Build(UvMapping map, float radius, int lonSegments, int latSegments)
    {
        lonSegments = Mathf.Clamp(lonSegments, 16, 1024);
        latSegments = Mathf.Clamp(latSegments, 8, 512);

        int cols = lonSegments + 1;
        int rows = latSegments + 1;
        var verts = new Vector3[cols * rows];
        var norms = new Vector3[verts.Length];
        var uvs = new Vector2[verts.Length];

        for (int y = 0; y < rows; y++)
        {
            float v = y / (float)latSegments;
            float vBase = map.latFlipped ? 1f - v : v;
            float lat = Mathf.Lerp(-90f, 90f, vBase);

            for (int x = 0; x < cols; x++)
            {
                float u = x / (float)lonSegments;
                float eu = map.lonMirrored
                    ? Mathf.Repeat(map.lonOffset - u, 1f)
                    : Mathf.Repeat(u - map.lonOffset, 1f);
                float lon = eu * 360f - 180f;

                Vector3 dir = EarthGeo.LatLonToDirection(lat, lon);
                int idx = y * cols + x;
                verts[idx] = dir * radius;
                norms[idx] = dir;
                uvs[idx] = new Vector2(u, v);
            }
        }

        var tris = new int[lonSegments * latSegments * 6];
        int t = 0;
        for (int y = 0; y < latSegments; y++)
        {
            for (int x = 0; x < lonSegments; x++)
            {
                int a = y * cols + x;
                int b = a + 1;
                int c = a + cols;
                int d = c + 1;

                tris[t++] = a;
                tris[t++] = c;
                tris[t++] = b;
                tris[t++] = b;
                tris[t++] = c;
                tris[t++] = d;
            }
        }

        var mesh = new Mesh { name = "EarthCrustHiRes" };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.MarkDynamic();
        return mesh;
    }

    /// <summary>기존 Sphere의 UV 규칙을 그대로 따르는 고밀도 메시로 교체.</summary>
    public static Mesh Upgrade(MeshFilter filter, int lonSegments = 0, int latSegments = 0)
    {
        if (filter == null || filter.sharedMesh == null)
            return null;

        if (lonSegments <= 0 || latSegments <= 0)
        {
            // 폰은 타격마다 법선을 다시 계산하는 비용이 커서 밀도를 낮춘다.
            bool mobile = Application.isMobilePlatform;
            lonSegments = mobile ? 224 : 384;
            latSegments = mobile ? 112 : 192;
        }

        Mesh source = filter.sharedMesh;
        // Unity Sphere는 반지름 0.5
        float radius = source.bounds.extents.x;
        if (radius <= 1e-4f)
            radius = 0.5f;

        UvMapping map = Calibrate(source);
        Apply(map);
        Mesh dense = Build(map, radius, lonSegments, latSegments);
        filter.mesh = dense;
        return dense;
    }

    /// <summary>텍스처를 칠하는 쪽(그을음/용암)이 같은 규칙을 쓰도록 공유한다.</summary>
    public static void Apply(UvMapping map)
    {
        EarthGeo.UvLonOffset = map.lonOffset;
        EarthGeo.UvLonMirrored = map.lonMirrored;
        EarthGeo.UvLatFlipped = map.latFlipped;
    }
}
