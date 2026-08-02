using UnityEngine;

/// <summary>
/// 빈 씬에서 Play만 눌러도 지구+운석 낙하 프로토타입이 돌아가도록 런타임 세팅.
/// Hierarchy에 빈 오브젝트를 만들고 이 컴포넌트만 붙이면 된다.
/// </summary>
public class MeteorImpactBootstrap : MonoBehaviour
{
    [Header("Optional overrides (비우면 Resources/Earth 자동 로드)")]
    [SerializeField] Texture2D earthDayTexture;
    [SerializeField] Texture2D earthNightTexture;
    [SerializeField] bool createHud = true;
    [SerializeField] bool enableFracture = true;

    void Awake()
    {
        SetupLighting();
        EarthPlanet earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            earth = CreateEarth();

        Camera cam = Camera.main;
        if (cam == null)
            cam = CreateCamera();

        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = Color.black;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
        if (FindObjectOfType<SpaceBackdrop>() == null)
        {
            var space = new GameObject("SpaceBackdrop");
            space.AddComponent<SpaceBackdrop>();
        }

        var orbit = cam.GetComponent<OrbitCamera>();
        if (orbit == null) orbit = cam.gameObject.AddComponent<OrbitCamera>();
        orbit.SetTarget(earth.transform);
        // 달에서 본 지구처럼 — 조금 멀리, 텔레포토 느낌
        orbit.FramePlanet(earth.Radius, 0.58f);
        if (cam.orthographic == false)
            cam.fieldOfView = 42f;

        if (FindObjectOfType<ZoomControls>() == null)
        {
            var zoomGo = new GameObject("ZoomControls");
            zoomGo.AddComponent<ZoomControls>().Bind(orbit);
        }

        ImpactHud hud = FindObjectOfType<ImpactHud>();
        if (hud == null && createHud)
            hud = CreateHud();

        var launcher = FindObjectOfType<MeteorLauncher>();
        if (launcher == null)
        {
            var go = new GameObject("MeteorLauncher");
            launcher = go.AddComponent<MeteorLauncher>();
        }
        launcher.Configure(cam, earth, hud);

        var big = FindObjectOfType<BigMeteorStrike>();
        if (big == null)
        {
            var bigGo = new GameObject("BigMeteorStrike");
            big = bigGo.AddComponent<BigMeteorStrike>();
        }
        big.Configure(cam, earth);

        if (earth.GetComponent<EarthDestructionVisual>() == null)
            earth.gameObject.AddComponent<EarthDestructionVisual>();

        if (enableFracture && earth.GetComponent<EarthFractureSystem>() == null)
            earth.gameObject.AddComponent<EarthFractureSystem>();

        EnsureWorldSystems(earth);
    }

    static void EnsureWorldSystems(EarthPlanet earth)
    {
        if (FindObjectOfType<PopulationSystem>() == null)
            new GameObject("PopulationSystem").AddComponent<PopulationSystem>();

        if (FindObjectOfType<WorldStatusHud>() == null)
            new GameObject("WorldStatusHud").AddComponent<WorldStatusHud>();

        var war = FindObjectOfType<NuclearWarSystem>();
        if (war == null)
            war = new GameObject("NuclearWarSystem").AddComponent<NuclearWarSystem>();
        war.Configure(earth);

        var quake = FindObjectOfType<EarthquakeSystem>();
        if (quake == null)
            quake = new GameObject("EarthquakeSystem").AddComponent<EarthquakeSystem>();
        quake.Configure(earth);

        var moon = FindObjectOfType<MoonImpactSystem>();
        if (moon == null)
            moon = new GameObject("MoonImpactSystem").AddComponent<MoonImpactSystem>();
        moon.Configure(earth);

        if (FindObjectOfType<WeaponRailPanel>() == null)
            new GameObject("WeaponRailPanel").AddComponent<WeaponRailPanel>();

        if (FindObjectOfType<EarthControlPanel>() == null)
        {
            var body = earth.GetComponent<EarthBodyData>();
            if (body == null)
                body = earth.gameObject.AddComponent<EarthBodyData>();
            var layers = earth.GetComponent<EarthLayerController>();
            var panelGo = new GameObject("EarthControlPanel");
            panelGo.AddComponent<EarthControlPanel>().Bind(body, layers);
        }
    }

    void SetupLighting()
    {
        var sun = FindObjectOfType<Light>();
        if (sun == null)
        {
            var sunGo = new GameObject("Sun");
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        // 우주: 강한 단일 태양광 + 거의 없는 앰비언트 (= 달에서 본 지구)
        sun.color = new Color(1f, 0.98f, 0.94f);
        sun.intensity = 2.1f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.transform.rotation = Quaternion.Euler(32f, -52f, 0f);

        var fillGo = GameObject.Find("FillLight");
        if (fillGo != null)
        {
            var fill = fillGo.GetComponent<Light>();
            if (fill != null)
                fill.intensity = 0.04f; // 거의 끄기 — 씻김 방지
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.012f, 0.014f, 0.022f);
        RenderSettings.reflectionIntensity = 0.15f;
        RenderSettings.fog = false;
    }

    EarthPlanet CreateEarth()
    {
        var earthGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        earthGo.name = "Earth";
        earthGo.transform.position = Vector3.zero;
        earthGo.transform.localScale = Vector3.one * 5f;

        var rend = earthGo.GetComponent<Renderer>();
        rend.material = EarthTextureLoader.CreateCrustMaterial(earthDayTexture, earthNightTexture);
        rend.receiveShadows = true;

        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(earthGo.transform, false);
        core.transform.localScale = Vector3.one * 0.42f;
        Destroy(core.GetComponent<Collider>());
        core.GetComponent<Renderer>().material = EarthTextureLoader.CreateCoreMaterial();
        core.SetActive(false);

        // day 맵에 바다 포함 — 반투명 Ocean 레이어는 끔 (비침 방지)
        var ocean = CreateLayerSphere("Ocean", earthGo.transform, 1.006f, EarthTextureLoader.CreateOceanMaterial(), false);
        ocean.SetActive(false);

        var clouds = CreateLayerSphere("Clouds", earthGo.transform, 1.028f, EarthTextureLoader.CreateCloudMaterial(), true);
        clouds.AddComponent<EarthSpin>().SetSpeed(3.2f);

        var atmosphere = CreateLayerSphere("Atmosphere", earthGo.transform, 1.05f, EarthTextureLoader.CreateAtmosphereMaterial(), false);
        var halo = CreateLayerSphere("AtmosphereHalo", atmosphere.transform, 1.035f, EarthTextureLoader.CreateAtmosphereHaloMaterial(), false);

        var aurora = CreateLayerSphere("Aurora", earthGo.transform, 1.04f, EarthTextureLoader.CreateAuroraMaterial(), false);

        var planet = earthGo.AddComponent<EarthPlanet>();
        planet.SetVisualRefs(rend, core.transform);
        earthGo.AddComponent<EarthSpin>().SetSpeed(7.5f);

        // 메시 UV는 Unity 기본 유지 (시작 시 교체하면 텍스처가 깨짐)
        // 크레이터 변형은 충돌 시에만 기존 메시를 복제해서 사용

        var body = earthGo.AddComponent<EarthBodyData>();

        var layers = earthGo.AddComponent<EarthLayerController>();
        layers.Bind(earthGo, ocean, clouds, atmosphere, aurora);

        var panelGo = new GameObject("EarthControlPanel");
        var panel = panelGo.AddComponent<EarthControlPanel>();
        panel.Bind(body, layers);

        return planet;
    }

    static GameObject CreateLayerSphere(string name, Transform parent, float localScale, Material mat, bool castShadows)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * localScale;
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.material = mat;
        r.shadowCastingMode = castShadows
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    Camera CreateCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 400f;
        go.AddComponent<AudioListener>();
        return cam;
    }

    ImpactHud CreateHud()
    {
        // Unity 6: UI.Text 기본 폰트(Arial)가 예외를 내므로 OnGUI HUD만 사용
        var go = new GameObject("ImpactHUD");
        return go.AddComponent<ImpactHud>();
    }
}
