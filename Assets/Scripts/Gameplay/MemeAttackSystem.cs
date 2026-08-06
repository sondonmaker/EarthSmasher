using System.Collections;
using UnityEngine;

/// <summary>밈 무기 — Doge / To The Moon / Pengu + 자율 소환 유닛.</summary>
public class MemeAttackSystem : MonoBehaviour
{
    public static MemeAttackSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;

    public bool IsDogeRunning { get; private set; }
    public bool IsMoonRunBusy { get; private set; }
    public bool IsPenguRunning { get; private set; }
    public bool IsTrumpRunning { get; private set; }

    const int MaxMemeUnits = 4;
    const int MaxCatUnits = 1;
    static float nextHeavyEarthFx;
    static float nextCatMeshFx;

    GameObject dogeGo;
    Coroutine moonRunCo;
    Coroutine penguCo;
    Coroutine trumpCo;

    void Awake()
    {
        Instance = this;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (dogeGo != null)
            Object.Destroy(dogeGo);
        if (moonRunCo != null)
            StopCoroutine(moonRunCo);
        if (penguCo != null)
            StopCoroutine(penguCo);
        if (trumpCo != null)
            StopCoroutine(trumpCo);
    }

    public static MemeAttackSystem Ensure()
    {
        if (Instance != null)
            return Instance;
        var go = new GameObject("MemeAttackSystem");
        return go.AddComponent<MemeAttackSystem>();
    }

    public void Configure(EarthPlanet planet) => earth = planet;

    public bool TryFire(string memeId, Vector3 worldPoint, Vector3 worldNormal)
    {
        if (string.IsNullOrEmpty(memeId))
            return false;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;

        Vector3 localDir = earth.transform.InverseTransformPoint(worldPoint).normalized;

        switch (memeId)
        {
            case "doge":
                if (IsDogeRunning || IsTrumpRunning)
                    return false;
                LaunchDogeFall(localDir);
                return true;
            case "to_the_moon":
                if (IsMoonRunBusy || IsDogeRunning || IsPenguRunning || IsTrumpRunning)
                    return false;
                moonRunCo = StartCoroutine(ToTheMoonRun(localDir));
                return true;
            case "pengu_coin":
                if (IsPenguRunning || IsDogeRunning || IsMoonRunBusy || IsTrumpRunning)
                    return false;
                penguCo = StartCoroutine(LaunchPenguCoin(localDir));
                return true;
            case "trump_tariff":
                if (IsTrumpRunning || IsDogeRunning || IsMoonRunBusy || IsPenguRunning)
                    return false;
                trumpCo = StartCoroutine(LaunchTrumpTariff(localDir));
                return true;
            case "pepe":
                return Summon<MemePepeUnit>(localDir, MemeVisuals.CreatePepe(earth.Radius * 0.32f));
            case "cat":
                if (MemeCatUnit.ActiveCount >= MaxCatUnits)
                    return false;
                return Summon<MemeCatUnit>(localDir, MemeVisuals.CreateCatOrb(earth.Radius * 0.38f));
            case "shark":
                return Summon<MemeSharkUnit>(localDir, MemeVisuals.CreateSneakerShark(earth.Radius * 0.52f));
            case "earth_cow":
                return Summon<MemeCowUnit>(localDir, MemeVisuals.CreateEarthCow(earth.Radius * 0.4f));
            default:
                return false;
        }
    }

    bool Summon<T>(Vector3 localDir, GameObject visual) where T : MemeUnitBase
    {
        if (MemeUnitBase.LiveCount >= MaxMemeUnits)
            return false;

        float R = earth.Radius;
        Vector3 dir = earth.transform.TransformDirection(localDir).normalized;
        Vector3 pos = earth.transform.position + dir * (R * 1.55f);
        visual.transform.position = pos;
        var unit = visual.AddComponent<T>();
        unit.Init(earth, localDir);
        return true;
    }

    /// <summary>배틀쉽급 가벼운 타격 — Dig + Burn만 (용암 메시/크레이터 페인트 없음).</summary>
    public static void LightHit(EarthPlanet earth, Vector3 hit, Vector3 normal, float digR, float digDepth, float burnR, float burnDark)
    {
        if (earth == null)
            return;
        if (Time.time < nextHeavyEarthFx)
        {
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, burnR * 0.85f, burnDark * 0.7f);
            return;
        }
        nextHeavyEarthFx = Time.time + 0.32f;
        EarthCraterDeform.Ensure(earth)?.Dig(hit, digR, digDepth, false);
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, burnR, burnDark);
    }

    /// <summary>고양이 스크래치 — 가벼운 그을음 + 드문 Dig/자국 (ScratchGrooves 없음).</summary>
    public static void CatScratch(EarthPlanet earth, Vector3 hit, Vector3 normal, ref float digR, ref float digD, int swipe, int seed)
    {
        if (earth == null)
            return;

        digR = Mathf.Min(0.11f, digR + 0.0045f);
        digD = Mathf.Min(0.052f, digD + 0.0032f);

        if (swipe % 3 != 0)
        {
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, digR * 0.5f, 0.32f);
            return;
        }

        if (Time.time >= nextCatMeshFx)
        {
            nextCatMeshFx = Time.time + 0.55f;
            EarthCraterDeform.Ensure(earth)?.Dig(hit, digR, digD, false, seed);
        }

        if (swipe % 6 == 0)
            EarthSurfaceScorch.Ensure(earth)?.PaintScratchMarks(hit, normal, 2, 0.07f + digR * 0.25f, seed, 4);
        else
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, digR * 0.65f, 0.4f);
    }

    void LaunchDogeFall(Vector3 localDir)
    {
        IsDogeRunning = true;
        float R = earth.Radius;
        Vector3 n = DirectionWorld(localDir);
        Vector3 hit = PointOnRay(localDir, R);

        dogeGo = MemeVisuals.CreateDoge(R * 1.35f);
        MemeVisuals.AddMeteorFallTrail(dogeGo, R);

        var body = dogeGo.AddComponent<MemeDogeCoinBody>();
        body.Launch(earth, hit, n, 9.5f, 4.5f, 22f, 1.6f, () =>
        {
            MemeCaption.Spawn(hit + n * (R * 0.35f), "wow", new Color(1f, 0.85f, 0.2f), R * 0.22f);
            MemeCaption.Spawn(hit + n * (R * 0.28f), "much crater", new Color(1f, 0.92f, 0.45f), R * 0.16f);
            ApplyCasualtiesStatic(0.008f);
            dogeGo = null;
            IsDogeRunning = false;
        });
    }

    IEnumerator ToTheMoonRun(Vector3 localDir)
    {
        IsMoonRunBusy = true;
        float R = earth.Radius;
        Vector3 n = DirectionWorld(localDir);
        Vector3 hit = PointOnRay(localDir, R);
        Vector3 pad = hit + n * (R * 0.025f);

        const float aspect = 1.45f;
        float startSize = R * 0.045f;
        float preLaunchSize = R * 0.2f;

        var ride = MemeVisuals.CreateElonDogeRide(startSize);
        ride.transform.position = pad;
        SetElonDogeScale(ride.transform, startSize, aspect);

        float buildDur = 0.9f;
        float buildT = 0f;
        while (buildT < buildDur)
        {
            buildT += Time.deltaTime;
            float u = buildT / buildDur;
            float ease = u * u;
            float sz = Mathf.Lerp(startSize, preLaunchSize, ease);
            SetElonDogeScale(ride.transform, sz, aspect);
            ride.transform.position = pad + n * (Mathf.Sin(buildT * 18f) * R * 0.003f * ease);
            yield return null;
        }

        ride.transform.position = pad;
        CinematicExplosion.Play(hit, n, 1.4f);
        ImpactShockwave.Spawn(hit, n, R * 0.72f);
        SpawnFlash(hit, n, R * 0.14f, new Color(1f, 0.58f, 0.1f, 0.75f));
        SpawnFlash(hit + n * (R * 0.06f), n, R * 0.08f, new Color(1f, 0.82f, 0.35f, 0.55f));
        LightHit(earth, hit, n, 0.065f, 0.03f, 0.048f, 0.62f);
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, 0.05f, 0.68f);
        CameraShake.Shake(0.52f, 0.42f);
        MemeCaption.Spawn(hit + n * (R * 0.3f), "BOOM!", new Color(1f, 0.28f, 0.05f), R * 0.22f);
        MemeCaption.Spawn(hit + n * (R * 0.2f), "much wow", new Color(1f, 0.85f, 0.25f), R * 0.13f);

        yield return new WaitForSeconds(0.12f);

        MemeVisuals.AddRainbowTrail(ride, R);

        float launchSize = preLaunchSize;
        float exitSize = R * 0.78f;
        Vector3 flyStart = pad + n * (R * 0.1f);
        Vector3 flyEnd = hit + n * (R * 5.2f);
        ride.transform.position = flyStart;

        float flyDur = 3.1f;
        float flyT = 0f;
        float nextBurn = 0f;
        while (flyT < flyDur)
        {
            flyT += Time.deltaTime;
            float u = flyT / flyDur;
            float lift = u * u;
            float grow = Mathf.SmoothStep(0f, 1f, u);

            ride.transform.position = Vector3.Lerp(flyStart, flyEnd, lift);
            SetElonDogeScale(ride.transform, Mathf.Lerp(launchSize, exitSize, grow), aspect);

            if (Time.time >= nextBurn && u < 0.45f)
            {
                nextBurn = Time.time + 0.14f;
                EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, 0.02f, 0.38f);
            }

            yield return null;
        }

        MemeCaption.Spawn(flyEnd + n * (R * 0.08f), "to the moon", new Color(1f, 0.92f, 0.35f), R * 0.15f);
        ApplyCasualtiesStatic(0.0045f);

        float fade = 0.4f;
        float fadeT = 0f;
        Vector3 fadeScale = ride.transform.localScale;
        while (fadeT < fade)
        {
            fadeT += Time.deltaTime;
            float u = fadeT / fade;
            ride.transform.localScale = fadeScale * (1f - u);
            ride.transform.position += n * (R * 0.35f * Time.deltaTime);
            yield return null;
        }

        Object.Destroy(ride);
        IsMoonRunBusy = false;
        moonRunCo = null;
    }

    static void SetElonDogeScale(Transform t, float size, float aspect)
    {
        t.localScale = new Vector3(size * aspect, size, 1f);
    }

    IEnumerator LaunchPenguCoin(Vector3 localDir)
    {
        IsPenguRunning = true;
        float R = earth.Radius;
        Vector3 n = DirectionWorld(localDir);
        Vector3 hit = PointOnRay(localDir, R);

        Vector3 tangent = Vector3.Cross(n, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(n, Vector3.right);
        tangent.Normalize();

        const int coinCount = 6;
        for (int i = 0; i < coinCount; i++)
        {
            Vector3 jitter = (tangent * Random.Range(-0.12f, 0.12f) + Vector3.Cross(n, tangent) * Random.Range(-0.08f, 0.08f)) * R;
            Vector3 aimHit = hit + jitter;
            Vector3 aimN = (aimHit - earth.transform.position).normalized;
            Vector3 impact = earth.transform.position + aimN * R;

            var coin = MemeVisuals.CreatePenguCoin(R * 0.32f);
            MemeVisuals.AddIceTrail(coin, R * 0.025f);
            var drop = coin.AddComponent<MemeSmallCoinDrop>();
            drop.Launch(earth, impact, aimN, 11f + i * 0.5f, 2.2f + i * 0.15f, true, null);
            yield return new WaitForSeconds(0.17f);
        }

        yield return new WaitForSeconds(0.25f);

        var hero = MemeVisuals.CreatePenguHero(R * 0.62f);
        Vector3 heroScale = hero.transform.localScale;
        Vector3 slideFrom = hit + n * (R * 1.8f) + tangent * (R * 0.35f);
        Vector3 slideTo = hit + n * (R * 0.12f);
        float slideDur = 0.75f;
        float slideT = 0f;
        while (slideT < slideDur)
        {
            slideT += Time.deltaTime;
            float u = slideT / slideDur;
            float ease = u * u * (3f - 2f * u);
            hero.transform.position = Vector3.Lerp(slideFrom, slideTo, ease);
            hero.transform.localScale = heroScale * (1f + Mathf.Sin(u * Mathf.PI) * 0.08f);
            yield return null;
        }

        LightHit(earth, hit, n, 0.055f, 0.022f, 0.03f, 0.42f);
        CameraShake.Shake(0.14f, 0.11f);
        MemeCaption.Spawn(hit + n * (R * 0.18f), Random.value > 0.5f ? "pengu" : "waddle", new Color(0.75f, 0.92f, 1f), R * 0.11f);
        ApplyCasualtiesStatic(0.0038f);

        float exit = 0.45f;
        float exitT = 0f;
        Vector3 exitScale = hero.transform.localScale;
        while (exitT < exit)
        {
            exitT += Time.deltaTime;
            float u = exitT / exit;
            hero.transform.localScale = exitScale * (1f - u * u);
            hero.transform.position += n * (R * 0.25f * Time.deltaTime);
            yield return null;
        }

        Object.Destroy(hero);
        IsPenguRunning = false;
        penguCo = null;
    }

    IEnumerator LaunchTrumpTariff(Vector3 localDir)
    {
        IsTrumpRunning = true;
        float R = earth.Radius;
        Vector3 n = DirectionWorld(localDir);
        Vector3 hit = PointOnRay(localDir, R);
        Vector3 center = earth.transform.position;
        Vector3 oppN = DirectionWorld(-localDir);
        Vector3 oppHit = PointOnRay(-localDir, R);

        const float trumpAltMul = 0.22f;
        var orbitCam = Object.FindObjectOfType<OrbitCamera>();
        if (orbitCam != null)
        {
            orbitCam.FrameMemeBillboard(n, trumpAltMul, 0.95f);
            yield return new WaitForSeconds(0.95f);
        }

        var trump = MemeVisuals.CreateTrumpBillboard(R * 0.82f);
        Vector3 trumpPos = hit + n * (R * trumpAltMul);
        trump.transform.position = trumpPos;

        MemeCaption.Spawn(hit + n * (R * 0.28f), "TARIFFS", new Color(1f, 0.72f, 0.15f), R * 0.14f);
        yield return new WaitForSeconds(0.35f);

        Vector3 tangent = Vector3.Cross(n, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(n, Vector3.right);
        tangent.Normalize();

        for (int i = 0; i < 8; i++)
        {
            Vector3 jitter = (tangent * Random.Range(-0.15f, 0.15f) + Vector3.Cross(n, tangent) * Random.Range(-0.1f, 0.1f)) * R;
            Vector3 aimHit = hit + jitter;
            Vector3 aimN = (aimHit - center).normalized;
            Vector3 impact = center + aimN * R;

            var coin = MemeVisuals.CreateTariffCoin(R * 0.28f);
            MemeVisuals.AddFireTrail(coin, R * 0.022f);
            var drop = coin.AddComponent<MemeSmallCoinDrop>();
            drop.Launch(earth, impact, aimN, 12f + i * 0.4f, 2f + i * 0.12f, false, null);
            yield return new WaitForSeconds(0.14f);
        }

        MemeCaption.Spawn(hit + n * (R * 0.22f), "TRADE WAR", new Color(1f, 0.35f, 0.12f), R * 0.13f);
        yield return new WaitForSeconds(0.25f);

        var scorch = EarthSurfaceScorch.Ensure(earth);
        const int firePoints = 28;
        for (int i = 0; i < firePoints; i++)
        {
            float u = i / (float)(firePoints - 1);
            Vector3 dir = GlobalFireDirection(u, localDir);
            Vector3 worldBurn = center + earth.transform.TransformDirection(dir) * R;
            Vector3 burnN = dir;
            float radius = Mathf.Lerp(0.042f, 0.075f, u);
            float dark = Mathf.Lerp(0.58f, 0.92f, u);
            scorch?.BurnAt(worldBurn, radius, dark);
            SpawnFlash(worldBurn, earth.transform.TransformDirection(burnN), R * 0.07f, new Color(1f, 0.42f, 0.08f, 0.55f));

            if (i % 3 == 0)
                earth.ApplyImpact(worldBurn, 6f + u * 8f);
            if (i % 5 == 0)
                CameraShake.Shake(0.1f + u * 0.08f, 0.07f + u * 0.05f);

            trump.transform.position = Vector3.Lerp(trump.transform.position, hit + n * (R * 0.16f), 0.08f);

            if (i == firePoints / 2)
                MemeCaption.Spawn(oppHit + oppN * (R * 0.18f), "SANCTIONS", new Color(1f, 0.5f, 0.15f), R * 0.11f);

            yield return new WaitForSeconds(0.065f);
        }

        CinematicExplosion.Play(hit, n, 1.55f);
        CinematicExplosion.Play(oppHit, oppN, 1.15f);
        ImpactShockwave.Spawn(hit, n, R * 1.15f);
        ImpactShockwave.Spawn(oppHit, oppN, R * 0.9f);
        scorch?.PaintMoltenFissures(hit, 0.11f, 0.72f, 2.6f, 11);
        scorch?.PaintMoltenFissures(oppHit, 0.09f, 0.68f, 2.3f, 9);
        LightHit(earth, hit, n, 0.095f, 0.042f, 0.055f, 0.88f);
        earth.ApplyImpact(hit, 18f);
        CameraShake.Shake(0.85f, 0.62f);

        yield return CameraRollPulse(0.5f, 110f);

        MemeCaption.Spawn(hit + n * (R * 0.32f), "MAKE EARTH GREAT AGAIN", new Color(1f, 0.88f, 0.25f), R * 0.17f);
        MemeCaption.Spawn(oppHit + oppN * (R * 0.2f), "TOTAL CHAOS", new Color(1f, 0.4f, 0.1f), R * 0.12f);
        ApplyCasualtiesStatic(0.0065f);

        float fade = 0.4f;
        float fadeT = 0f;
        Vector3 trumpScale = trump.transform.localScale;
        while (fadeT < fade)
        {
            fadeT += Time.deltaTime;
            float u = fadeT / fade;
            trump.transform.localScale = trumpScale * (1f - u);
            yield return null;
        }

        Object.Destroy(trump);
        IsTrumpRunning = false;
        trumpCo = null;
    }

    static Vector3 GlobalFireDirection(float u, Vector3 primaryLocal)
    {
        float phi = u * Mathf.PI * 2f * 2.4f;
        float theta = Mathf.PI * (0.12f + 0.76f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2.5f)));
        Vector3 dir = new Vector3(
            Mathf.Sin(theta) * Mathf.Cos(phi),
            Mathf.Cos(theta),
            Mathf.Sin(theta) * Mathf.Sin(phi));
        return Vector3.Slerp(primaryLocal.normalized, dir.normalized, 0.35f).normalized;
    }

    static IEnumerator CameraRollPulse(float duration, float degrees)
    {
        var cam = Camera.main;
        if (cam == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cam.transform.Rotate(Vector3.forward, degrees * Time.deltaTime / duration, Space.Self);
            yield return null;
        }
    }

    Vector3 DirectionWorld(Vector3 localDir) =>
        earth.transform.TransformDirection(localDir).normalized;

    Vector3 PointOnRay(Vector3 localDir, float distFromCenter) =>
        earth.transform.position + DirectionWorld(localDir) * distFromCenter;

    public static void ApplyCasualtiesStatic(float frac)
    {
        var pop = PopulationSystem.Instance;
        if (pop == null)
            return;
        long now = pop.Population;
        long deaths = (long)Mathf.Floor(now * frac);
        if (deaths > 0)
            pop.ApplyCasualties(deaths);
    }

    public static void SpawnFlash(Vector3 point, Vector3 normal, float radius, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MemeFlash";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = point + normal * 0.04f;
        go.transform.localScale = Vector3.one * radius;
        var mat = RuntimeMaterial.UnlitTransparent(col);
        go.GetComponent<Renderer>().material = mat;
        go.AddComponent<MoonFlashBurst>().Init(mat, radius * 0.2f, radius * 1.2f, 0.45f);
    }

    public static void SpawnKickDust(Vector3 point, Vector3 normal, float size, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "KickDust";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = point + normal * Random.Range(0.02f, 0.12f) * size
                + Random.insideUnitSphere * size * 0.4f;
            go.transform.localScale = Vector3.one * Random.Range(size * 0.15f, size * 0.45f);
            var mat = RuntimeMaterial.UnlitTransparent(new Color(0.3f, 0.55f, 0.95f, 0.35f));
            go.GetComponent<Renderer>().material = mat;
            go.AddComponent<QuakeDustFade>().Init(mat, Random.Range(0.3f, 0.6f), size * 1.5f);
        }
    }

    public static void SpawnMilkSplash(Vector3 point, Vector3 normal, float size, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "MilkSplash";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = point + normal * Random.Range(0.05f, 0.2f) * size
                + Random.insideUnitSphere * size * 0.5f;
            go.transform.localScale = Vector3.one * Random.Range(size * 0.2f, size * 0.55f);
            var mat = RuntimeMaterial.UnlitTransparent(new Color(0.98f, 0.98f, 0.95f, 0.5f));
            go.GetComponent<Renderer>().material = mat;
            go.AddComponent<QuakeDustFade>().Init(mat, Random.Range(0.4f, 0.75f), size * 1.8f);
        }
    }
}

/// <summary>밈 텍스트 — wow / bonk / moo 등.</summary>
public class MemeCaption : MonoBehaviour
{
    TextMesh textMesh;
    float life = 1.2f;
    float t;
    Vector3 drift;
    float startSize;

    public static void Spawn(Vector3 worldPos, string text, Color color, float scale)
    {
        var go = new GameObject("MemeCaption");
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = 0.12f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold;

        var cap = go.AddComponent<MemeCaption>();
        cap.textMesh = tm;
        cap.startSize = scale;
        cap.drift = Random.insideUnitSphere * scale * 0.15f;
        cap.drift.y = Mathf.Abs(cap.drift.y);
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / life);
        transform.position += drift * Time.deltaTime * 0.35f;
        transform.localScale = Vector3.one * startSize * (1f + u * 0.25f);
        if (textMesh != null)
        {
            Color c = textMesh.color;
            c.a = 1f - u;
            textMesh.color = c;
        }
        if (u >= 1f)
            Destroy(gameObject);
    }
}
