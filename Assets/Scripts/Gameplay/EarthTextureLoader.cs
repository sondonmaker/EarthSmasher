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

        var mat = new Material(Shader.Find("Standard"));
        if (day != null)
        {
            mat.mainTexture = day;
            mat.color = Color.white;
            // 바다 하이라이트가 느껴지도록 적당히 광택
            mat.SetFloat("_Glossiness", 0.55f);
            mat.SetFloat("_Metallic", 0.02f);
        }
        else
        {
            mat.color = new Color(0.12f, 0.38f, 0.85f);
            mat.SetFloat("_Glossiness", 0.55f);
        }

        if (night != null)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", night);
            // 낮 쪽은 약하게, 밤 도시광
            mat.SetColor("_EmissionColor", new Color(1.15f, 1.05f, 0.9f) * 0.55f);
        }

        return mat;
    }

    public static Material CreateCloudMaterial()
    {
        // 얇은 구름은 잘라내고(커버리지↓), 남은 덩어리는 선명하게
        return BuildCloudMaterial(0.9f, 0.72f, 0.4f, 1.7f, Vector2.zero, Vector2.one,
            new Color(0.96f, 0.97f, 0.99f, 1f));
    }

    /// <summary>보조 구름층 (현재 미사용 — 메인만으로 커버리지 조절).</summary>
    public static Material CreateCloudDetailMaterial()
    {
        return BuildCloudMaterial(0.2f, 0.9f, 0.45f, 1.4f, new Vector2(0.17f, 0.08f), new Vector2(1.35f, 1.35f),
            new Color(0.92f, 0.94f, 0.97f, 1f));
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
        mat.SetFloat("_LightWrap", 0.42f);
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
            mat.SetColor("_DeepColor", new Color(0.01f, 0.07f, 0.26f, 0.5f));
            mat.SetColor("_ShallowColor", new Color(0.08f, 0.62f, 0.7f, 0.42f));
            mat.SetFloat("_Gloss", 0.94f);
            mat.SetFloat("_FresnelPower", 3.2f);
            mat.SetFloat("_SpecIntensity", 1.6f);
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
            mat.SetColor("_Color", new Color(0.55f, 0.78f, 1f, 1f));
            mat.SetFloat("_RimPower", 3.2f);
            mat.SetFloat("_Intensity", 0.85f);
            mat.SetFloat("_HorizonBoost", 0.25f);
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
        mat.SetColor("_Color", new Color(0.7f, 0.85f, 1f, 1f));
        mat.SetFloat("_RimPower", 2.4f);
        mat.SetFloat("_Intensity", 0.35f);
        mat.SetFloat("_HorizonBoost", 0.12f);
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
