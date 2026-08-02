using UnityEngine;

/// <summary>
/// 핵/충돌 자국을 지표면 텍스처에 직접 칠한다 (메시 데칼 없음).
/// </summary>
public class EarthSurfaceScorch : MonoBehaviour
{
    [SerializeField] Renderer crustRenderer;
    [SerializeField] int paintResolution = 1024;

    Texture2D working;
    Color32[] pixels;
    bool dirty;
    int dirtyFrames;

    public static EarthSurfaceScorch Ensure(EarthPlanet earth)
    {
        if (earth == null)
            return null;
        var scorch = earth.GetComponent<EarthSurfaceScorch>();
        if (scorch == null)
            scorch = earth.gameObject.AddComponent<EarthSurfaceScorch>();
        scorch.Bind(earth);
        return scorch;
    }

    public void Bind(EarthPlanet earth)
    {
        if (crustRenderer == null && earth != null)
            crustRenderer = earth.GetComponent<Renderer>();
        EnsureWorkingTexture();
    }

    void Awake()
    {
        if (crustRenderer == null)
            crustRenderer = GetComponent<Renderer>();
    }

    void LateUpdate()
    {
        if (!dirty || working == null)
            return;
        working.SetPixels32(pixels);
        working.Apply(false);
        dirty = false;
        dirtyFrames = 0;
    }

    void EnsureWorkingTexture()
    {
        if (working != null || crustRenderer == null)
            return;

        var mat = crustRenderer.material; // instance
        Texture src = mat.mainTexture;
        if (src == null)
            src = EarthTextureLoader.Day;

        int w = paintResolution;
        int h = paintResolution;
        if (src != null)
        {
            w = Mathf.Clamp(src.width, 256, 2048);
            h = Mathf.Clamp(src.height, 256, 2048);
            // keep aspect if not square
            if (src.width != src.height)
            {
                float aspect = src.width / (float)src.height;
                if (aspect >= 1f)
                {
                    w = Mathf.Clamp(src.width, 256, 2048);
                    h = Mathf.Max(128, Mathf.RoundToInt(w / aspect));
                }
                else
                {
                    h = Mathf.Clamp(src.height, 256, 2048);
                    w = Mathf.Max(128, Mathf.RoundToInt(h * aspect));
                }
            }
        }

        working = new Texture2D(w, h, TextureFormat.RGB24, false, false);
        working.name = "EarthCrustRuntime";
        working.wrapMode = TextureWrapMode.Repeat;
        working.filterMode = FilterMode.Bilinear;

        if (src is Texture2D src2d && src2d.isReadable)
        {
            // blit via GetPixels if same size, else scale sample
            if (src2d.width == w && src2d.height == h)
            {
                pixels = src2d.GetPixels32();
            }
            else
            {
                pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = src2d.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                    pixels[y * w + x] = c;
                }
            }
        }
        else
        {
            // GPU copy fallback when source isn't readable
            pixels = new Color32[w * h];
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src != null ? src : Texture2D.blackTexture, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            working.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            working.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            pixels = working.GetPixels32();
        }

        working.SetPixels32(pixels);
        working.Apply(false);
        mat.mainTexture = working;
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", working);
    }

    /// <summary>
    /// 월드 충격 지점을 어두운 원형으로 태운다. radiusNorm ≈ 지구 반지름 대비 비율.
    /// </summary>
    public void BurnAt(Vector3 worldPoint, float radiusNorm = 0.035f, float darkness = 0.72f)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null)
            return;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-6f)
            return;

        EarthGeo.DirectionToLatLon(local.normalized, out float lat, out float lon);
        EarthGeo.LatLonToUv(lat, lon, out float u, out float v);

        int w = working.width;
        int h = working.height;
        int cx = Mathf.Clamp(Mathf.RoundToInt(u * (w - 1)), 0, w - 1);
        int cy = Mathf.Clamp(Mathf.RoundToInt(v * (h - 1)), 0, h - 1);

        // equirectangular: radius in texels (lon wraps)
        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.55f, 4f, w * 0.08f);
        int r = Mathf.CeilToInt(radiusPx);
        float invR = 1f / Mathf.Max(0.001f, radiusPx);
        darkness = Mathf.Clamp01(darkness);

        Color32 ash = new Color32(28, 22, 18, 255);

        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h)
                continue;
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                // wrap longitude
                while (x < 0) x += w;
                while (x >= w) x -= w;

                float dist = Mathf.Sqrt(dx * dx + dy * dy) * invR;
                if (dist > 1f)
                    continue;

                // soft falloff — center darker
                float fall = 1f - dist;
                fall *= fall;
                float amount = darkness * fall;
                if (amount < 0.02f)
                    continue;

                int idx = y * w + x;
                Color32 p = pixels[idx];
                p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, ash.r, amount));
                p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, ash.g, amount));
                p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, ash.b, amount));
                pixels[idx] = p;
            }
        }

        dirty = true;
    }
}
