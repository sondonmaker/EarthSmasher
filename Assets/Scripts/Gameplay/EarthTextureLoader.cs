using UnityEngine;

/// <summary>
/// Resources/Earth 텍스처 + 커스텀 셰이더로 고급 지구 비주얼.
/// </summary>
public static class EarthTextureLoader
{
    public static Texture2D Day => Load("Earth/earth_day");
    public static Texture2D Night => Load("Earth/earth_night");
    public static Texture2D Clouds
    {
        get
        {
            var hi = Load("Earth/earth_clouds_4k");
            return hi != null ? hi : Load("Earth/earth_clouds");
        }
    }
    public static Texture2D Water => Load("Earth/earth_water");
    public static Texture2D Topology => Load("Earth/earth_topology");

    public static Material CreateCrustMaterial(Texture2D dayOverride = null, Texture2D nightOverride = null)
    {
        var day = dayOverride != null ? dayOverride : Day;
        var night = nightOverride != null ? nightOverride : Night;

        var space = Shader.Find("EarthSmasher/EarthFromSpace");
        if (space != null)
        {
            var mat = new Material(space);
            if (day != null)
                mat.mainTexture = day;
            if (night != null)
                mat.SetTexture("_NightTex", night);
            mat.color = new Color(0.82f, 0.86f, 0.9f, 1f);
            mat.SetFloat("_Exposure", 0.72f);
            mat.SetFloat("_Contrast", 1.28f);
            mat.SetFloat("_Terminator", 0.26f);
            mat.SetFloat("_NightIntensity", 1.05f);
            mat.SetFloat("_AmbientFloor", 0.018f);
            return mat;
        }

        var fb = new Material(Shader.Find("Standard"));
        if (day != null)
        {
            fb.mainTexture = day;
            fb.color = new Color(0.75f, 0.78f, 0.82f, 1f);
            fb.SetFloat("_Glossiness", 0.35f);
        }
        else
            fb.color = new Color(0.08f, 0.2f, 0.45f);

        if (night != null)
        {
            fb.EnableKeyword("_EMISSION");
            fb.SetTexture("_EmissionMap", night);
            fb.SetColor("_EmissionColor", new Color(1.1f, 1f, 0.85f) * 0.45f);
        }
        return fb;
    }

    public static Material CreateCloudMaterial()
    {
        // 레퍼런스: 소용돌이·얇은 베일 유지, 코어만 밝고 선명
        return BuildCloudMaterial(0.78f, 0.95f, 0.22f, 1.55f, Vector2.zero, Vector2.one,
            new Color(0.98f, 0.99f, 1f, 1f));
    }

    /// <summary>구름 아래 부드러운 그림자 — 표면 깊이감.</summary>
    public static Material CreateCloudShadowMaterial()
    {
        var shader = Shader.Find("EarthSmasher/CloudShadow");
        if (shader == null)
            return null;

        var mat = new Material(shader);
        mat.mainTexture = Clouds;
        mat.SetFloat("_Strength", 0.48f);
        mat.SetFloat("_Threshold", 0.2f);
        mat.SetFloat("_Softness", 1.15f);
        return mat;
    }

    /// <summary>보조 구름층 (옵션).</summary>
    public static Material CreateCloudDetailMaterial()
    {
        return BuildCloudMaterial(0.22f, 1.05f, 0.32f, 1.35f, new Vector2(0.17f, 0.08f), new Vector2(1.35f, 1.35f),
            new Color(0.94f, 0.96f, 0.99f, 1f));
    }

    static Material BuildCloudMaterial(float opacity, float softness, float threshold, float contrast, Vector2 offset, Vector2 tiling, Color tint)
    {
        var shader = Shader.Find("EarthSmasher/CloudsSoft");
        if (shader == null)
        {
            var fallback = new Material(Shader.Find("Standard"));
            fallback.mainTexture = Clouds;
            fallback.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(opacity * 0.45f));
            SetTransparent(fallback);
            return fallback;
        }

        var mat = new Material(shader);
        mat.mainTexture = Clouds;
        mat.color = tint;
        mat.SetFloat("_Opacity", opacity);
        mat.SetFloat("_Softness", softness);
        mat.SetFloat("_Threshold", threshold);
        mat.SetFloat("_Contrast", contrast);
        mat.SetFloat("_LightWrap", 0.38f);
        mat.SetFloat("_Volume", 0.45f);
        mat.mainTextureOffset = offset;
        mat.mainTextureScale = tiling;
        return mat;
    }

    public static Material CreateCoreMaterial()
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.28f, 0.04f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.08f) * 4.5f);
        mat.SetFloat("_Glossiness", 0.7f);
        return mat;
    }

    public static Material CreateOceanMaterial()
    {
        var shader = Shader.Find("EarthSmasher/OceanCoastal");
        var water = Water;

        if (shader != null && water != null)
        {
            var mat = new Material(shader);
            mat.mainTexture = water;
            // 레퍼런스: 거의 검정에 가까운 심해 — 흰 구름 대비
            mat.SetColor("_DeepColor", new Color(0.004f, 0.02f, 0.08f, 0.72f));
            mat.SetColor("_ShallowColor", new Color(0.04f, 0.35f, 0.48f, 0.45f));
            mat.SetFloat("_Gloss", 0.96f);
            mat.SetFloat("_FresnelPower", 3.4f);
            mat.SetFloat("_SpecIntensity", 1.35f);
            return mat;
        }

        // 폴백
        var fb = new Material(Shader.Find("Standard"));
        var oceanColor = new Color(0.02f, 0.28f, 0.55f, 0.45f);
        if (water != null)
        {
            var overlay = EarthGeo.BuildOceanOverlay(water, oceanColor);
            fb.mainTexture = overlay != null ? overlay : water;
            fb.color = Color.white;
        }
        else fb.color = oceanColor;
        fb.SetFloat("_Glossiness", 0.95f);
        SetTransparent(fb);
        return fb;
    }

    public static Material CreateAtmosphereMaterial()
    {
        var shader = Shader.Find("EarthSmasher/AtmosphereFresnel");
        if (shader != null)
        {
            var mat = new Material(shader);
            // 달/우주 시점: 얇고 밝은 청록 림
            mat.SetColor("_Color", new Color(0.45f, 0.78f, 1f, 1f));
            mat.SetFloat("_RimPower", 4.8f);
            mat.SetFloat("_Intensity", 1.55f);
            mat.SetFloat("_HorizonBoost", 0.55f);
            return mat;
        }

        var fb = new Material(Shader.Find("Standard"));
        fb.color = new Color(0.45f, 0.7f, 1f, 0.2f);
        fb.EnableKeyword("_EMISSION");
        fb.SetColor("_EmissionColor", new Color(0.35f, 0.6f, 1f) * 0.5f);
        SetTransparent(fb);
        fb.renderQueue = 3100;
        return fb;
    }

    /// <summary>바깥쪽 더 부드러운 헤일로</summary>
    public static Material CreateAtmosphereHaloMaterial()
    {
        var shader = Shader.Find("EarthSmasher/AtmosphereFresnel");
        if (shader == null) return CreateAtmosphereMaterial();

        var mat = new Material(shader);
        mat.SetColor("_Color", new Color(0.55f, 0.8f, 1f, 1f));
        mat.SetFloat("_RimPower", 3.6f);
        mat.SetFloat("_Intensity", 0.28f);
        mat.SetFloat("_HorizonBoost", 0.2f);
        return mat;
    }

    public static Material CreateAuroraMaterial()
    {
        var mat = new Material(Shader.Find("Standard"));
        var tex = EarthGeo.BuildAuroraTexture(1024, 512);
        mat.mainTexture = tex;
        mat.color = Color.white;
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", new Color(0.45f, 1f, 0.7f) * 2.0f);
        SetTransparent(mat);
        mat.renderQueue = 3050;
        return mat;
    }

    static Texture2D Load(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null)
            Debug.LogWarning($"[EarthTextureLoader] Missing Resources/{path}");
        return tex;
    }

    static void SetTransparent(Material mat)
    {
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}
