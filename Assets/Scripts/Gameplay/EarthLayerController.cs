using UnityEngine;

/// <summary>
/// 지구 시각 레이어 — 실데이터(물 마스크 / 자기극 오로라 타원) 기반.
/// </summary>
public class EarthLayerController : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] GameObject crust;
    [SerializeField] GameObject ocean;
    [SerializeField] GameObject clouds;
    [SerializeField] GameObject atmosphere;
    [SerializeField] GameObject aurora;

    [Header("Debug markers (자기극)")]
    // 개발용 마커. 켜면 자북/자남에 발광 구체가 떠서 우주 뷰를 해친다.
    [SerializeField] bool showMagneticPoles = false;

    [Header("State")]
    public bool oceanEnabled = false;
    public bool cloudsEnabled = true;
    public bool atmosphereEnabled = true;
    public bool auroraEnabled = true;
    public bool nightLightsEnabled = true;

    [Range(0f, 1f)] public float oceanStrength = 0.95f;
    [Range(0f, 1f)] public float cloudsStrength = 0.62f;
    [Range(0f, 1f)] public float atmosphereStrength = 0.5f;
    [Range(0f, 1f)] public float auroraStrength = 0.85f;
    [Range(0f, 2f)] public float nightLightsStrength = 0.85f;

    /// <summary>자기 활동 — 타원 확장을 흉내 (0=조용, 1=폭풍)</summary>
    [Range(0f, 1f)] public float geomagneticActivity = 0.25f;

    Renderer _oceanRend;
    Renderer _cloudsRend;
    Renderer _atmosphereRend;
    Renderer _auroraRend;
    Material _crustMat;
    Color _nightEmissionBase = new Color(1.1f, 1f, 0.85f);
    Transform _poleN;
    Transform _poleS;
    float _baseAuroraEmission = 2.2f;

    public void Bind(
        GameObject crustGo,
        GameObject oceanGo,
        GameObject cloudsGo,
        GameObject atmosphereGo,
        GameObject auroraGo)
    {
        crust = crustGo;
        ocean = oceanGo;
        clouds = cloudsGo;
        atmosphere = atmosphereGo;
        aurora = auroraGo;

        _oceanRend = ocean != null ? ocean.GetComponent<Renderer>() : null;
        _cloudsRend = clouds != null ? clouds.GetComponent<Renderer>() : null;
        _atmosphereRend = atmosphere != null ? atmosphere.GetComponent<Renderer>() : null;
        _auroraRend = aurora != null ? aurora.GetComponent<Renderer>() : null;

        var crustRend = crust != null ? crust.GetComponent<Renderer>() : null;
        if (crustRend != null)
            _crustMat = crustRend.material;

        BuildMagneticPoleMarkers();
        ApplyAll();
    }

    void BuildMagneticPoleMarkers()
    {
        if (!showMagneticPoles || crust == null) return;

        _poleN = CreatePoleMarker("MagneticNorth", EarthGeo.MagneticNorthLat, EarthGeo.MagneticNorthLon, new Color(0.3f, 1f, 0.6f));
        _poleS = CreatePoleMarker("MagneticSouth", EarthGeo.MagneticSouthLat, EarthGeo.MagneticSouthLon, new Color(0.5f, 0.7f, 1f));
    }

    Transform CreatePoleMarker(string name, float lat, float lon, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(crust.transform, false);
        go.transform.localPosition = EarthGeo.LatLonToDirection(lat, lon) * 0.52f;
        go.transform.localScale = Vector3.one * 0.035f;
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(color, 2.5f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    void Update()
    {
        if (!auroraEnabled || _auroraRend == null) return;

        // 약한 숨쉬기 + 자기활동에 따른 밝기
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 1.4f);
        float activity = Mathf.Lerp(0.65f, 1.35f, geomagneticActivity);
        float e = _baseAuroraEmission * auroraStrength * pulse * activity;
        var mat = _auroraRend.material;
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", new Color(0.45f, 1f, 0.7f) * e);

        Color c = mat.HasProperty("_Color") ? mat.GetColor("_Color") : mat.color;
        c.a = Mathf.Clamp01(auroraStrength * 0.9f * pulse);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        mat.color = c;
    }

    public void ApplyAll()
    {
        SetActive(ocean, oceanEnabled);
        SetActive(clouds, cloudsEnabled);
        SetActive(atmosphere, atmosphereEnabled);
        SetActive(aurora, auroraEnabled);
        if (_poleN != null) _poleN.gameObject.SetActive(showMagneticPoles && auroraEnabled);
        if (_poleS != null) _poleS.gameObject.SetActive(showMagneticPoles && auroraEnabled);

        if (_oceanRend != null)
            SetLayerAlpha(_oceanRend, Mathf.Lerp(0.75f, 1f, oceanStrength), false);

        if (_cloudsRend != null)
        {
            // 메인 + 서브 구름층 모두 같은 강도로 (자식 CloudsHigh 포함)
            // Color.a 를 너무 깎으면 구름이 사라짐 — 강도는 거의 그대로 유지
            float cloudA = Mathf.Lerp(0.55f, 1f, cloudsStrength);
            SetLayerAlpha(_cloudsRend, cloudA, false);
            if (_cloudsRend != null && _cloudsRend.material != null && _cloudsRend.material.HasProperty("_Opacity"))
                _cloudsRend.material.SetFloat("_Opacity", Mathf.Lerp(0.55f, 1.1f, cloudsStrength));
            var childRends = clouds.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < childRends.Length; i++)
            {
                if (childRends[i] == _cloudsRend)
                    continue;
                // 그림자 레이어는 알파 스케일하지 않음 (Multiply)
                if (childRends[i].gameObject.name == "CloudShadow")
                    continue;
                SetLayerAlpha(childRends[i], cloudA * 0.65f, false);
            }
        }

        if (_atmosphereRend != null)
            SetLayerAlpha(_atmosphereRend, atmosphereStrength * 0.35f, false);

        ApplyNightLights();
    }

    void ApplyNightLights()
    {
        if (_crustMat == null) return;

        if (_crustMat.HasProperty("_NightIntensity"))
        {
            _crustMat.SetFloat("_NightIntensity", nightLightsEnabled ? nightLightsStrength * 1.2f : 0f);
            return;
        }

        if (nightLightsEnabled && _crustMat.HasProperty("_EmissionColor"))
        {
            _crustMat.EnableKeyword("_EMISSION");
            _crustMat.SetColor("_EmissionColor", _nightEmissionBase * nightLightsStrength);
        }
        else if (_crustMat.HasProperty("_EmissionColor"))
        {
            _crustMat.SetColor("_EmissionColor", Color.black);
            _crustMat.DisableKeyword("_EMISSION");
        }
    }

    static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on)
            go.SetActive(on);
    }

    static void SetLayerAlpha(Renderer rend, float alpha, bool emissive)
    {
        if (rend == null) return;
        var mat = rend.material;
        alpha = Mathf.Clamp01(alpha);

        if (mat.HasProperty("_Color"))
        {
            Color c = mat.GetColor("_Color");
            c.a = alpha;
            mat.SetColor("_Color", c);
        }
        else if (mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
        }
        else if (mat.HasProperty("_DeepColor"))
        {
            Color deep = mat.GetColor("_DeepColor");
            deep.a = Mathf.Clamp01(Mathf.Max(0.85f, alpha));
            mat.SetColor("_DeepColor", deep);
            if (mat.HasProperty("_ShallowColor"))
            {
                Color shallow = mat.GetColor("_ShallowColor");
                shallow.a = Mathf.Clamp01(Mathf.Max(0.75f, alpha * 0.92f));
                mat.SetColor("_ShallowColor", shallow);
            }
        }
        else if (mat.HasProperty("_Opacity"))
        {
            mat.SetFloat("_Opacity", alpha);
        }
        else if (mat.HasProperty("_Intensity"))
        {
            mat.SetFloat("_Intensity", alpha * 0.85f);
        }

        if (emissive && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            Color e = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            mat.SetColor("_EmissionColor", new Color(e.r, e.g, e.b) * (alpha * 2.5f));
        }
    }
}
