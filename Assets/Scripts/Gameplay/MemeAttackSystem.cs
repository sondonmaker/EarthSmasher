using System.Collections;
using UnityEngine;

/// <summary>밈 무기 — Doge / To The Moon / Pengu + 자율 소환 유닛.</summary>
public class MemeAttackSystem : MonoBehaviour
{
    public static MemeAttackSystem Instance { get; private set; }

    [SerializeField] EarthPlanet earth;

    public bool IsDogeRunning { get; private set; }
    public int ActiveMoonRunCount { get; private set; }
    public bool IsMoonRunBusy => ActiveMoonRunCount >= MaxMoonRuns;
    public bool IsPenguRunning { get; private set; }
    public bool IsTrumpRunning { get; private set; }
    public bool IsTrumpCrashRunning { get; private set; }
    public bool IsTrojanRunning { get; private set; }

    bool IsAnyTrumpBusy => IsTrumpRunning || IsTrumpCrashRunning;

    const int MaxMemeUnits = 4;
    const int MaxCatUnits = 1;
    const int MaxMoonRuns = 4;
    static float nextHeavyEarthFx;
    static float nextCatMeshFx;

    GameObject dogeGo;
    Coroutine penguCo;
    Coroutine trumpCo;
    Coroutine trumpCrashCo;
    Coroutine trojanCo;

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
        if (penguCo != null)
            StopCoroutine(penguCo);
        if (trumpCo != null)
            StopCoroutine(trumpCo);
        if (trumpCrashCo != null)
            StopCoroutine(trumpCrashCo);
        if (trojanCo != null)
            StopCoroutine(trojanCo);
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
                if (IsDogeRunning || IsAnyTrumpBusy || IsTrojanRunning)
                    return false;
                LaunchDogeFall(localDir);
                return true;
            case "to_the_moon":
                if (IsMoonRunBusy)
                    return false;
                StartCoroutine(ToTheMoonRun(localDir));
                return true;
            case "pengu_coin":
                if (IsPenguRunning || IsDogeRunning || IsMoonRunBusy || IsAnyTrumpBusy || IsTrojanRunning)
                    return false;
                penguCo = StartCoroutine(LaunchPenguCoin(localDir));
                return true;
            case "trump_tariff":
                if (IsAnyTrumpBusy || IsDogeRunning || IsMoonRunBusy || IsPenguRunning || IsTrojanRunning)
                    return false;
                trumpCo = StartCoroutine(LaunchTrumpTariff(localDir));
                return true;
            case "trump_market_crash":
                if (IsAnyTrumpBusy || IsDogeRunning || IsMoonRunBusy || IsPenguRunning || IsTrojanRunning)
                    return false;
                trumpCrashCo = StartCoroutine(LaunchTrumpMarketCrash(localDir));
                return true;
            case "trojan_horse":
                if (IsTrojanRunning || IsAnyTrumpBusy || IsDogeRunning || IsPenguRunning)
                    return false;
                trojanCo = StartCoroutine(LaunchTrojanHorse(localDir));
                return true;
            case "pepe":
                return Summon<MemePepeUnit>(localDir, MemeVisuals.CreatePepe(earth.Radius * 0.32f));
            case "cat":
                if (MemeCatUnit.ActiveCount >= MaxCatUnits)
                    return false;
                return Summon<MemeCatUnit>(localDir, MemeVisuals.CreateCatOrb(earth.Radius * 0.55f));
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

    /// <summary>거대 고양이 발톱 — 평행 홈 파기 + 텍스처 긁힘.</summary>
    public static void CatScratch(
        EarthPlanet earth, Vector3 hit, Vector3 normal, Vector3 scratchDir,
        ref float digR, ref float digD, int swipe, int seed)
    {
        if (earth == null)
            return;

        digR = Mathf.Min(0.2f, digR + 0.007f);
        digD = Mathf.Min(0.085f, digD + 0.0045f);

        scratchDir = Vector3.ProjectOnPlane(scratchDir, normal).normalized;
        if (scratchDir.sqrMagnitude < 1e-4f)
            return;

        float grooveLen = Mathf.Clamp(0.14f + digR * 0.65f, 0.12f, 0.24f);
        float grooveSpread = Mathf.Clamp(0.022f + digR * 0.35f, 0.018f, 0.048f);
        float grooveDepth = Mathf.Clamp(0.028f + digD * 0.85f, 0.025f, 0.075f);

        if (Time.time >= nextCatMeshFx)
        {
            nextCatMeshFx = Time.time + 0.1f;
            EarthCraterDeform.Ensure(earth)?.ScratchGroovesParallel(
                hit, normal, scratchDir, 4, grooveLen, grooveSpread, grooveDepth, seed, 16);
        }

        var scorch = EarthSurfaceScorch.Ensure(earth);
        scorch?.PaintParallelScratches(hit, normal, scratchDir, 4, grooveLen, grooveSpread, seed, 18);
        scorch?.BurnAt(hit, digR * 0.28f, 0.52f);

        LightHit(earth, hit, normal, digR * 0.42f, digD * 0.55f, digR * 0.3f, 0.58f);
        CameraShake.Shake(0.09f + digR * 0.15f, 0.07f);
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
            MemeCaption.Spawn(hit + n * (R * 0.32f), "much wow", new Color(1f, 0.88f, 0.32f), R * 0.18f);
            ApplyCasualtiesStatic(0.008f);
            dogeGo = null;
            IsDogeRunning = false;
        });
    }

    IEnumerator ToTheMoonRun(Vector3 localDir)
    {
        ActiveMoonRunCount++;
        float R = earth.Radius;
        Vector3 n = DirectionWorld(localDir);
        Vector3 hit = PointOnRay(localDir, R);
        Vector3 pad = hit + n * (R * 0.025f);

        const float aspect = 1.45f;
        float startSize = R * 0.07f;
        float preLaunchSize = R * 0.32f;

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
        MemeCaption.Spawn(hit + n * (R * 0.34f), "BOOM!", new Color(1f, 0.28f, 0.05f), R * 0.2f);

        yield return new WaitForSeconds(0.12f);

        MemeVisuals.AddRainbowTrail(ride, R);

        float launchSize = preLaunchSize;
        float exitSize = R * 1.05f;
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
        ActiveMoonRunCount = Mathf.Max(0, ActiveMoonRunCount - 1);
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

        const float trumpAltMul = 0.26f;
        const float focusDur = 0.7f;
        var orbitCam = Object.FindObjectOfType<OrbitCamera>();
        if (orbitCam != null)
        {
            orbitCam.FocusOnSurfaceHit(hit, trumpAltMul, focusDur);
            yield return new WaitForSeconds(focusDur);
        }

        var trump = MemeVisuals.CreateTrumpBillboard(R * 1.05f);
        Vector3 trumpPos = hit + n * (R * trumpAltMul);
        trump.transform.position = trumpPos;

        MemeCaption.Spawn(hit + n * (R * 0.28f), "TARIFFS", new Color(1f, 0.72f, 0.15f), R * 0.14f);
        yield return new WaitForSeconds(0.4f);

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
            yield return new WaitForSeconds(0.16f);
        }

        MemeCaption.Spawn(hit + n * (R * 0.22f), "TRADE WAR", new Color(1f, 0.35f, 0.12f), R * 0.13f);
        yield return new WaitForSeconds(0.3f);

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

            if (i == 5 || i == 10 || i == 15 || i == 20 || i == 25)
            {
                int slot = i / 5 - 1;
                Vector3 blastLocal = GlobalTariffBlastLocalDir(slot, localDir);
                Vector3 blastN = DirectionWorld(blastLocal);
                Vector3 blastHit = center + blastN * R;
                PlayTariffWorldBlast(scorch, blastHit, blastN, R, 0.92f + slot * 0.1f);
            }

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

        yield return CameraRollPulse(0.38f, 72f);

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

    static readonly string[] MarketTickers =
    {
        "-8%", "-15%", "-34%", "SELL", "AAPL", "MSFT", "DOW", "REKT", "BEAR", "CRASH", "BYE", "NGMI"
    };

    IEnumerator LaunchTrumpMarketCrash(Vector3 localDir)
    {
        IsTrumpCrashRunning = true;
        float R = earth.Radius;
        Vector3 n = DirectionWorld(localDir);
        Vector3 hit = PointOnRay(localDir, R);
        Vector3 center = earth.transform.position;
        var scorch = EarthSurfaceScorch.Ensure(earth);

        Vector3 tangent = Vector3.Cross(n, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(n, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(n, tangent).normalized;

        const float trumpAltMul = 0.26f;
        const float focusDur = 0.65f;
        var orbitCam = Object.FindObjectOfType<OrbitCamera>();
        if (orbitCam != null)
        {
            orbitCam.FocusOnSurfaceHit(hit, trumpAltMul, focusDur);
            yield return new WaitForSeconds(focusDur);
        }

        var trump = MemeVisuals.CreateTrumpMarketCrashBillboard(R * 1.05f);
        Vector3 trumpPos = hit + n * (R * trumpAltMul);
        trump.transform.position = trumpPos;

        MemeCaption.Spawn(hit + n * (R * 0.3f), "BREAKING NEWS", new Color(1f, 0.2f, 0.12f), R * 0.13f);
        ImpactShockwave.Spawn(trumpPos, n, R * 0.42f);
        CameraShake.Shake(0.16f, 0.12f);
        yield return new WaitForSeconds(0.18f);
        ImpactShockwave.Spawn(trumpPos, n, R * 0.58f);
        yield return new WaitForSeconds(0.12f);

        for (int i = 0; i < 11; i++)
        {
            float fan = (i - 5f) * R * 0.048f;
            Vector3 pos = trumpPos + tangent * fan + bitangent * Random.Range(-0.07f, 0.07f) * R;
            string label = MarketTickers[i % MarketTickers.Length];
            Color col = label.StartsWith("-")
                ? new Color(1f, 0.12f, 0.08f)
                : new Color(0.15f, 1f, 0.32f);
            MemeCaption.SpawnTicker(pos + n * (R * 0.04f), label, col, R * 0.052f, n);
            yield return new WaitForSeconds(0.032f);
        }

        MemeCaption.Spawn(hit + n * (R * 0.26f), "STONKS CRASH", new Color(0.2f, 1f, 0.35f), R * 0.14f);
        yield return new WaitForSeconds(0.22f);

        yield return PaintHeatMapGrid(scorch, hit, center, tangent, bitangent, R);

        for (int i = 0; i < 6; i++)
        {
            float gx = Random.Range(-0.27f, 0.27f) * R;
            float gy = Random.Range(-0.21f, 0.21f) * R;
            Vector3 impact = LocalPatchPoint(hit, center, tangent, bitangent, gx, gy, R);
            Vector3 impactN = LocalPatchNormal(hit, center, tangent, bitangent, gx, gy);

            var tile = MemeVisuals.CreateRedStockTile(R * 0.15f);
            MemeVisuals.AddRedStockTrail(tile, R * 0.013f);
            var drop = tile.AddComponent<MemeSmallCoinDrop>();
            drop.Launch(earth, impact, impactN, 15f + i * 0.5f, 2.8f + i * 0.07f, true, null);
            yield return new WaitForSeconds(0.07f);
        }

        yield return new WaitForSeconds(0.15f);
        yield return CrashArrowSlam(scorch, hit, center, tangent, bitangent, R);
        yield return PanicRippleRings(scorch, hit, center, n, tangent, bitangent, R);

        MemeCaption.Spawn(hit + n * (R * 0.28f), "CIRCUIT BREAKER", new Color(1f, 0.85f, 0.15f), R * 0.15f);
        ImpactShockwave.Spawn(hit, n, R * 1.0f);
        CinematicExplosion.Play(hit, n, 1.3f);
        SpawnFlash(hit, n, R * 0.15f, new Color(1f, 0.08f, 0.06f, 0.75f));
        LightHit(earth, hit, n, 0.075f, 0.034f, 0.048f, 0.82f);
        earth.ApplyImpact(hit, 15f);
        SpawnRedFloatTiles(hit, center, n, tangent, bitangent, R, 14);
        yield return MarketTickerShake(0.48f, 0.09f);

        MemeCaption.Spawn(hit + n * (R * 0.22f), "TRADING HALTED", new Color(1f, 0.15f, 0.08f), R * 0.12f);
        ApplyCasualtiesStatic(0.0065f);

        float vibrateDur = 0.22f;
        float vibrateT = 0f;
        Vector3 trumpBase = trump.transform.position;
        while (vibrateT < vibrateDur)
        {
            vibrateT += Time.deltaTime;
            trump.transform.position = trumpBase
                + tangent * (Mathf.Sin(vibrateT * 80f) * R * 0.003f)
                + bitangent * (Mathf.Sin(vibrateT * 105f) * R * 0.0025f);
            yield return null;
        }

        float fade = 0.32f;
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
        IsTrumpCrashRunning = false;
        trumpCrashCo = null;
    }

    static Vector3 LocalPatchPoint(
        Vector3 hit, Vector3 center, Vector3 tangent, Vector3 bitangent, float uTan, float uBit, float R)
    {
        Vector3 raw = hit + tangent * uTan + bitangent * uBit;
        Vector3 dir = (raw - center).normalized;
        return center + dir * R;
    }

    static Vector3 LocalPatchNormal(
        Vector3 hit, Vector3 center, Vector3 tangent, Vector3 bitangent, float uTan, float uBit)
    {
        Vector3 raw = hit + tangent * uTan + bitangent * uBit;
        return (raw - center).normalized;
    }

    IEnumerator PaintHeatMapGrid(
        EarthSurfaceScorch scorch, Vector3 hit, Vector3 center,
        Vector3 tangent, Vector3 bitangent, float R)
    {
        const int cols = 8;
        const int rows = 6;
        float patch = R * 0.35f;
        float cell = patch / cols;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float uTan = (col - cols * 0.5f + 0.5f) * cell;
                float uBit = (row - rows * 0.5f + 0.5f) * cell;
                Vector3 pt = LocalPatchPoint(hit, center, tangent, bitangent, uTan, uBit, R);
                Vector3 ptN = LocalPatchNormal(hit, center, tangent, bitangent, uTan, uBit);

                scorch?.BurnAt(pt, Random.Range(0.022f, 0.032f), Random.Range(0.74f, 0.92f));
                if ((row + col) % 2 == 0)
                    SpawnFlash(pt, ptN, R * 0.024f, new Color(1f, 0.06f, 0.05f, 0.4f));

                yield return new WaitForSeconds(0.015f);
            }
        }
    }

    IEnumerator CrashArrowSlam(
        EarthSurfaceScorch scorch, Vector3 hit, Vector3 center,
        Vector3 tangent, Vector3 bitangent, float R)
    {
        const int steps = 18;
        var points = new Vector3[steps];
        var normals = new Vector3[steps];

        for (int i = 0; i < steps; i++)
        {
            float u = i / (float)(steps - 1);
            float zig = (i % 2 == 0 ? 1f : -1f) * (1f - u * 0.55f);
            float uTan = R * (0.3f - u * 0.58f);
            float uBit = R * (-0.18f + u * 0.42f + zig * 0.045f);
            points[i] = LocalPatchPoint(hit, center, tangent, bitangent, uTan, uBit, R);
            normals[i] = LocalPatchNormal(hit, center, tangent, bitangent, uTan, uBit);
            points[i] += normals[i] * (R * 0.045f);
        }

        var arrowGo = new GameObject("CrashArrow");
        var line = arrowGo.AddComponent<LineRenderer>();
        line.loop = false;
        line.useWorldSpace = true;
        line.widthMultiplier = R * 0.014f;
        line.material = RuntimeMaterial.UnlitTransparent(new Color(0.1f, 1f, 0.28f, 0.92f));
        line.startColor = new Color(0.15f, 1f, 0.35f, 0.95f);
        line.endColor = new Color(0.05f, 0.75f, 0.15f, 0.55f);
        line.positionCount = 0;

        for (int i = 0; i < steps; i++)
        {
            line.positionCount = i + 1;
            line.SetPosition(i, points[i]);
            Vector3 surf = points[i] - normals[i] * (R * 0.045f);
            SpawnFlash(surf, normals[i], R * Mathf.Lerp(0.05f, 0.028f, i / (float)(steps - 1)),
                new Color(0.1f, 1f, 0.28f, 0.72f));
            scorch?.BurnAt(surf, 0.03f, 0.8f + i * 0.005f);
            if (i == steps - 1 || i == steps / 2)
                CameraShake.Shake(0.06f + i * 0.003f, 0.05f);

            if (i == steps / 2)
                MemeCaption.Spawn(surf + normals[i] * (R * 0.1f), "SELL!", new Color(0.15f, 1f, 0.3f), R * 0.085f);

            yield return new WaitForSeconds(0.03f);
        }

        Vector3 tipSurf = points[steps - 1] - normals[steps - 1] * (R * 0.045f);
        Vector3 tipN = normals[steps - 1];
        CinematicExplosion.Play(tipSurf, tipN, 1.35f);
        ImpactShockwave.Spawn(tipSurf, tipN, R * 0.78f);
        SpawnFlash(tipSurf, tipN, R * 0.13f, new Color(0.12f, 1f, 0.25f, 0.82f));
        scorch?.PaintMoltenFissures(tipSurf, 0.085f, 0.69f, 2.1f, 7);
        LightHit(earth, tipSurf, tipN, 0.065f, 0.03f, 0.044f, 0.84f);
        earth.ApplyImpact(tipSurf, 18f);
        CameraShake.Shake(0.48f, 0.34f);
        MemeCaption.Spawn(tipSurf + tipN * (R * 0.12f), "-99%", new Color(1f, 0.1f, 0.08f), R * 0.135f);

        yield return new WaitForSeconds(0.18f);
        Object.Destroy(arrowGo);
    }

    IEnumerator PanicRippleRings(
        EarthSurfaceScorch scorch, Vector3 hit, Vector3 center, Vector3 n,
        Vector3 tangent, Vector3 bitangent, float R)
    {
        for (int ring = 1; ring <= 4; ring++)
        {
            float ringR = R * (0.075f + ring * 0.055f);
            int segments = 6 + ring * 2;
            for (int s = 0; s < segments; s++)
            {
                float ang = s / (float)segments * Mathf.PI * 2f + ring * 0.4f;
                float uTan = Mathf.Cos(ang) * ringR;
                float uBit = Mathf.Sin(ang) * ringR;
                Vector3 pt = LocalPatchPoint(hit, center, tangent, bitangent, uTan, uBit, R);
                Vector3 ptN = LocalPatchNormal(hit, center, tangent, bitangent, uTan, uBit);

                scorch?.BurnAt(pt, 0.025f + ring * 0.003f, 0.7f + ring * 0.05f);
                if (s % 2 == 0)
                {
                    string label = MarketTickers[(ring + s) % MarketTickers.Length];
                    MemeCaption.SpawnTicker(pt + ptN * (R * 0.045f), label,
                        new Color(1f, 0.15f, 0.1f), R * 0.042f, ptN);
                }
            }

            ImpactShockwave.Spawn(hit + n * (R * 0.02f), n, R * (0.22f + ring * 0.1f));
            yield return new WaitForSeconds(0.095f);
        }
    }

    void SpawnRedFloatTiles(
        Vector3 hit, Vector3 center, Vector3 n, Vector3 tangent, Vector3 bitangent, float R, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float uTan = Random.Range(-0.3f, 0.3f) * R;
            float uBit = Random.Range(-0.24f, 0.24f) * R;
            Vector3 pt = LocalPatchPoint(hit, center, tangent, bitangent, uTan, uBit, R);
            Vector3 ptN = LocalPatchNormal(hit, center, tangent, bitangent, uTan, uBit);

            var tile = MemeVisuals.CreateRedStockTile(R * 0.09f);
            tile.transform.position = pt + ptN * (R * 0.02f);
            tile.transform.rotation = Quaternion.LookRotation(ptN);
            var mat = tile.GetComponent<Renderer>().material;
            tile.AddComponent<MemeRiseFade>().Init(mat, ptN * (R * 0.55f), Random.Range(0.45f, 0.75f));
        }
    }

    static IEnumerator MarketTickerShake(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            CameraShake.Shake(magnitude, 0.035f);
            yield return new WaitForSeconds(0.065f);
        }
    }

    IEnumerator LaunchTrojanHorse(Vector3 localDir)
    {
        IsTrojanRunning = true;
        float R = earth.Radius;
        Vector3 n = DirectionWorld(localDir);
        Vector3 hit = PointOnRay(localDir, R);
        Vector3 center = earth.transform.position;
        var scorch = EarthSurfaceScorch.Ensure(earth);

        const float aspect = 0.92f;
        float giftSize = R * 0.05f;
        float giantSize = R * 0.95f;
        float soldierSize = R * 0.13f;

        var horse = MemeVisuals.CreateTrojanHorse(giftSize);
        Vector3 pad = hit + n * (R * 0.04f);
        Vector3 dropFrom = hit + n * (R * 1.2f);
        horse.transform.position = dropFrom;
        SetTrojanHorseScale(horse.transform, giftSize, aspect);
        horse.GetComponent<MemeBillboard>()?.FaceTowardEarth(earth);

        float dropDur = 0.55f;
        float dropT = 0f;
        while (dropT < dropDur)
        {
            dropT += Time.deltaTime;
            float u = dropT / dropDur;
            float ease = 1f - (1f - u) * (1f - u);
            horse.transform.position = Vector3.Lerp(dropFrom, pad, ease);
            yield return null;
        }

        MemeCaption.Spawn(hit + n * (R * 0.32f), "A GIFT FOR EARTH", new Color(0.92f, 0.78f, 0.42f), R * 0.13f);
        yield return new WaitForSeconds(0.22f);
        MemeCaption.Spawn(hit + n * (R * 0.24f), "DO NOT OPEN", new Color(0.85f, 0.35f, 0.28f), R * 0.11f);
        LightHit(earth, hit, n, 0.018f, 0.006f, 0.015f, 0.28f);
        CameraShake.Shake(0.08f, 0.06f);
        yield return new WaitForSeconds(0.45f);

        Vector3 growPos = hit + n * (R * 0.32f);
        float growDur = 1.35f;
        float growT = 0f;
        while (growT < growDur)
        {
            growT += Time.deltaTime;
            float u = growT / growDur;
            float ease = u * u * (3f - 2f * u);
            float sz = Mathf.Lerp(giftSize, giantSize, ease);
            SetTrojanHorseScale(horse.transform, sz, aspect);
            horse.transform.position = Vector3.Lerp(pad, growPos, ease)
                + n * (Mathf.Sin(growT * 14f) * R * 0.002f * u);
            yield return null;
        }

        CinematicExplosion.Play(growPos, n, 1.2f);
        ImpactShockwave.Spawn(growPos, n, R * 0.75f);
        SpawnFlash(growPos, n, R * 0.1f, new Color(1f, 0.55f, 0.12f, 0.72f));
        MemeCaption.Spawn(hit + n * (R * 0.38f), "SURPRISE", new Color(1f, 0.82f, 0.25f), R * 0.16f);
        MemeCaption.Spawn(hit + n * (R * 0.26f), "TROJAN", new Color(0.95f, 0.42f, 0.18f), R * 0.13f);
        CameraShake.Shake(0.42f, 0.3f);

        Vector3 tangent = Vector3.Cross(n, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(n, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(n, tangent);

        const int soldierCount = 24;
        float horseH = giantSize;
        Vector3 doorBase = growPos + n * (horseH * 0.1f);
        float patchRadius = R * 0.16f;

        for (int i = 0; i < soldierCount; i++)
        {
            float side = (i % 2 == 0 ? 1f : -1f) * Random.Range(0.65f, 1f);
            Vector3 emerge = doorBase
                + tangent * (side * horseH * Random.Range(0.14f, 0.34f))
                + bitangent * Random.Range(-horseH * 0.2f, horseH * 0.2f)
                + n * Random.Range(horseH * 0.04f, horseH * 0.16f);

            Vector3 patchHit = hit
                + tangent * Random.Range(-patchRadius, patchRadius)
                + bitangent * Random.Range(-patchRadius * 0.85f, patchRadius * 0.85f);
            Vector3 aimLocal = earth.transform.InverseTransformPoint(patchHit).normalized;

            var kind = (TrojanSoldierKind)(i % 3);
            var soldier = MemeVisuals.CreateTrojanSoldier(soldierSize, kind);
            var raid = soldier.AddComponent<MemeTrojanSoldierRaid>();
            raid.Launch(earth, emerge, aimLocal, i * 0.05f);

            yield return new WaitForSeconds(0.055f);
        }

        yield return new WaitForSeconds(1.6f);

        for (int b = 0; b < 3; b++)
        {
            Vector3 localBlast = hit
                + tangent * Random.Range(-patchRadius * 0.7f, patchRadius * 0.7f)
                + bitangent * Random.Range(-patchRadius * 0.55f, patchRadius * 0.55f);
            Vector3 blastN = (localBlast - center).normalized;
            Vector3 blastHit = center + blastN * R;
            CinematicExplosion.Play(blastHit, blastN, 0.75f + b * 0.12f);
            ImpactShockwave.Spawn(blastHit, blastN, R * 0.35f);
            LightHit(earth, blastHit, blastN, 0.04f, 0.016f, 0.032f, 0.55f);
            scorch?.BurnAt(blastHit, 0.04f, 0.62f);
            CameraShake.Shake(0.12f, 0.09f);
            yield return new WaitForSeconds(0.12f);
        }

        yield return new WaitForSeconds(0.35f);

        scorch?.PaintMoltenFissures(hit, 0.09f, 0.68f, 2.4f, 10);
        LightHit(earth, hit, n, 0.07f, 0.028f, 0.048f, 0.75f);
        earth.ApplyImpact(hit, 14f);
        ApplyCasualtiesStatic(0.0052f);

        Vector3 exitStart = horse.transform.position;
        Vector3 exitEnd = hit + n * (R * 4.8f);
        float exitDur = 2.2f;
        float exitT = 0f;
        Vector3 exitScale = horse.transform.localScale;
        while (exitT < exitDur)
        {
            exitT += Time.deltaTime;
            float u = exitT / exitDur;
            float lift = u * u;
            horse.transform.position = Vector3.Lerp(exitStart, exitEnd, lift);
            horse.transform.localScale = exitScale * (1f - u * 0.35f);
            yield return null;
        }

        float fade = 0.35f;
        float fadeT = 0f;
        Vector3 fadeScale = horse.transform.localScale;
        while (fadeT < fade)
        {
            fadeT += Time.deltaTime;
            float u = fadeT / fade;
            horse.transform.localScale = fadeScale * (1f - u);
            horse.transform.position += n * (R * 0.28f * Time.deltaTime);
            yield return null;
        }

        Object.Destroy(horse);
        IsTrojanRunning = false;
        trojanCo = null;
    }

    static void SetTrojanHorseScale(Transform t, float size, float aspect) =>
        t.localScale = new Vector3(size * aspect, size, 1f);

    static Vector3 GlobalTariffBlastLocalDir(int slot, Vector3 primaryLocal)
    {
        const int slots = 5;
        float t = (slot + 0.5f) / slots;
        float phi = t * Mathf.PI * 2f + 0.85f;
        float theta = Mathf.PI * (0.2f + 0.58f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2.7f)));
        Vector3 dir = new Vector3(
            Mathf.Sin(theta) * Mathf.Cos(phi),
            Mathf.Cos(theta),
            Mathf.Sin(theta) * Mathf.Sin(phi));
        if (Vector3.Dot(dir, primaryLocal.normalized) > 0.78f)
            dir = Vector3.Slerp(dir, -primaryLocal, 0.6f);
        return dir.normalized;
    }

    void PlayTariffWorldBlast(EarthSurfaceScorch scorch, Vector3 hit, Vector3 normal, float R, float power)
    {
        CinematicExplosion.Play(hit, normal, power);
        ImpactShockwave.Spawn(hit, normal, R * (0.7f + power * 0.18f));
        SpawnFlash(hit, normal, R * 0.11f, new Color(1f, 0.48f, 0.1f, 0.68f));
        scorch?.PaintMoltenFissures(hit, 0.085f, 0.66f, 2.15f, 9);
        scorch?.BurnAt(hit, 0.055f, 0.82f);
        LightHit(earth, hit, normal, 0.06f, 0.028f, 0.045f, 0.72f);
        earth.ApplyImpact(hit, 12f + power * 5f);
        CameraShake.Shake(0.22f + power * 0.06f, 0.18f);
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

    static int sortOrder;
    static float lastSpawnTime;
    static Vector3 lastSpawnPos;
    static string lastSpawnText;

    public static void Spawn(Vector3 worldPos, string text, Color color, float scale)
    {
        scale = Mathf.Max(0.05f, scale);
        float minGap = scale * 0.5f;
        if (Time.time - lastSpawnTime < 0.5f
            && text == lastSpawnText
            && (worldPos - lastSpawnPos).sqrMagnitude < minGap * minGap)
            return;

        lastSpawnTime = Time.time;
        lastSpawnPos = worldPos;
        lastSpawnText = text;

        var go = new GameObject("MemeCaption");
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * scale;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = ++sortOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        var cap = go.AddComponent<MemeCaption>();
        cap.textMesh = tm;
        cap.startSize = scale;
        cap.drift = Random.insideUnitSphere * scale * 0.12f;
        cap.drift.y = Mathf.Abs(cap.drift.y);
    }

    public static void SpawnTicker(Vector3 worldPos, string text, Color color, float scale, Vector3 driftNormal)
    {
        scale = Mathf.Max(0.03f, scale);
        var go = new GameObject("MemeTicker");
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * scale;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 40;
        tm.characterSize = 0.09f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = ++sortOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        var cap = go.AddComponent<MemeCaption>();
        cap.textMesh = tm;
        cap.startSize = scale;
        cap.life = 0.65f;
        Vector3 driftDir = driftNormal.sqrMagnitude > 1e-4f ? driftNormal.normalized : Vector3.up;
        cap.drift = driftDir * scale * 0.45f
            + Random.insideUnitSphere * scale * 0.06f;
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

public class MemeRiseFade : MonoBehaviour
{
    Material mat;
    Vector3 velocity;
    float life;
    float t;

    public void Init(Material material, Vector3 vel, float lifetime)
    {
        mat = material;
        velocity = vel;
        life = Mathf.Max(0.1f, lifetime);
    }

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / life);
        if (mat != null)
        {
            Color c = mat.color;
            c.a = 1f - u;
            mat.color = c;
        }
        transform.localScale *= 1f - u * 0.35f * Time.deltaTime * 3f;
        if (u >= 1f)
            Destroy(gameObject);
    }
}
