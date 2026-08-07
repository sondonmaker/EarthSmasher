using UnityEngine;

/// <summary>
/// 핵/충돌 자국을 지표면 텍스처에 직접 칠한다 (메시 데칼 없음).
/// </summary>
public class EarthSurfaceScorch : MonoBehaviour
{
    [SerializeField] Renderer crustRenderer;
    [SerializeField] int paintResolution = 1024;

    Texture2D working;
    Texture sourceTex;
    Color32[] pixels;
    Color32[] basePixels;
    bool dirty;
    int dirtyFrames;
    float nextTextureApply;
    const float TextureApplyInterval = 0.12f;

    static Texture2D lavaColor;
    static Texture2D lavaEmit;
    static Texture2D rockColor;
    static Color32[] lavaColorPx;
    static Color32[] lavaEmitPx;
    static Color32[] rockColorPx;
    static int lavaW, lavaH, rockW, rockH;

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
        if (Time.unscaledTime < nextTextureApply)
            return;
        FlushTexture();
    }

    public void FlushTexture()
    {
        if (!dirty || working == null)
            return;
        working.SetPixels32(pixels);
        working.Apply(false);
        dirty = false;
        dirtyFrames = 0;
        nextTextureApply = Time.unscaledTime + TextureApplyInterval;
    }

    void EnsureWorkingTexture()
    {
        if (working != null || crustRenderer == null)
            return;

        var mat = crustRenderer.material; // instance
        Texture src = mat.mainTexture;
        if (src == null)
            src = EarthTextureLoader.Day;
        // 초기화 때 다시 읽어야 하므로 원본 참조를 남긴다 (픽셀 사본은 메모리가 커서 보관하지 않음)
        sourceTex = src;

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

        LoadBasePixels();

        working.SetPixels32(pixels);
        working.Apply(false);
        mat.mainTexture = working;
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", working);
    }

    /// <summary>원본 지표 텍스처를 pixels 버퍼에 다시 채운다.</summary>
    void LoadBasePixels()
    {
        if (working == null)
            return;

        int w = working.width;
        int h = working.height;

        if (sourceTex is Texture2D src2d && src2d.isReadable)
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
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sourceTex != null ? sourceTex : Texture2D.blackTexture, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            working.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            working.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            pixels = working.GetPixels32();
        }

        if (pixels != null && pixels.Length > 0)
        {
            basePixels = new Color32[pixels.Length];
            System.Array.Copy(pixels, basePixels, pixels.Length);
        }
    }

    /// <summary>0~1 — 그을음·용암·크레이터 텍스처 피해 면적.</summary>
    public float SampleSurfaceDamage01()
    {
        EnsureWorkingTexture();
        if (pixels == null || basePixels == null || pixels.Length != basePixels.Length)
            return 0f;

        int step = Mathf.Max(1, pixels.Length / 14000);
        int damaged = 0;
        int total = 0;
        for (int i = 0; i < pixels.Length; i += step)
        {
            total++;
            if (IsDamagedPixel(basePixels[i], pixels[i]))
                damaged++;
        }

        float frac = total > 0 ? damaged / (float)total : 0f;
        return Mathf.Clamp01(Mathf.Sqrt(frac) * 1.4f);
    }

    static bool IsDamagedPixel(Color32 before, Color32 after)
    {
        float bl = before.r * 0.34f + before.g * 0.44f + before.b * 0.22f;
        float al = after.r * 0.34f + after.g * 0.44f + after.b * 0.22f;
        if (bl - al > 16f)
            return true;
        if (after.r > before.r + 22f && after.g < before.g * 0.82f)
            return true;
        return false;
    }

    /// <summary>태운 자국·균열·크레이터 자국을 지우고 원래 지표로 되돌린다.</summary>
    public void RestoreSurface()
    {
        EnsureWorkingTexture();
        if (working == null)
            return;

        LoadBasePixels();
        working.SetPixels32(pixels);
        working.Apply(false);
        dirty = false;
        dirtyFrames = 0;
    }

    public bool TryExportPng(out byte[] png)
    {
        EnsureWorkingTexture();
        if (working == null)
        {
            png = null;
            return false;
        }

        if (dirty)
        {
            working.SetPixels32(pixels);
            working.Apply(false);
            dirty = false;
        }

        png = working.EncodeToPNG();
        return png != null && png.Length > 0;
    }

    public bool TryImportPng(byte[] png)
    {
        EnsureWorkingTexture();
        if (working == null || png == null || png.Length == 0)
            return false;

        var temp = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!temp.LoadImage(png))
        {
            Object.Destroy(temp);
            return false;
        }

        if (temp.width != working.width || temp.height != working.height)
        {
            Object.Destroy(temp);
            return false;
        }

        pixels = temp.GetPixels32();
        Object.Destroy(temp);
        working.SetPixels32(pixels);
        working.Apply(false);
        dirty = false;
        return true;
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

    /// <summary>홀드 빔 전용 — 용암 텍스처 샘플 없이 가볍게 태운다.</summary>
    public void PaintSustainBurnAt(Vector3 worldPoint, float radiusNorm = 0.018f, float heat = 0.88f)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null)
            return;
        if (!TryImpactUv(worldPoint, out int cx, out int cy, out _, out _))
            return;

        StampSustainBurnDisc(cx, cy, radiusNorm, heat);
        dirty = true;
    }

    /// <summary>홀드 빔 궤적 — step 수 제한 + 경량 스탬프.</summary>
    public void PaintSustainBurnSegment(Vector3 fromWorld, Vector3 toWorld, float radiusNorm, float heat)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null)
            return;

        Vector3 center = transform.position;
        Vector3 a = (fromWorld - center).normalized;
        Vector3 b = (toWorld - center).normalized;
        if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f)
        {
            PaintSustainBurnAt(toWorld, radiusNorm, heat);
            return;
        }

        float angleDeg = Vector3.Angle(a, b);
        int steps = Mathf.Clamp(Mathf.CeilToInt(angleDeg / 3.2f), 1, 10);
        float shell = LocalShellRadius();
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 dir = Vector3.Slerp(a, b, t).normalized;
            Vector3 p = transform.TransformPoint(dir * shell);
            if (!TryImpactUv(p, out int cx, out int cy, out _, out _))
                continue;
            StampSustainBurnDisc(cx, cy, radiusNorm, heat);
        }

        dirty = true;
    }

    void StampSustainBurnDisc(int cx, int cy, float radiusNorm, float heat)
    {
        int w = working.width;
        int h = working.height;
        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.55f, 3f, w * 0.045f);
        int r = Mathf.CeilToInt(radiusPx);
        float invR2 = 1f / Mathf.Max(0.001f, radiusPx * radiusPx);
        heat = Mathf.Clamp01(heat);

        Color32 charred = new Color32(22, 14, 10, 255);
        Color32 ember = new Color32(190, 48, 12, 255);
        Color32 core = new Color32(240, 130, 28, 255);

        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h)
                continue;
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                while (x < 0) x += w;
                while (x >= w) x -= w;

                float dist2 = (dx * dx + dy * dy) * invR2;
                if (dist2 > 1f)
                    continue;

                float dist = Mathf.Sqrt(dist2);
                float fall = 1f - dist;
                fall *= fall;
                int idx = y * w + x;
                Color32 p = pixels[idx];

                if (dist < 0.42f)
                    p = BlendPx(p, core, heat * fall * 0.75f);
                else if (dist < 0.78f)
                    p = BlendPx(p, ember, heat * fall * 0.55f);
                else
                    p = BlendPx(p, charred, heat * fall * 0.45f);

                pixels[idx] = p;
            }
        }
    }

    /// <summary>홀드 빔 — 중심은 용암, 가장자리는 그을린 불타는 자국 (텍스처에 영구 남음).</summary>
    public void PaintBeamBurnAt(Vector3 worldPoint, float radiusNorm = 0.022f, float heat = 0.85f)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null)
            return;
        if (!TryImpactUv(worldPoint, out int cx, out int cy, out _, out _))
            return;

        EnsureImpactTextures();
        StampBeamBurnDisc(cx, cy, radiusNorm, heat);
        dirty = true;
    }

    /// <summary>빔을 움직일 때 표면을 따라 불타는 선을 이어 그린다.</summary>
    public void PaintBeamBurnSegment(Vector3 fromWorld, Vector3 toWorld, float radiusNorm, float heat)
    {
        Vector3 center = transform.position;
        Vector3 a = (fromWorld - center).normalized;
        Vector3 b = (toWorld - center).normalized;
        if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f)
        {
            PaintBeamBurnAt(toWorld, radiusNorm, heat);
            return;
        }

        float angleDeg = Vector3.Angle(a, b);
        int steps = Mathf.Clamp(Mathf.CeilToInt(angleDeg / 1.45f), 1, 32);
        float shell = LocalShellRadius();
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 dir = Vector3.Slerp(a, b, t).normalized;
            Vector3 p = transform.TransformPoint(dir * shell);
            PaintBeamBurnAt(p, radiusNorm, heat * Mathf.Lerp(0.82f, 1f, 1f - t * 0.15f));
        }
    }

    float LocalShellRadius()
    {
        var col = GetComponent<SphereCollider>();
        return col != null ? col.radius : 0.5f;
    }

    void StampBeamBurnDisc(int cx, int cy, float radiusNorm, float heat)
    {
        int w = working.width;
        int h = working.height;
        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.55f, 3f, w * 0.06f);
        int r = Mathf.CeilToInt(radiusPx);
        float invR = 1f / Mathf.Max(0.001f, radiusPx);
        heat = Mathf.Clamp01(heat);

        Color32 charred = new Color32(18, 12, 10, 255);
        Color32 ember = new Color32(210, 52, 10, 255);
        Color32 core = new Color32(255, 168, 36, 255);

        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h)
                continue;
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                while (x < 0) x += w;
                while (x >= w) x -= w;

                float dist = Mathf.Sqrt(dx * dx + dy * dy) * invR;
                if (dist > 1f)
                    continue;

                float fall = 1f - dist;
                fall *= fall;
                int idx = y * w + x;
                Color32 p = pixels[idx];

                if (dist < 0.38f)
                {
                    float coreAmt = heat * Mathf.Lerp(0.55f, 0.92f, fall);
                    if (lavaColorPx != null)
                    {
                        Color32 lava = Sample(lavaColorPx, lavaW, lavaH, x * 0.014f, y * 0.014f);
                        Color32 emit = lavaEmitPx != null
                            ? Sample(lavaEmitPx, lavaW, lavaH, x * 0.014f, y * 0.014f)
                            : lava;
                        var hot = new Color32(
                            (byte)Mathf.Min(255, (lava.r * 2 + emit.r) / 2),
                            (byte)Mathf.Min(255, (lava.g + emit.g / 2) / 2),
                            (byte)Mathf.Min(255, lava.b / 3 + 12),
                            255);
                        p = BlendPx(p, hot, coreAmt);
                    }
                    else
                    {
                        p = BlendPx(p, core, coreAmt);
                    }
                }

                if (dist >= 0.22f && dist < 0.82f)
                {
                    float ring = heat * Mathf.Lerp(0.35f, 0.78f, 1f - Mathf.Abs(dist - 0.48f) * 2.2f);
                    p = BlendPx(p, ember, ring);
                }

                if (dist >= 0.55f)
                {
                    float ashAmt = heat * fall * 0.62f;
                    p = BlendPx(p, charred, ashAmt);
                }

                pixels[idx] = p;
            }
        }
    }

    static Color32 BlendPx(Color32 basePx, Color32 tint, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(basePx.r, tint.r, amount)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(basePx.g, tint.g, amount)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(basePx.b, tint.b, amount)),
            255);
    }

    /// <summary>관통구 주변 스코치·용암을 지워 구멍이 막혀 보이지 않게 한다.</summary>
    public void CarveOpening(Vector3 worldPoint, float radiusNorm = 0.035f)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null || basePixels == null || basePixels.Length != pixels.Length)
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

        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.55f, 4f, w * 0.1f);
        int r = Mathf.CeilToInt(radiusPx);
        float invR = 1f / Mathf.Max(0.001f, radiusPx);

        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h)
                continue;
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                while (x < 0) x += w;
                while (x >= w) x -= w;

                float dist = Mathf.Sqrt(dx * dx + dy * dy) * invR;
                if (dist > 1f)
                    continue;

                int idx = y * w + x;
                pixels[idx] = basePixels[idx];
            }
        }

        dirty = true;
    }

    static void EnsureImpactTextures()
    {
        if (lavaColorPx != null)
            return;

        lavaColor = Resources.Load<Texture2D>("Impact/lava_color");
        lavaEmit = Resources.Load<Texture2D>("Impact/lava_emission");
        rockColor = Resources.Load<Texture2D>("Impact/rock_color");

        if (lavaColor != null)
        {
            var readable = MakeReadable(lavaColor);
            lavaColorPx = readable.GetPixels32();
            lavaW = readable.width;
            lavaH = readable.height;
        }
        if (lavaEmit != null)
        {
            var readable = MakeReadable(lavaEmit);
            lavaEmitPx = readable.GetPixels32();
        }
        if (rockColor != null)
        {
            var readable = MakeReadable(rockColor);
            rockColorPx = readable.GetPixels32();
            rockW = readable.width;
            rockH = readable.height;
        }
    }

    static Texture2D MakeReadable(Texture2D src)
    {
        try
        {
            if (src.isReadable)
                return src;
        }
        catch { /* ignore */ }

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

    static Color32 Sample(Color32[] px, int tw, int th, float u, float v)
    {
        if (px == null || tw < 1 || th < 1)
            return new Color32(40, 30, 25, 255);
        int x = ((int)(u * tw) % tw + tw) % tw;
        int y = ((int)(v * th) % th + th) % th;
        return px[y * tw + x];
    }

    /// <summary>
    /// AmbientCG 용암/암석 — 타원+노이즈로 불규칙한 크레이터 자국.
    /// </summary>
    public void PaintImpactCrater(Vector3 worldPoint, float radiusNorm = 0.12f, int seed = 0)
    {
        EnsureWorkingTexture();
        EnsureImpactTextures();
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

        if (seed == 0)
            seed = (cx * 73856093) ^ (cy * 19349663) ^ radiusNorm.GetHashCode();
        var rng = new System.Random(seed);

        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.72f, 22f, w * 0.22f);
        float stretchX = Mathf.Lerp(0.7f, 1.35f, (float)rng.NextDouble());
        float stretchY = Mathf.Lerp(0.72f, 1.3f, (float)rng.NextDouble());
        float rot = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float cosR = Mathf.Cos(rot);
        float sinR = Mathf.Sin(rot);
        float n1 = Mathf.Lerp(0.12f, 0.3f, (float)rng.NextDouble());
        float n2 = Mathf.Lerp(0.05f, 0.16f, (float)rng.NextDouble());
        float p1 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p2 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        int h1 = 2 + rng.Next(0, 3);
        int h2 = 5 + rng.Next(0, 5);
        float uvScale = Mathf.Lerp(0.4f, 0.85f, (float)rng.NextDouble());
        float uvRot = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float cosU = Mathf.Cos(uvRot);
        float sinU = Mathf.Sin(uvRot);

        int r = Mathf.CeilToInt(radiusPx * 1.55f * Mathf.Max(stretchX, stretchY));
        float aspect = h / (float)Mathf.Max(1, w);

        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h)
                continue;
            for (int dx = -r; dx <= r; dx++)
            {
                // 회전+타원 거리
                float rx = (dx * cosR + dy * sinR) / stretchX;
                float ry = (-dx * sinR + dy * cosR) / (stretchY * Mathf.Max(0.35f, aspect * 2f));
                float ang = Mathf.Atan2(ry, rx);
                float edgeMul = 1f
                    + n1 * Mathf.Sin(h1 * ang + p1)
                    + n2 * Mathf.Sin(h2 * ang + p2);
                float dist = Mathf.Sqrt(rx * rx + ry * ry) / Mathf.Max(0.001f, radiusPx * edgeMul);
                if (dist > 1.35f)
                    continue;

                int x = cx + dx;
                while (x < 0) x += w;
                while (x >= w) x -= w;

                float mask;
                if (dist < 0.45f)
                    mask = 0.95f;
                else if (dist < 0.78f)
                    mask = Mathf.Lerp(0.95f, 0.5f, (dist - 0.45f) / 0.33f);
                else
                    mask = Mathf.SmoothStep(1f, 0f, (dist - 0.78f) / 0.57f) * 0.5f;

                // 가장자리 깨짐 — 들쭉날쭉 알파
                float ragged = 0.75f + 0.25f * Mathf.Sin(ang * 11f + p2 + dist * 6f);
                mask *= Mathf.Lerp(0.55f, 1f, ragged);
                if (mask < 0.02f)
                    continue;

                float lx = (dx * cosU - dy * sinU) * uvScale / radiusPx;
                float ly = (dx * sinU + dy * cosU) * uvScale / radiusPx;
                float su = Mathf.Abs(lx) + 0.5f;
                float sv = Mathf.Abs(ly) + 0.5f;

                Color32 rock = Sample(rockColorPx, rockW, rockH, su, sv);
                Color32 lava = Sample(lavaColorPx, lavaW, lavaH, su * 1.3f, sv * 1.3f);
                Color32 emit = Sample(lavaEmitPx, lavaW, lavaH, su * 1.3f, sv * 1.3f);

                var hot = new Color32(
                    (byte)Mathf.Min(255, (lava.r * 2 + emit.r) / 2),
                    (byte)Mathf.Min(255, (lava.g + emit.g / 2) / 2),
                    (byte)Mathf.Min(255, lava.b / 3 + 8),
                    255);

                Color32 target;
                if (dist < 0.7f)
                {
                    float t = dist / 0.7f;
                    var core = new Color32(
                        (byte)Mathf.Min(255, hot.r + 40),
                        (byte)Mathf.Min(255, hot.g + 10),
                        hot.b,
                        255);
                    target = Color32.Lerp(core, hot, t);
                }
                else
                {
                    target = Color32.Lerp(hot, rock, (dist - 0.7f) / 0.65f);
                }

                int idx = y * w + x;
                Color32 p = pixels[idx];
                p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, target.r, mask));
                p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, target.g, mask));
                p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, target.b, mask));
                pixels[idx] = p;
            }
        }

        dirty = true;
    }

    /// <summary>호환용 — 낙서 크랙 대신 텍스처 크레이터만 강화.</summary>
    public void PaintLavaCracks(Vector3 worldPoint, float radiusNorm = 0.12f, int branches = 16)
    {
        PaintImpactCrater(worldPoint, radiusNorm * 0.85f);
    }

    /// <summary>
    /// 지진 균열: 진앙에서 방사형 균열선을 어둡게 칠한다.
    /// </summary>
    public void CrackAt(Vector3 worldPoint, float radiusNorm = 0.05f, int branches = 7)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null)
            return;

        if (!TryImpactUv(worldPoint, out int cx, out int cy, out _, out _))
            return;

        float radiusPx = Mathf.Clamp(radiusNorm * working.width * 0.6f, 10f, working.width * 0.12f);
        BurnAt(worldPoint, radiusNorm * 0.35f, 0.35f);
        PaintDarkBranches(cx, cy, 0f, radiusPx, branches, false);
        dirty = true;
    }

    /// <summary>
    /// 용암 원 가장자리에서 바깥으로 충격 크랙이 퍼짐.
    /// </summary>
    public void PaintShockCracks(Vector3 worldPoint, float craterRadiusNorm, float startFrac, float endMul, int branches, bool moltenCore)
    {
        PaintMoltenFissures(worldPoint, craterRadiusNorm, startFrac, endMul, branches);
    }

    /// <summary>
    /// 레퍼런스형 용암 균열: 검정 가장자리 + 주황 + 노란 핵 (텍스처).
    /// </summary>
    public void PaintMoltenFissures(Vector3 worldPoint, float craterRadiusNorm, float startFrac, float endMul, int branches)
    {
        EnsureWorkingTexture();
        EnsureImpactTextures();
        if (working == null || pixels == null)
            return;

        if (!TryImpactUv(worldPoint, out int cx, out int cy, out _, out _))
            return;

        float craterPx = Mathf.Clamp(craterRadiusNorm * working.width * 0.72f, 22f, working.width * 0.22f);
        float startR = craterPx * Mathf.Clamp(startFrac, 0.3f, 0.95f);
        float endR = craterPx * Mathf.Clamp(endMul, 1.1f, 3.5f);
        PaintMoltenBranches(cx, cy, startR, endR, branches);
        dirty = true;
    }

    void PaintMoltenBranches(int cx, int cy, float startR, float endR, int branches)
    {
        int w = working.width;
        int h = working.height;
        float aspect = h / (float)w;
        branches = Mathf.Clamp(branches, 6, 28);
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);

        Color32 edge = new Color32(10, 8, 7, 255);
        Color32 orange = new Color32(255, 90, 18, 255);
        Color32 core = new Color32(255, 220, 90, 255);

        for (int b = 0; b < branches; b++)
        {
            float ang = baseAngle + (Mathf.PI * 2f * b / branches) + Random.Range(-0.3f, 0.3f);
            float len = (endR - startR) * Random.Range(0.75f, 1.2f);
            int steps = Mathf.Max(16, Mathf.RoundToInt(len));

            float x = cx + Mathf.Cos(ang) * startR;
            float y = cy + Mathf.Sin(ang) * startR * aspect;
            float dir = ang + Random.Range(-0.12f, 0.12f);
            bool fork = Random.value > 0.45f;
            float forkAt = Random.Range(0.3f, 0.65f);

            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                dir += Random.Range(-0.2f, 0.2f);
                if (fork && t > forkAt)
                    dir += Random.Range(-0.5f, 0.5f);

                float step = 0.85f + Random.Range(0f, 0.5f);
                x += Mathf.Cos(dir) * step;
                y += Mathf.Sin(dir) * step * aspect;

                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (iy < 0 || iy >= h)
                    break;
                while (ix < 0) ix += w;
                while (ix >= w) ix -= w;

                float tip = 1f - t; // 용암 원 쪽이 더 굵고 밝음
                int outer = tip > 0.55f ? 3 : (tip > 0.25f ? 2 : 1);
                // 레이어: 검정 테두리 → 주황 → 노란 핵
                StampCrack(ix, iy, outer, edge, 0.75f * tip + 0.2f);
                StampCrack(ix, iy, Mathf.Max(0, outer - 1), orange, 0.85f * tip + 0.15f);
                if (tip > 0.2f)
                    StampCrack(ix, iy, 0, core, 0.55f * tip + 0.2f);

                // 용암 텍스처 살짝 섞어 줄무늬 느낌
                if (lavaColorPx != null && (s % 2 == 0))
                {
                    Color32 lava = Sample(lavaColorPx, lavaW, lavaH, ix * 0.01f, iy * 0.01f);
                    StampCrack(ix, iy, 1, lava, 0.35f * tip);
                }
            }
        }
    }

    bool TryImpactUv(Vector3 worldPoint, out int cx, out int cy, out float lat, out float lon)
    {
        cx = cy = 0;
        lat = lon = 0f;
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-6f)
            return false;

        EarthGeo.DirectionToLatLon(local.normalized, out lat, out lon);
        EarthGeo.LatLonToUv(lat, lon, out float u, out float v);
        int w = working.width;
        int h = working.height;
        cx = Mathf.Clamp(Mathf.RoundToInt(u * (w - 1)), 0, w - 1);
        cy = Mathf.Clamp(Mathf.RoundToInt(v * (h - 1)), 0, h - 1);
        return true;
    }

    void PaintDarkBranches(int cx, int cy, float startR, float endR, int branches, bool moltenCore)
    {
        int w = working.width;
        int h = working.height;
        float aspect = h / (float)w;
        branches = Mathf.Clamp(branches, 4, 28);
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);

        Color32 edge = new Color32(16, 12, 10, 255);
        Color32 deep = new Color32(8, 6, 5, 255);
        Color32 ember = new Color32(120, 28, 8, 255);

        for (int b = 0; b < branches; b++)
        {
            float ang = baseAngle + (Mathf.PI * 2f * b / branches) + Random.Range(-0.28f, 0.28f);
            float len = (endR - startR) * Random.Range(0.7f, 1.15f);
            int steps = Mathf.Max(14, Mathf.RoundToInt(len));

            // 용암 원 테두리에서 시작 → 바깥으로
            float x = cx + Mathf.Cos(ang) * startR;
            float y = cy + Mathf.Sin(ang) * startR * aspect;
            float dir = ang + Random.Range(-0.15f, 0.15f);
            bool fork = Random.value > 0.55f;
            float forkAt = Random.Range(0.35f, 0.65f);

            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                dir += Random.Range(-0.22f, 0.22f);
                if (fork && t > forkAt)
                    dir += Random.Range(-0.4f, 0.4f);

                float step = 0.9f + Random.Range(0f, 0.45f);
                x += Mathf.Cos(dir) * step;
                y += Mathf.Sin(dir) * step * aspect;

                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (iy < 0 || iy >= h)
                    break;
                while (ix < 0) ix += w;
                while (ix >= w) ix -= w;

                float tip = 1f - t;
                int thick = tip > 0.65f ? 2 : (tip > 0.3f ? 1 : 0);

                // 바깥쪽은 어두운 크랙, 안쪽(용암 근처)만 약한 잔열
                Color32 col = tip > 0.55f && moltenCore ? ember : (tip > 0.35f ? deep : edge);
                float amt = 0.5f + tip * 0.45f;
                StampCrack(ix, iy, thick, col, amt);

                // 크랙 가장자리 살짝 어둡게 (두께감)
                if (thick >= 1)
                    StampCrack(ix, iy, thick + 1, edge, 0.22f * tip);
            }
        }
    }

    void StampCrack(int cx, int cy, int thick, Color32 col, float amount)
    {
        int w = working.width;
        int h = working.height;
        for (int dy = -thick; dy <= thick; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h)
                continue;
            for (int dx = -thick; dx <= thick; dx++)
            {
                int x = cx + dx;
                while (x < 0) x += w;
                while (x >= w) x -= w;
                int idx = y * w + x;
                Color32 p = pixels[idx];
                p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, col.r, amount));
                p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, col.g, amount));
                p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, col.b, amount));
                pixels[idx] = p;
            }
        }
    }

    /// <summary>고양이 발톱 자국 — 평행 긁힌 선.</summary>
    public void PaintScratchMarks(Vector3 worldPoint, Vector3 worldNormal, int slashes, float lengthNorm, int seed = 0, int maxSteps = 0)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null)
            return;
        if (!TryImpactUv(worldPoint, out int cx, out int cy, out _, out _))
            return;

        int w = working.width;
        int h = working.height;
        float aspect = h / (float)w;
        slashes = Mathf.Clamp(slashes, 2, 6);
        float lenPx = Mathf.Clamp(lengthNorm * w * 0.55f, 18f, w * 0.18f);
        if (seed == 0)
            seed = cx * 131 + cy * 17;

        var rng = new System.Random(seed);
        float baseAngle = Mathf.Lerp(-0.55f, -0.15f, (float)rng.NextDouble());
        Color32 scrape = new Color32(48, 38, 32, 255);
        Color32 bright = new Color32(210, 195, 175, 255);

        for (int i = 0; i < slashes; i++)
        {
            float ang = baseAngle + i * (0.9f / Mathf.Max(1, slashes - 1));
            float x = cx;
            float y = cy;
            int steps = maxSteps > 0 ? maxSteps : Mathf.Max(12, Mathf.RoundToInt(lenPx));
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                x += Mathf.Cos(ang) * (lenPx / steps);
                y += Mathf.Sin(ang) * (lenPx / steps) * aspect;
                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (iy < 0 || iy >= h)
                    break;
                while (ix < 0) ix += w;
                while (ix >= w) ix -= w;
                float tip = 1f - t * 0.7f;
                StampCrack(ix, iy, tip > 0.6f ? 2 : 1, scrape, 0.55f * tip + 0.2f);
                if (s % 3 == 0)
                    StampCrack(ix, iy, 0, bright, 0.18f * tip);
            }
        }

        dirty = true;
    }

    /// <summary>평행 발톱 자국 — scratchAxisWorld 방향으로 clawCount 줄.</summary>
    public void PaintParallelScratches(
        Vector3 worldPoint, Vector3 worldNormal, Vector3 scratchAxisWorld,
        int clawCount, float lengthNorm, float spreadNorm, int seed = 0, int maxSteps = 0)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null)
            return;
        if (!TryImpactUv(worldPoint, out int cx, out int cy, out _, out _))
            return;

        Vector3 axis = Vector3.ProjectOnPlane(scratchAxisWorld, worldNormal).normalized;
        if (axis.sqrMagnitude < 1e-4f)
            return;
        Vector3 spreadW = Vector3.Cross(worldNormal, axis).normalized;

        int w = working.width;
        int h = working.height;
        float aspect = h / (float)w;

        if (!TryImpactUv(worldPoint + axis * 0.08f, out int ax, out int ay, out _, out _))
            return;
        if (!TryImpactUv(worldPoint + spreadW * 0.04f, out int sx, out int sy, out _, out _))
            return;

        float ang = Mathf.Atan2((ay - cy) * aspect, ax - cx);
        float spreadPx = Mathf.Clamp(spreadNorm * w * 0.42f, 6f, w * 0.045f);
        float lenPx = Mathf.Clamp(lengthNorm * w * 0.72f, 28f, w * 0.24f);
        clawCount = Mathf.Clamp(clawCount, 2, 5);
        int steps = maxSteps > 0 ? maxSteps : Mathf.Max(16, Mathf.RoundToInt(lenPx));
        if (seed == 0)
            seed = cx * 131 + cy * 17;

        Color32 gouge = new Color32(32, 24, 18, 255);
        Color32 rim = new Color32(195, 175, 145, 255);
        Color32 expose = new Color32(88, 58, 38, 255);

        for (int i = 0; i < clawCount; i++)
        {
            float lane = (i - (clawCount - 1) * 0.5f) * spreadPx;
            float ox = cx + Mathf.Cos(ang + Mathf.PI * 0.5f) * lane;
            float oy = cy + Mathf.Sin(ang + Mathf.PI * 0.5f) * lane * aspect;
            for (int s = 0; s < steps; s++)
            {
                float t = steps <= 1 ? 0f : s / (float)(steps - 1);
                float x = ox + Mathf.Cos(ang) * (lenPx * t);
                float y = oy + Mathf.Sin(ang) * (lenPx * t) * aspect;
                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (iy < 0 || iy >= h)
                    break;
                while (ix < 0) ix += w;
                while (ix >= w) ix -= w;
                float tip = 1f - t * 0.55f;
                StampCrack(ix, iy, tip > 0.65f ? 3 : 2, gouge, 0.72f * tip + 0.18f);
                StampCrack(ix, iy, 1, expose, 0.35f * tip);
                if (s % 2 == 0)
                    StampCrack(ix, iy, 0, rim, 0.22f * tip);
            }
        }

        dirty = true;
    }

    /// <summary>신발 박힌 자국 — 타원형 청/흰색.</summary>
    public void PaintSneakerPrint(Vector3 worldPoint, float radiusNorm, int seed = 0)
    {
        EnsureWorkingTexture();
        if (working == null || pixels == null)
            return;
        if (!TryImpactUv(worldPoint, out int cx, out int cy, out _, out _))
            return;

        int w = working.width;
        int h = working.height;
        float aspect = h / (float)w;
        float rx = Mathf.Clamp(radiusNorm * w * 0.35f, 10f, w * 0.08f);
        float ry = rx * 1.55f;
        if (seed == 0)
            seed = cx ^ cy;

        var rng = new System.Random(seed);
        float rot = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        Color32 sole = new Color32(38, 92, 168, 255);
        Color32 tread = new Color32(220, 228, 240, 255);

        for (int dy = -Mathf.CeilToInt(ry) - 1; dy <= Mathf.CeilToInt(ry) + 1; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h)
                continue;
            for (int dx = -Mathf.CeilToInt(rx) - 1; dx <= Mathf.CeilToInt(rx) + 1; dx++)
            {
                int x = cx + dx;
                while (x < 0) x += w;
                while (x >= w) x -= w;

                float lx = dx;
                float ly = dy / aspect;
                float c = Mathf.Cos(rot);
                float s = Mathf.Sin(rot);
                float ex = lx * c - ly * s;
                float ey = lx * s + ly * c;
                float d = (ex * ex) / (rx * rx) + (ey * ey) / (ry * ry);
                if (d > 1.05f)
                    continue;

                float mask = Mathf.Clamp01(1f - d);
                int idx = y * w + x;
                Color32 p = pixels[idx];
                Color32 col = Mathf.Abs(ex) < rx * 0.15f ? tread : sole;
                p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, col.r, mask * 0.72f));
                p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, col.g, mask * 0.72f));
                p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, col.b, mask * 0.72f));
                pixels[idx] = p;
            }
        }

        dirty = true;
    }
}
