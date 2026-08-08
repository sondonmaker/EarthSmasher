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
            // 알파 채널이 있는 실제 구름 맵 우선 (덩어리 JPG보다 훨씬 자연스러움)
            var alpha = Load("Earth/earth_clouds_alpha");
            if (alpha != null)
                return alpha;
            var hi = Load("Earth/earth_clouds_4k");
            return hi != null ? hi : Load("Earth/earth_clouds");
        }
    }
    public static Texture2D Water => Load("Earth/earth_water");
    public static Texture2D Topology => Load("Earth/earth_topology");
    public static Texture2D PlanetEarthFreeDay => LoadPlanetEarthFreeTexture("Earth2kTexture");
    public static Texture2D PlanetEarthFreeNormal => LoadPlanetEarthFreeTexture("Earth2kNormal");

    public static Material CreateCrustMaterial(Texture2D dayOverride = null, Texture2D nightOverride = null)
    {
        var day = dayOverride != null ? dayOverride : (PlanetEarthFreeDay != null ? PlanetEarthFreeDay : Day);
        var night = nightOverride != null ? nightOverride : Night;

        var space = Shader.Find("EarthSmasher/EarthFromSpace");
        if (space != null)
        {
            var mat = new Material(space);
            if (day != null)
                mat.mainTexture = day;
            if (night != null)
                mat.SetTexture("_NightTex", night);
            mat.color = Color.white;
            mat.SetFloat("_Exposure", 1.05f);
            mat.SetFloat("_Contrast", 1.05f);
            mat.SetFloat("_Terminator", 0.22f);
            mat.SetFloat("_NightIntensity", 1.0f);
            mat.SetFloat("_AmbientFloor", 0.05f);
            return mat;
        }

        var fb = new Material(SafeShader("Standard"));
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
        var shader = Shader.Find("EarthSmasher/CloudsSoft");
        if (shader == null)
        {
            var fallback = new Material(SafeShader("Standard"));
            fallback.mainTexture = Clouds;
            fallback.color = new Color(0.98f, 0.99f, 1f, 0.45f);
            SetTransparent(fallback);
            return fallback;
        }

        var mat = new Material(shader);
        mat.mainTexture = Clouds;
        mat.color = new Color(1f, 1f, 1f, 1f);
        // 넓은 구름 뱅크 줄이고, 소용돌이 핵심만 남김
        mat.SetFloat("_Opacity", 1.05f);
        mat.SetFloat("_AlphaBoost", 1.0f);
        mat.SetFloat("_AlphaGamma", 1.75f);
        mat.SetFloat("_CoverageCut", 0.28f);
        mat.SetFloat("_LightWrap", 0.35f);
        mat.SetFloat("_Volume", 0.28f);
        mat.SetFloat("_PolarThin", 0.88f);
        mat.SetFloat("_PolarStart", 0.55f);
        return mat;
    }

    /// <summary>구름 아래 부드러운 그림자 — 표면 깊이감.</summary>
    public static Material CreateCloudShadowMaterial()
    {
        var shader = Shader.Find("EarthSmasher/CloudShadow");
        if (shader == null)
            return null;

        var mat = new Material(shader);
        mat.mainTexture = Clouds;
        mat.SetFloat("_Strength", 0.22f);
        mat.SetFloat("_Threshold", 0.18f);
        mat.SetFloat("_Softness", 1.4f);
        return mat;
    }

    /// <summary>보조 구름층 (옵션).</summary>
    public static Material CreateCloudDetailMaterial() => CreateCloudMaterial();

    public static Material CreateCoreMaterial()
    {
        var mat = new Material(SafeShader("Standard"));
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
            // 불투명에 가깝게 — 지표면이 비치지 않게
            mat.SetColor("_DeepColor", new Color(0.008f, 0.04f, 0.12f, 0.96f));
            mat.SetColor("_ShallowColor", new Color(0.05f, 0.32f, 0.42f, 0.88f));
            mat.SetFloat("_Gloss", 0.92f);
            mat.SetFloat("_FresnelPower", 3.6f);
            mat.SetFloat("_SpecIntensity", 1.15f);
            return mat;
        }

        // 폴백
        var fb = new Material(SafeShader("Standard"));
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
            mat.SetColor("_Color", new Color(0.5f, 0.75f, 1f, 1f));
            mat.SetFloat("_RimPower", 4.5f);
            mat.SetFloat("_Intensity", 1.0f);
            mat.SetFloat("_HorizonBoost", 0.4f);
            return mat;
        }

        var fb = new Material(SafeShader("Standard"));
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
        mat.SetColor("_Color", new Color(0.65f, 0.82f, 1f, 1f));
        mat.SetFloat("_RimPower", 3.2f);
        mat.SetFloat("_Intensity", 0.3f);
        mat.SetFloat("_HorizonBoost", 0.15f);
        return mat;
    }

    /// <summary>
    /// Standard + SetTransparent 는 쓰지 않는다. 빌드에서 _ALPHABLEND_ON 변형이
    /// 스트리핑되면 알파가 1로 강제되어, 지구보다 큰 오로라 셸이 행성 전체를
    /// 시커멓게 덮어버린다. 가산 블렌딩 전용 셰이더만 사용한다.
    /// </summary>
    public static Material CreateAuroraMaterial()
    {
        var tex = EarthGeo.BuildAuroraTexture(1024, 512);

        var shader = Shader.Find("EarthSmasher/AuroraGlow");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.mainTexture = tex;
            mat.SetColor("_Color", new Color(1f, 1f, 1f, 0.85f));
            mat.SetColor("_EmissionColor", new Color(0.45f, 1f, 0.7f) * 2.0f);
            mat.SetFloat("_Intensity", 0.75f);
            return mat;
        }

        // 폴백도 가산에 가까운 언릿만 — 불투명해질 수 있는 Standard는 금지.
        var fb = new Material(SafeShader("Sprites/Default"));
        fb.mainTexture = tex;
        fb.color = new Color(0.45f, 1f, 0.7f, 0.7f);
        fb.renderQueue = 3050;
        return fb;
    }

    /// <summary>
    /// 빌드에서 셰이더가 제거되면 Shader.Find가 null을 주고 new Material(null)이
    /// 예외를 던져 부트스트랩 전체가 죽는다. 절대 null을 반환하지 않는다.
    /// </summary>
    public static Shader SafeShader(params string[] names)
    {
        foreach (string n in names)
        {
            var s = Shader.Find(n);
            if (s != null)
                return s;
        }

        foreach (string n in new[] { "Standard", "Universal Render Pipeline/Lit", "Sprites/Default", "Legacy Shaders/Diffuse" })
        {
            var s = Shader.Find(n);
            if (s != null)
            {
                Debug.LogWarning($"[EarthTextureLoader] Missing shader ({string.Join(", ", names)}) — falling back to {n}. Add it to Always Included Shaders.");
                return s;
            }
        }

        Debug.LogError("[EarthTextureLoader] No usable shader found at all.");
        return null;
    }

    static Texture2D LoadPlanetEarthFreeTexture(string fileName)
    {
        var tex = Resources.Load<Texture2D>("PlanetEarthFree/" + fileName);
        if (tex != null)
            return tex;

#if UNITY_EDITOR
        tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"Assets/Planet Earth Free/Materials/{fileName}.png");
        if (tex != null)
            return tex;

        tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"Assets/Resources/PlanetEarthFree/{fileName}.png");
#endif
        return tex;
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
