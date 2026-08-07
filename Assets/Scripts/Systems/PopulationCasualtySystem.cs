using System;
using UnityEngine;

/// <summary>
/// 위·경도, 바다 마스크, 야간 조명(인구 밀도 proxy)로 타격 지점·범위별 사망자를 계산한다.
/// </summary>
public static class PopulationCasualtySystem
{
    const float DensityCutoff = 0.003f;
    const float GlobalScale = 2.65f;
    const float AreaBoostMul = 14f;
    const float LandFloorMul = 0.018f;
    const float AnchorReachDeg = 12f;

    struct PopAnchor
    {
        public float lat, lon, weight;
    }

    // 통계 기반 근사 — 야간 맵 + 농촌/내륙 보정
    static readonly PopAnchor[] Anchors =
    {
        new PopAnchor { lat = 31.2f, lon = 121.5f, weight = 1.00f }, // Shanghai
        new PopAnchor { lat = 39.9f, lon = 116.4f, weight = 0.98f }, // Beijing
        new PopAnchor { lat = 23.1f, lon = 113.3f, weight = 0.92f }, // Guangzhou
        new PopAnchor { lat = 30.6f, lon = 104.1f, weight = 0.78f }, // Chengdu
        new PopAnchor { lat = 28.6f, lon = 77.2f, weight = 0.95f }, // Delhi
        new PopAnchor { lat = 19.1f, lon = 72.9f, weight = 0.88f }, // Mumbai
        new PopAnchor { lat = 22.6f, lon = 88.4f, weight = 0.82f }, // Kolkata
        new PopAnchor { lat = 35.7f, lon = 139.7f, weight = 0.90f }, // Tokyo
        new PopAnchor { lat = 37.6f, lon = 127.0f, weight = 0.86f }, // Seoul
        new PopAnchor { lat = 14.6f, lon = 121.0f, weight = 0.72f }, // Manila
        new PopAnchor { lat = -6.2f, lon = 106.8f, weight = 0.80f }, // Jakarta
        new PopAnchor { lat = 13.8f, lon = 100.5f, weight = 0.70f }, // Bangkok
        new PopAnchor { lat = 40.7f, lon = -74.0f, weight = 0.85f }, // NYC
        new PopAnchor { lat = 34.0f, lon = -118.2f, weight = 0.78f }, // LA
        new PopAnchor { lat = 41.9f, lon = -87.6f, weight = 0.72f }, // Chicago
        new PopAnchor { lat = 51.5f, lon = -0.12f, weight = 0.68f }, // London
        new PopAnchor { lat = 48.9f, lon = 2.35f, weight = 0.65f }, // Paris
        new PopAnchor { lat = 52.5f, lon = 13.4f, weight = 0.62f }, // Berlin
        new PopAnchor { lat = 55.75f, lon = 37.62f, weight = 0.70f }, // Moscow
        new PopAnchor { lat = 30.0f, lon = 31.2f, weight = 0.74f }, // Cairo
        new PopAnchor { lat = 6.5f, lon = 3.4f, weight = 0.68f }, // Lagos
        new PopAnchor { lat = -23.5f, lon = -46.6f, weight = 0.76f }, // São Paulo
        new PopAnchor { lat = 19.4f, lon = -99.1f, weight = 0.73f }, // Mexico City
        new PopAnchor { lat = -33.9f, lon = 151.2f, weight = 0.58f }, // Sydney
        new PopAnchor { lat = -1.3f, lon = 36.8f, weight = 0.55f }, // Nairobi
        new PopAnchor { lat = 25.0f, lon = 55.3f, weight = 0.60f }, // Dubai
        new PopAnchor { lat = 33.7f, lon = 73.0f, weight = 0.64f }, // Islamabad
        new PopAnchor { lat = -34.6f, lon = -58.4f, weight = 0.66f }, // Buenos Aires
    };

    static Texture2D waterTex;
    static Color32[] waterPx;
    static int waterW, waterH;

    static Color32[] popPx;
    static int popW, popH;
    static bool mapsReady;

    /// <summary>EarthSurfaceScorch BurnAt radiusNorm → 각도(°).</summary>
    public static float ScorchNormToDegrees(float radiusNorm) => radiusNorm * 108f;

    /// <summary>EarthCraterDeform Dig/Carve radiusNorm → 각도(°).</summary>
    public static float DigNormToDegrees(float radiusNorm) => radiusNorm * 82f;

    public static bool IsOcean(float lat, float lon)
    {
        EnsureMaps();
        if (waterPx == null)
            return false;

        SampleUv(lat, lon, out float u, out float v);
        Color32 c = SampleBilinear(waterPx, waterW, waterH, u, v, wrapX: true);
        float water = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / 255f;
        return water > 0.42f;
    }

    public static float SampleDensity(float lat, float lon)
    {
        EnsureMaps();
        if (IsOcean(lat, lon))
            return 0f;

        SampleUv(lat, lon, out float u, out float v);
        float night = 0f;
        if (popPx != null)
        {
            Color32 c = SampleBilinear(popPx, popW, popH, u, v, wrapX: true);
            night = (c.r * 0.35f + c.g * 0.45f + c.b * 0.2f) / 255f;
            night = Mathf.Pow(Mathf.Clamp01(night), 0.72f);
        }

        float anchor = SampleAnchorBoost(lat, lon);
        float heuristic = HeuristicLandDensity(lat, lon);
        return Mathf.Clamp01(Mathf.Max(Mathf.Max(night, anchor), heuristic));
    }

    public static long EstimateDeaths(float lat, float lon, float radiusDeg, float lethality, float yieldMul = 1f)
    {
        lethality = Mathf.Clamp01(lethality);
        yieldMul = Mathf.Max(0.05f, yieldMul);
        radiusDeg = Mathf.Max(0.05f, radiusDeg);

        float avgDensity = IntegrateDisc(lat, lon, radiusDeg);
        if (avgDensity < DensityCutoff)
            return 0;

        float effectiveRadius = radiusDeg * Mathf.Sqrt(yieldMul);
        float solidFrac = (1f - Mathf.Cos(effectiveRadius * Mathf.Deg2Rad)) * 0.5f;
        float areaBoost = 1f + solidFrac * AreaBoostMul;
        float share = solidFrac * avgDensity * GlobalScale * Mathf.Pow(avgDensity, 0.22f) * areaBoost;

        // 넓게 부서진 육지 타격은 최소 피해 보장 (바다/극지 제외)
        float floorShare = solidFrac * LandFloorMul * Mathf.Max(0.18f, avgDensity);
        share = Mathf.Max(share, floorShare);

        long globalPop = PopulationSystem.Instance != null
            ? PopulationSystem.Instance.Population
            : PopulationSystem.BaselinePopulation;

        long deaths = (long)Mathf.Floor(globalPop * share * lethality * yieldMul);
        deaths = Math.Max(0, deaths);

        // 대규모 파괴는 인구 비례 + 절대 하한 (연출 대비 체감 보정)
        if (solidFrac > 0.0025f && avgDensity > DensityCutoff)
        {
            long minFromArea = (long)Mathf.Floor(globalPop * solidFrac * lethality * 0.0045f);
            if (deaths < minFromArea)
                deaths = minFromArea;
        }

        return deaths;
    }

    public static long ApplyAt(EarthPlanet earth, Vector3 worldPoint, float radiusDeg, float lethality, float yieldMul = 1f)
    {
        if (earth == null)
            return 0;

        Vector3 local = earth.transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-6f)
            return 0;

        EarthGeo.DirectionToLatLon(local.normalized, out float lat, out float lon);
        return ApplyAtLatLon(lat, lon, radiusDeg, lethality, yieldMul);
    }

    public static long ApplyAtLatLon(float lat, float lon, float radiusDeg, float lethality, float yieldMul = 1f)
    {
        long deaths = EstimateDeaths(lat, lon, radiusDeg, lethality, yieldMul);
        if (deaths <= 0)
            return 0;

        var pop = PopulationSystem.Instance;
        if (pop == null)
            return 0;

        pop.ApplyCasualties(deaths);

        var earth = UnityEngine.Object.FindObjectOfType<EarthPlanet>();
        if (earth != null)
            PopulationDestructionSync.EnforceCap(earth, force: true);

        return deaths;
    }

    static float IntegrateDisc(float lat, float lon, float radiusDeg)
    {
        float sum = SampleDensity(lat, lon) * 2.5f;
        float wsum = 2.5f;

        int rings = Mathf.Clamp(Mathf.CeilToInt(radiusDeg / 1.4f), 1, 10);
        Vector3 center = EarthGeo.LatLonToDirection(lat, lon);

        for (int ri = 1; ri <= rings; ri++)
        {
            float frac = ri / (float)rings;
            float angDeg = radiusDeg * frac;
            int pts = Mathf.Clamp(5 + ri * 4, 6, 36);

            for (int i = 0; i < pts; i++)
            {
                float az = i / (float)pts * Mathf.PI * 2f;
                Vector3 dir = OffsetOnSphere(center, angDeg, az);
                EarthGeo.DirectionToLatLon(dir, out float slat, out float slon);
                sum += SampleDensity(slat, slon);
                wsum += 1f;
            }
        }

        return wsum > 0f ? sum / wsum : 0f;
    }

    static float SampleAnchorBoost(float lat, float lon)
    {
        float best = 0f;
        for (int i = 0; i < Anchors.Length; i++)
        {
            var a = Anchors[i];
            float dist = AngularDistanceDeg(lat, lon, a.lat, a.lon);
            if (dist > AnchorReachDeg)
                continue;
            float fall = Mathf.Exp(-dist / 3.2f);
            best = Mathf.Max(best, a.weight * fall);
        }
        return best;
    }

    static float AngularDistanceDeg(float lat1, float lon1, float lat2, float lon2)
    {
        Vector3 a = EarthGeo.LatLonToDirection(lat1, lon1);
        Vector3 b = EarthGeo.LatLonToDirection(lat2, lon2);
        return Vector3.Angle(a, b);
    }

    static Vector3 OffsetOnSphere(Vector3 center, float angleDeg, float azimuthRad)
    {
        center.Normalize();
        Vector3 t = Vector3.Cross(center, Mathf.Abs(center.y) < 0.92f ? Vector3.up : Vector3.right).normalized;
        Vector3 b = Vector3.Cross(center, t);
        float ang = angleDeg * Mathf.Deg2Rad;
        return (Mathf.Cos(ang) * center + Mathf.Sin(ang) * (Mathf.Cos(azimuthRad) * t + Mathf.Sin(azimuthRad) * b)).normalized;
    }

    static float HeuristicLandDensity(float lat, float lon)
    {
        if (Mathf.Abs(lat) > 75f)
            return 0.012f;
        // 대략적 인구 밸트 (유라시아·아메리카·아프리카 중위도)
        float belt = 1f - Mathf.Abs(Mathf.Abs(lat) - 32f) / 58f;
        belt = Mathf.Clamp01(belt);
        return belt * 0.14f;
    }

    /// <summary>
    /// NASA equirectangular — 메시 UV 오프셋(EarthGeo)과 별도로 인구/바다 맵 샘플.
    /// EarthGeo.LatLonToUv를 쓰면 육지 클릭이 바다로 오판될 수 있다.
    /// </summary>
    static void SampleGeoStandardUv(float lat, float lon, out float u, out float v)
    {
        v = Mathf.InverseLerp(-90f, 90f, lat);
        float lonN = lon;
        while (lonN < -180f) lonN += 360f;
        while (lonN > 180f) lonN -= 360f;
        u = Mathf.InverseLerp(-180f, 180f, lonN);
    }

    static void SampleUv(float lat, float lon, out float u, out float v)
    {
        SampleGeoStandardUv(lat, lon, out u, out v);
    }

    static Color32 SampleBilinear(Color32[] px, int w, int h, float u, float v, bool wrapX)
    {
        float fx = u * (w - 1);
        float fy = v * (h - 1);
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float tx = fx - x0;
        float ty = fy - y0;

        if (wrapX)
        {
            x0 = Mod(x0, w);
            x1 = Mod(x1, w);
        }
        else
        {
            x0 = Mathf.Clamp(x0, 0, w - 1);
            x1 = Mathf.Clamp(x1, 0, w - 1);
        }

        y0 = Mathf.Clamp(y0, 0, h - 1);
        y1 = Mathf.Clamp(y1, 0, h - 1);

        Color32 c00 = px[y0 * w + x0];
        Color32 c10 = px[y0 * w + x1];
        Color32 c01 = px[y1 * w + x0];
        Color32 c11 = px[y1 * w + x1];

        return Lerp4(c00, c10, c01, c11, tx, ty);
    }

    static int Mod(int x, int m)
    {
        x %= m;
        if (x < 0) x += m;
        return x;
    }

    static Color32 Lerp4(Color32 a, Color32 b, Color32 c, Color32 d, float tx, float ty)
    {
        Color32 ab = Lerp2(a, b, tx);
        Color32 cd = Lerp2(c, d, tx);
        return Lerp2(ab, cd, ty);
    }

    static Color32 Lerp2(Color32 a, Color32 b, float t)
    {
        return new Color32(
            (byte)Mathf.Lerp(a.r, b.r, t),
            (byte)Mathf.Lerp(a.g, b.g, t),
            (byte)Mathf.Lerp(a.b, b.b, t),
            (byte)Mathf.Lerp(a.r, b.r, t));
    }

    static void EnsureMaps()
    {
        if (mapsReady)
            return;
        mapsReady = true;

        var water = EarthTextureLoader.Water;
        if (water != null && water.isReadable)
        {
            waterTex = water;
            waterW = water.width;
            waterH = water.height;
            waterPx = water.GetPixels32();
        }

        var night = EarthTextureLoader.Night;
        if (night != null)
        {
            int tw = Mathf.Min(512, night.width);
            int th = Mathf.Max(128, tw / 2);
            popPx = CaptureReadablePixels(night, tw, th, out popW, out popH);
        }

        if (popPx == null)
        {
            var day = EarthTextureLoader.Day;
            if (day != null)
            {
                int tw = Mathf.Min(512, day.width);
                int th = Mathf.Max(128, tw / 2);
                popPx = CaptureReadablePixels(day, tw, th, out popW, out popH);
            }
        }
    }

    static Color32[] CaptureReadablePixels(Texture src, int tw, int th, out int outW, out int outH)
    {
        outW = tw;
        outH = th;
        var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var readable = new Texture2D(tw, th, TextureFormat.RGB24, false);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        readable.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        readable.Apply(false, true);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = readable.GetPixels32();
        UnityEngine.Object.Destroy(readable);
        return px;
    }
}
