using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// NASA LROC 텍스처 기반 달 메시/머티리얼. 회색 프리미티브 대체용.
/// </summary>
public static class MoonVisual
{
    const string ColorPath = "Moon/moon_color_2k";
    const string HeightPath = "Moon/moon_height_1k";

    static Texture2D colorTex;
    static Texture2D normalTex;
    static Mesh sphereMesh;
    static bool triedLoad;

    public static GameObject Create(float earthRadius, float sizeMul = 0.42f)
    {
        EnsureLoaded();

        var go = new GameObject("EventMoon");
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = sphereMesh != null ? sphereMesh : GetFallbackSphereMesh();

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateMaterial();
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;

        go.transform.localScale = Vector3.one * (earthRadius * sizeMul);
        return go;
    }

    static void EnsureLoaded()
    {
        if (triedLoad)
            return;
        triedLoad = true;

        colorTex = Resources.Load<Texture2D>(ColorPath);
        var height = Resources.Load<Texture2D>(HeightPath);
        if (height != null)
            normalTex = BuildNormalMap(height, 3.2f);

        sphereMesh = BuildUvSphere(72, 48);

        if (colorTex == null)
            Debug.LogWarning("[MoonVisual] Missing Resources/Moon/moon_color_2k — using procedural fallback.");
    }

    static Material CreateMaterial()
    {
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");

        var mat = new Material(shader);

        if (colorTex != null)
        {
            mat.mainTexture = colorTex;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", colorTex);
            ApplyColor(mat, Color.white);
        }
        else
        {
            // 폴백: 절차적 크레이터 텍스처
            var proc = BuildProceduralMoon(512, 256);
            mat.mainTexture = proc;
            ApplyColor(mat, Color.white);
        }

        if (normalTex != null)
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.EnableKeyword("_NORMALMAP");
            if (mat.HasProperty("_BumpScale"))
                mat.SetFloat("_BumpScale", 1.15f);
            if (mat.HasProperty("_BumpMap"))
                mat.SetTexture("_BumpMap", normalTex);
        }

        // 달 먼지: 거의 무광택
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.08f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.08f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.02f);

        return mat;
    }

    static void ApplyColor(Material mat, Color c)
    {
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", c);
        mat.color = c;
    }

    static Mesh GetFallbackSphereMesh()
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tmp);
        return mesh;
    }

    static Mesh BuildUvSphere(int lonSeg, int latSeg)
    {
        lonSeg = Mathf.Clamp(lonSeg, 16, 128);
        latSeg = Mathf.Clamp(latSeg, 8, 64);

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
                verts[i] = p * 0.5f; // radius 0.5 → scale = diameter
                norms[i] = p;
                uvs[i] = new Vector2(u, v);
                i++;
            }
        }

        var tris = new int[lonSeg * latSeg * 6];
        int t = 0;
        for (int y = 0; y < latSeg; y++)
        {
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
        }

        var mesh = new Mesh { name = "MoonHiSphere" };
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    static Texture2D BuildNormalMap(Texture2D height, float strength)
    {
        // 읽기 가능한 카피로 샘플
        var src = MakeReadableCopy(height);
        int w = src.width;
        int h = src.height;
        var pixels = src.GetPixels32();
        var nrm = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float c = pixels[y * w + x].r / 255f;
                float cx = pixels[y * w + ((x + 1) % w)].r / 255f;
                float cy = pixels[((y + 1) % h) * w + x].r / 255f;
                float dx = (c - cx) * strength;
                float dy = (c - cy) * strength;
                Vector3 n = new Vector3(dx, dy, 1f).normalized;
                nrm[y * w + x] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255),
                    255);
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
        tex.name = "MoonNormalRuntime";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(nrm);
        tex.Apply(true, true);
        return tex;
    }

    static Texture2D MakeReadableCopy(Texture2D src)
    {
        if (src == null)
            return null;
        try
        {
            if (src.isReadable)
                return src;
        }
        catch
        {
            // isReadable can throw on some builds
        }

        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
        copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        copy.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    static Texture2D BuildProceduralMoon(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGB24, true, false);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)w;
            float v = y / (float)h;
            float n = Mathf.PerlinNoise(u * 6f, v * 3f) * 0.55f
                    + Mathf.PerlinNoise(u * 18f, v * 9f) * 0.3f
                    + Mathf.PerlinNoise(u * 48f, v * 24f) * 0.15f;
            // maria
            float maria = Mathf.PerlinNoise(u * 2.2f + 3f, v * 1.1f) > 0.58f ? 0.78f : 1f;
            float g = Mathf.Lerp(0.35f, 0.78f, n) * maria;
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255);
            px[y * w + x] = new Color32(b, b, (byte)(b * 0.96f), 255);
        }

        // stamp a few dark craters
        for (int i = 0; i < 40; i++)
        {
            int cx = Random.Range(0, w);
            int cy = Random.Range(0, h);
            int r = Random.Range(4, 28);
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                if (d > 1f) continue;
                int x = (cx + dx + w) % w;
                int y = cy + dy;
                if (y < 0 || y >= h) continue;
                float rim = Mathf.Exp(-Mathf.Pow((d - 0.82f) * 8f, 2f));
                float bowl = Mathf.SmoothStep(1f, 0.55f, d);
                float shade = Mathf.Lerp(bowl, 1.15f, rim * 0.5f);
                int idx = y * w + x;
                var c = px[idx];
                px[idx] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * shade), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * shade), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * shade), 0, 255),
                    255);
            }
        }

        tex.SetPixels32(px);
        tex.Apply(true, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
