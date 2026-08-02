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
    /// AmbientCG 용암/암석 텍스처로 부드러운 크레이터 자국 (낙서 선 없음).
    /// </summary>
    public void PaintImpactCrater(Vector3 worldPoint, float radiusNorm = 0.12f)
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

        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.72f, 22f, w * 0.22f);
        int r = Mathf.CeilToInt(radiusPx * 1.4f);
        float invR = 1f / Mathf.Max(0.001f, radiusPx);
        float uvScale = Random.Range(0.35f, 0.7f);
        float uvRot = Random.Range(0f, Mathf.PI * 2f);
        float cosR = Mathf.Cos(uvRot);
        float sinR = Mathf.Sin(uvRot);

        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h)
                continue;
            for (int dx = -r; dx <= r; dx++)
            {
                float dist = Mathf.Sqrt(dx * dx + dy * dy) * invR;
                if (dist > 1.4f)
                    continue;

                int x = cx + dx;
                while (x < 0) x += w;
                while (x >= w) x -= w;

                // 원형 소프트 마스크
                float mask;
                if (dist < 0.55f)
                    mask = 0.92f;
                else if (dist < 0.85f)
                    mask = Mathf.Lerp(0.92f, 0.55f, (dist - 0.55f) / 0.3f);
                else
                    mask = Mathf.SmoothStep(1f, 0f, (dist - 0.85f) / 0.55f) * 0.55f;

                if (mask < 0.02f)
                    continue;

                float lx = (dx * cosR - dy * sinR) * uvScale / radiusPx;
                float ly = (dx * sinR + dy * cosR) * uvScale / radiusPx;
                float su = Mathf.Abs(lx) + 0.5f;
                float sv = Mathf.Abs(ly) + 0.5f;

                Color32 rock = Sample(rockColorPx, rockW, rockH, su, sv);
                Color32 lava = Sample(lavaColorPx, lavaW, lavaH, su * 1.3f, sv * 1.3f);
                Color32 emit = Sample(lavaEmitPx, lavaW, lavaH, su * 1.3f, sv * 1.3f);

                // 중심=어두운 암석+약한 용암, 링=용암, 바깥=재
                Color32 target;
                // 맞은 면 전체 = 붉은 용암 텍스처 (스크린샷 느낌)
                var hot = new Color32(
                    (byte)Mathf.Min(255, (lava.r * 2 + emit.r) / 2),
                    (byte)Mathf.Min(255, (lava.g + emit.g / 2) / 2),
                    (byte)Mathf.Min(255, lava.b / 3 + 8),
                    255);
                if (dist < 0.75f)
                {
                    float t = dist / 0.75f;
                    // 중심이 더 밝고 가장자리로 갈수록 약간 어두워짐
                    var core = new Color32(
                        (byte)Mathf.Min(255, hot.r + 40),
                        (byte)Mathf.Min(255, hot.g + 10),
                        hot.b,
                        255);
                    target = Color32.Lerp(core, hot, t);
                }
                else
                {
                    target = Color32.Lerp(hot, rock, (dist - 0.75f) / 0.65f);
                }

                int idx = y * w + x;
                Color32 p = pixels[idx];
                float a = mask;
                p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, target.r, a));
                p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, target.g, a));
                p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, target.b, a));
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

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-6f)
            return;

        EarthGeo.DirectionToLatLon(local.normalized, out float lat, out float lon);
        EarthGeo.LatLonToUv(lat, lon, out float u, out float v);

        int w = working.width;
        int h = working.height;
        int cx = Mathf.Clamp(Mathf.RoundToInt(u * (w - 1)), 0, w - 1);
        int cy = Mathf.Clamp(Mathf.RoundToInt(v * (h - 1)), 0, h - 1);

        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.6f, 10f, w * 0.12f);

        // 진앙 약한 그을림
        BurnAt(worldPoint, radiusNorm * 0.35f, 0.35f);

        Color32 crack = new Color32(18, 14, 12, 255);
        branches = Mathf.Clamp(branches, 3, 14);
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);

        for (int b = 0; b < branches; b++)
        {
            float ang = baseAngle + (Mathf.PI * 2f * b / branches) + Random.Range(-0.35f, 0.35f);
            float len = radiusPx * Random.Range(0.55f, 1.05f);
            int steps = Mathf.Max(8, Mathf.RoundToInt(len));
            float x = cx;
            float y = cy;
            float dir = ang;

            for (int s = 0; s < steps; s++)
            {
                dir += Random.Range(-0.28f, 0.28f);
                x += Mathf.Cos(dir);
                y += Mathf.Sin(dir) * (h / (float)w); // aspect-ish

                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (iy < 0 || iy >= h)
                    break;
                while (ix < 0) ix += w;
                while (ix >= w) ix -= w;

                float tip = 1f - s / (float)steps;
                int thick = tip > 0.55f ? 1 : 0;
                StampCrack(ix, iy, thick, crack, 0.55f + tip * 0.4f);
            }
        }

        dirty = true;
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
}
