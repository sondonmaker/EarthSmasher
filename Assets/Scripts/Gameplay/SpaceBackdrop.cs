using UnityEngine;

/// <summary>
/// NASA SVS Deep Star Maps 기반 사실적 우주 스카이박스.
/// Resources/Space/starmap_2020_*_gal.exr 가 있으면 프로시저럴 배경은 쓰지 않는다.
/// </summary>
public class SpaceBackdrop : MonoBehaviour
{
    [SerializeField] float exposure = 1.15f;
    [SerializeField] float rotation = 0f;
    [SerializeField] bool prefer8K = true;

    Material skyMat;
    bool usingNasaMap;

    public bool UsingNasaMap => usingNasaMap;

    void Awake()
    {
        transform.position = Vector3.zero;
        usingNasaMap = TryApplyNasaSkybox();
        if (!usingNasaMap)
        {
            Debug.LogWarning(
                "[SpaceBackdrop] NASA star map missing in Resources/Space/. " +
                "Expected starmap_2020_8k_gal or starmap_2020_4k_gal. " +
                "Procedural fallback disabled to avoid a cheap look — sky stays dark.");
            ApplyDarkSkyFallback();
        }
    }

    void OnDestroy()
    {
        if (skyMat != null)
            Destroy(skyMat);
    }

    bool TryApplyNasaSkybox()
    {
        Texture tex = null;
        if (prefer8K)
            tex = Resources.Load<Texture>("Space/starmap_2020_8k_gal");
        if (tex == null)
            tex = Resources.Load<Texture>("Space/starmap_2020_4k_gal");
        if (tex == null)
            return false;

        var shader = Shader.Find("Skybox/Panoramic");
        if (shader == null)
        {
            Debug.LogError("[SpaceBackdrop] Skybox/Panoramic shader not found.");
            return false;
        }

        skyMat = new Material(shader);
        skyMat.name = "NasaDeepStarMapSkybox";
        skyMat.SetTexture("_MainTex", tex);
        if (skyMat.HasProperty("_Tex"))
            skyMat.SetTexture("_Tex", tex);
        if (skyMat.HasProperty("_Exposure"))
            skyMat.SetFloat("_Exposure", exposure);
        if (skyMat.HasProperty("_Rotation"))
            skyMat.SetFloat("_Rotation", rotation);
        // Latitude/Longitude mapping for equirectangular
        if (skyMat.HasProperty("_Mapping"))
            skyMat.SetFloat("_Mapping", 1f); // 1 = Latitude Longitude typically
        if (skyMat.HasProperty("_ImageType"))
            skyMat.SetFloat("_ImageType", 0f); // 0 = 360 Degrees

        RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.04f, 0.05f, 0.08f);
        RenderSettings.ambientEquatorColor = new Color(0.02f, 0.025f, 0.04f);
        RenderSettings.ambientGroundColor = Color.black;
        DynamicGI.UpdateEnvironment();

        ApplyCameraSkybox();
        Debug.Log($"[SpaceBackdrop] NASA SVS Deep Star Maps skybox active ({tex.width}x{tex.height}).");
        return true;
    }

    void ApplyDarkSkyFallback()
    {
        RenderSettings.skybox = null;
        ApplyCameraSolidDark();
    }

    void ApplyCameraSkybox()
    {
        var cams = FindObjectsOfType<Camera>();
        for (int i = 0; i < cams.Length; i++)
        {
            var cam = cams[i];
            if (cam == null)
                continue;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
        }
    }

    void ApplyCameraSolidDark()
    {
        var cams = FindObjectsOfType<Camera>();
        for (int i = 0; i < cams.Length; i++)
        {
            var cam = cams[i];
            if (cam == null)
                continue;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.002f, 0.003f, 0.008f);
        }
    }

    /// <summary>런타임 exposure 미세 조정 (Inspector / 디버그용).</summary>
    public void SetExposure(float value)
    {
        exposure = Mathf.Clamp(value, 0.2f, 4f);
        if (skyMat != null && skyMat.HasProperty("_Exposure"))
            skyMat.SetFloat("_Exposure", exposure);
    }
}
