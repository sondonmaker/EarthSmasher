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

    /// <summary>
    /// 달/대형 충돌 크레이터: 검은 분지 + 용암 링 + 방사형 이젝타.
    /// </summary>
    public void PaintImpactCrater(Vector3 worldPoint, float radiusNorm = 0.12f)
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

        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.7f, 18f, w * 0.2f);
        int r = Mathf.CeilToInt(radiusPx * 1.35f);
        float invR = 1f / Mathf.Max(0.001f, radiusPx);

        Color32 basin = new Color32(12, 10, 9, 255);
        Color32 ash = new Color32(32, 26, 22, 255);
        Color32 molten = new Color32(255, 90, 18, 255);
        Color32 glow = new Color32(255, 160, 40, 255);
        Color32 ejecta = new Color32(48, 40, 34, 255);

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
                if (dist > 1.35f)
                    continue;

                int idx = y * w + x;
                Color32 p = pixels[idx];

                if (dist <= 0.55f)
                {
                    // 분지 중심 — 거의 검게
                    float a = Mathf.SmoothStep(1f, 0.35f, dist / 0.55f);
                    p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, basin.r, a));
                    p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, basin.g, a));
                    p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, basin.b, a));
                }
                else if (dist <= 0.78f)
                {
                    // 용암 링
                    float rim = 1f - Mathf.Abs(dist - 0.66f) / 0.14f;
                    rim = Mathf.Clamp01(rim);
                    rim *= rim;
                    Color32 hot = Color32.Lerp(molten, glow, rim * 0.55f);
                    p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, hot.r, rim * 0.92f));
                    p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, hot.g, rim * 0.85f));
                    p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, hot.b, rim * 0.7f));
                }
                else
                {
                    // 바깥 재/이젝타 담요
                    float a = Mathf.SmoothStep(1f, 0f, (dist - 0.78f) / 0.57f) * 0.7f;
                    p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, ash.r, a));
                    p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, ash.g, a));
                    p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, ash.b, a));
                }

                pixels[idx] = p;
            }
        }

        // 방사형 이젝타 줄무늬
        int rays = 14;
        float baseAng = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < rays; i++)
        {
            float ang = baseAng + (Mathf.PI * 2f * i / rays) + Random.Range(-0.12f, 0.12f);
            float len = radiusPx * Random.Range(0.95f, 1.45f);
            int steps = Mathf.Max(10, Mathf.RoundToInt(len));
            float x = cx + Mathf.Cos(ang) * radiusPx * 0.55f;
            float y = cy + Mathf.Sin(ang) * radiusPx * 0.55f * (h / (float)w);

            for (int s = 0; s < steps; s++)
            {
                x += Mathf.Cos(ang);
                y += Mathf.Sin(ang) * (h / (float)w);
                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (iy < 0 || iy >= h)
                    break;
                while (ix < 0) ix += w;
                while (ix >= w) ix -= w;

                float tip = 1f - s / (float)steps;
                float amount = 0.25f + tip * 0.45f;
                int thick = tip > 0.4f ? 1 : 0;
                for (int dy = -thick; dy <= thick; dy++)
                {
                    int yy = iy + dy;
                    if (yy < 0 || yy >= h)
                        continue;
                    for (int dx = -thick; dx <= thick; dx++)
                    {
                        int xx = ix + dx;
                        while (xx < 0) xx += w;
                        while (xx >= w) xx -= w;
                        int idx = yy * w + xx;
                        Color32 p = pixels[idx];
                        p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, ejecta.r, amount));
                        p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, ejecta.g, amount));
                        p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, ejecta.b, amount));
                        pixels[idx] = p;
                    }
                }
            }
        }

        // 용암 균열 — 분지에서 바깥으로 빛나는 금
        PaintLavaCracksAtUv(cx, cy, radiusPx, Mathf.RoundToInt(Mathf.Lerp(10f, 22f, radiusNorm / 0.25f)));

        dirty = true;
    }

    /// <summary>
    /// 충돌 후 용암처럼 빛나는 방사형 크랙.
    /// </summary>
    public void PaintLavaCracks(Vector3 worldPoint, float radiusNorm = 0.12f, int branches = 16)
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
        float radiusPx = Mathf.Clamp(radiusNorm * w * 0.75f, 20f, w * 0.22f);
        PaintLavaCracksAtUv(cx, cy, radiusPx, branches);
        dirty = true;
    }

    void PaintLavaCracksAtUv(int cx, int cy, float radiusPx, int branches)
    {
        int w = working.width;
        int h = working.height;
        branches = Mathf.Clamp(branches, 6, 28);
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);

        Color32 core = new Color32(255, 220, 80, 255);   // 노란 핵
        Color32 lava = new Color32(255, 70, 10, 255);    // 주황 용암
        Color32 ember = new Color32(180, 30, 8, 255);    // 가장자리 잿빛

        for (int b = 0; b < branches; b++)
        {
            float ang = baseAngle + (Mathf.PI * 2f * b / branches) + Random.Range(-0.4f, 0.4f);
            float len = radiusPx * Random.Range(0.65f, 1.35f);
            int steps = Mathf.Max(12, Mathf.RoundToInt(len));
            float x = cx;
            float y = cy;
            float dir = ang;
            bool split = Random.value > 0.55f;
            float splitAt = Random.Range(0.35f, 0.7f);

            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                dir += Random.Range(-0.32f, 0.32f);
                if (split && t > splitAt)
                    dir += Random.Range(-0.55f, 0.55f);

                x += Mathf.Cos(dir) * (0.85f + Random.Range(0f, 0.4f));
                y += Mathf.Sin(dir) * (h / (float)w) * (0.85f + Random.Range(0f, 0.4f));

                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (iy < 0 || iy >= h)
                    break;
                while (ix < 0) ix += w;
                while (ix >= w) ix -= w;

                // 중심부일수록 밝고 두껍게
                float heat = Mathf.Lerp(1f, 0.25f, t);
                int thick = heat > 0.7f ? 2 : (heat > 0.4f ? 1 : 0);
                Color32 col = heat > 0.75f ? core : (heat > 0.4f ? lava : ember);
                StampCrack(ix, iy, thick, col, 0.65f + heat * 0.35f);

                // 주변에 약한 열기 후광
                if (heat > 0.5f && (s % 2 == 0))
                    StampCrack(ix, iy, thick + 1, lava, 0.25f * heat);
            }
        }

        // 분지 바닥 용암 웅덩이
        int poolR = Mathf.CeilToInt(radiusPx * 0.28f);
        for (int dy = -poolR; dy <= poolR; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h) continue;
            for (int dx = -poolR; dx <= poolR; dx++)
            {
                float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(1f, poolR);
                if (d > 1f) continue;
                int x = cx + dx;
                while (x < 0) x += w;
                while (x >= w) x -= w;
                float a = (1f - d) * (1f - d) * Random.Range(0.45f, 0.95f);
                if (a < 0.15f) continue;
                Color32 hot = Color32.Lerp(ember, core, a);
                int idx = y * w + x;
                Color32 p = pixels[idx];
                p.r = (byte)Mathf.RoundToInt(Mathf.Lerp(p.r, hot.r, a));
                p.g = (byte)Mathf.RoundToInt(Mathf.Lerp(p.g, hot.g, a * 0.9f));
                p.b = (byte)Mathf.RoundToInt(Mathf.Lerp(p.b, hot.b, a * 0.55f));
                pixels[idx] = p;
            }
        }
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
