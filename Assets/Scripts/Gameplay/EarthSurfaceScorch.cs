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
}
