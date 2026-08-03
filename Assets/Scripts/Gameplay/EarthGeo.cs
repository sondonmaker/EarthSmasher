using UnityEngine;

/// <summary>
/// 실제 지구 좌표/자기극 기준 지리 유틸.
/// Unity Sphere UV: V=0 남극(-Y), V=1 북극(+Y), U=경도.
/// </summary>
public static class EarthGeo
{
    // IGRF / World Magnetic Model 근사값 (2024–2025)
    // 북자극(자기 북극, 지리 좌표): 캐나다 북부 방향
    public const float MagneticNorthLat = 86.50f;
    public const float MagneticNorthLon = -164.04f; // 195.96°E → -164°W

    // 남자극(자기 남극)
    public const float MagneticSouthLat = -64.09f;
    public const float MagneticSouthLon = 135.87f; // 남극 대륙 쪽

    // 조용한 때의 오로라 타원 (자기위도 °)
    public const float AuroraOvalCenterMagLat = 67f;
    public const float AuroraOvalHalfWidthDeg = 5.5f;

    /// <summary>위도(-90~90), 경도(-180~180) → 단위 구 방향 (Y-up)</summary>
    public static Vector3 LatLonToDirection(float latDeg, float lonDeg)
    {
        float lat = latDeg * Mathf.Deg2Rad;
        float lon = lonDeg * Mathf.Deg2Rad;
        float cosLat = Mathf.Cos(lat);
        return new Vector3(
            cosLat * Mathf.Sin(lon),
            Mathf.Sin(lat),
            cosLat * Mathf.Cos(lon)).normalized;
    }

    /// <summary>구면 방향 → 위도/경도</summary>
    public static void DirectionToLatLon(Vector3 dir, out float latDeg, out float lonDeg)
    {
        dir = dir.normalized;
        latDeg = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        lonDeg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    // 실제 지구 메시의 UV 규칙. EarthMeshBuilder가 기준 메시에서 역산해 채운다.
    // 이게 틀리면 지형은 맞는 곳이 파이는데 그을음/용암 텍스처는 엉뚱한 곳(반대편)에 찍힌다.
    public static float UvLonOffset;
    public static bool UvLonMirrored;
    public static bool UvLatFlipped;

    /// <summary>텍스처 UV → 위경도</summary>
    public static void UvToLatLon(float u, float v, out float latDeg, out float lonDeg)
    {
        float vBase = UvLatFlipped ? 1f - v : v;
        latDeg = Mathf.Lerp(-90f, 90f, vBase);

        float eu = UvLonMirrored
            ? Mathf.Repeat(UvLonOffset - u, 1f)
            : Mathf.Repeat(u - UvLonOffset, 1f);
        lonDeg = eu * 360f - 180f;
    }

    /// <summary>위경도 → 텍스처 UV</summary>
    public static void LatLonToUv(float latDeg, float lonDeg, out float u, out float v)
    {
        float vBase = Mathf.InverseLerp(-90f, 90f, latDeg);
        v = UvLatFlipped ? 1f - vBase : vBase;

        float lon = lonDeg;
        if (lon < -180f) lon += 360f;
        if (lon > 180f) lon -= 360f;
        float eu = Mathf.InverseLerp(-180f, 180f, lon);

        u = UvLonMirrored
            ? Mathf.Repeat(UvLonOffset - eu, 1f)
            : Mathf.Repeat(eu + UvLonOffset, 1f);
    }

    /// <summary>
    /// 지리 지점의 자기위도(대략).
    /// 해당 자극까지의 각거리로 계산: magLat = 90 - angularDistance.
    /// </summary>
    public static float MagneticLatitude(float latDeg, float lonDeg, bool northern)
    {
        Vector3 p = LatLonToDirection(latDeg, lonDeg);
        Vector3 pole = northern
            ? LatLonToDirection(MagneticNorthLat, MagneticNorthLon)
            : LatLonToDirection(MagneticSouthLat, MagneticSouthLon);
        float ang = Vector3.Angle(p, pole); // 0 at pole … 180 opposite
        return 90f - ang;
    }

    /// <summary>오로라 타원 안이면 0~1 강도</summary>
    public static float AuroraIntensityAt(float latDeg, float lonDeg)
    {
        float n = OvalBand(MagneticLatitude(latDeg, lonDeg, true));
        float s = OvalBand(MagneticLatitude(latDeg, lonDeg, false));
        return Mathf.Max(n, s);
    }

    static float OvalBand(float magLat)
    {
        // |magLat| 가 타원 중심 근처에 있을수록 강함 (극=90, 적도=0)
        float absLat = Mathf.Abs(magLat);
        float d = Mathf.Abs(absLat - AuroraOvalCenterMagLat);
        if (d > AuroraOvalHalfWidthDeg * 2f) return 0f;
        return Mathf.Clamp01(1f - d / (AuroraOvalHalfWidthDeg * 1.4f));
    }

    /// <summary>자기극/오로라 타원 기반 오로라 텍스처 생성</summary>
    public static Texture2D BuildAuroraTexture(int width = 1024, int height = 512)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                UvToLatLon(u, v, out float lat, out float lon);

                float baseI = AuroraIntensityAt(lat, lon);
                // 경도 방향 물결 — 실제 오로라 아치 느낌
                float wave = 0.65f + 0.35f * Mathf.Sin(lon * Mathf.Deg2Rad * 6f + lat * 0.08f);
                float i = baseI * wave;

                // 북=초록, 남=자홍 섞인 초록
                bool northish = lat > 0f;
                Color col = northish
                    ? new Color(0.25f * i, 1f * i, 0.55f * i, i * 0.85f)
                    : new Color(0.55f * i, 0.85f * i, 1f * i, i * 0.85f);

                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply(false, false);
        return tex;
    }

    /// <summary>바다 마스크(흰색=물)로 알파 텍스처 보정. Read/Write 꺼져 있으면 null.</summary>
    public static Texture2D BuildOceanOverlay(Texture2D waterMask, Color oceanColor)
    {
        if (waterMask == null || !waterMask.isReadable)
            return null;

        int w = waterMask.width;
        int h = waterMask.height;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = waterMask.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            float water = Mathf.Max(pixels[i].r, Mathf.Max(pixels[i].g, pixels[i].b));
            water = Mathf.SmoothStep(0.15f, 0.55f, water);
            var c = oceanColor;
            c.a = water * oceanColor.a;
            pixels[i] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }
}
