using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpacecraftKind
{
    Ufo,
    OrbitalCannon,
    FighterWing,
    Battleship,
    PlanetKiller,
    VonNeumannProbe
}

/// <summary>4번 메뉴: 우주선 소환.</summary>
public class SpacecraftFleetSystem : MonoBehaviour
{
    public static SpacecraftFleetSystem Instance { get; private set; }

    static readonly List<FleetUfo> LiveUfos = new List<FleetUfo>();
    static readonly List<FleetBattleship> LiveBattleships = new List<FleetBattleship>();

    const int MaxLiveUfos = 8;
    const float UfoGroundFxCooldown = 0.4f;
    static float nextUfoGroundFxTime;
    static int lastUfoListCleanFrame;
    static Material sharedTractorBeamMat;

    public static int LiveUfoCount
    {
        get
        {
            PruneUfoList();
            return LiveUfos.Count;
        }
    }

    static void PruneUfoList()
    {
        for (int i = LiveUfos.Count - 1; i >= 0; i--)
        {
            if (LiveUfos[i] == null)
                LiveUfos.RemoveAt(i);
        }
    }

    public static bool TryConsumeUfoGroundFx()
    {
        if (Time.time < nextUfoGroundFxTime)
            return false;
        nextUfoGroundFxTime = Time.time + UfoGroundFxCooldown;
        return true;
    }

    [SerializeField] EarthPlanet earth;
    [SerializeField] Camera cam;

    public bool IsBusy { get; private set; }

    public static SpacecraftFleetSystem Ensure()
    {
        var s = FindObjectOfType<SpacecraftFleetSystem>();
        if (s != null)
            return s;
        return new GameObject("SpacecraftFleetSystem").AddComponent<SpacecraftFleetSystem>();
    }

    public void Abort()
    {
        StopAllCoroutines();
        IsBusy = false;
        for (int i = LiveUfos.Count - 1; i >= 0; i--)
        {
            if (LiveUfos[i] != null)
                Object.Destroy(LiveUfos[i].gameObject);
        }
        LiveUfos.Clear();
        for (int i = LiveBattleships.Count - 1; i >= 0; i--)
        {
            if (LiveBattleships[i] != null)
                Object.Destroy(LiveBattleships[i].gameObject);
        }
        LiveBattleships.Clear();
    }

    public static void RegisterUfo(FleetUfo ufo)
    {
        if (ufo != null && !LiveUfos.Contains(ufo))
            LiveUfos.Add(ufo);
    }

    public static void UnregisterUfo(FleetUfo ufo)
    {
        LiveUfos.Remove(ufo);
    }

    public static void RegisterBattleship(FleetBattleship ship)
    {
        if (ship != null && !LiveBattleships.Contains(ship))
            LiveBattleships.Add(ship);
    }

    public static void UnregisterBattleship(FleetBattleship ship)
    {
        LiveBattleships.Remove(ship);
    }

    public static FleetUfo FindNearestUfo(Vector3 from)
    {
        if (Time.frameCount - lastUfoListCleanFrame > 45)
        {
            PruneUfoList();
            lastUfoListCleanFrame = Time.frameCount;
        }

        FleetUfo best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < LiveUfos.Count; i++)
        {
            float sq = (LiveUfos[i].transform.position - from).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = LiveUfos[i];
            }
        }
        return best;
    }

    void Awake()
    {
        Instance = this;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (cam == null)
            cam = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    Vector3 aimOverride = Vector3.forward;
    bool hasAimOverride;

    public bool TrySummon(SpacecraftKind kind)
    {
        return TrySummonAt(kind, null);
    }

    public bool TrySummonAt(SpacecraftKind kind, Vector3? worldPoint)
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;
        if (cam == null)
            cam = Camera.main;

        if (worldPoint.HasValue)
        {
            aimOverride = (worldPoint.Value - earth.transform.position).normalized;
            hasAimOverride = aimOverride.sqrMagnitude > 1e-6f;
        }
        else
        {
            hasAimOverride = false;
        }

        if (kind == SpacecraftKind.Ufo && LiveUfoCount >= MaxLiveUfos)
            return false;

        StartCoroutine(Run(kind));
        return true;
    }

    IEnumerator Run(SpacecraftKind kind)
    {
        IsBusy = true;
        switch (kind)
        {
            case SpacecraftKind.Ufo:
                SummonUfo();
                break;
            case SpacecraftKind.OrbitalCannon:
                yield return SummonOrbitalCannon();
                break;
            case SpacecraftKind.FighterWing:
                yield return SummonFighterWing();
                break;
            case SpacecraftKind.Battleship:
                SummonBattleship();
                break;
            case SpacecraftKind.PlanetKiller:
                yield return SummonPlanetKiller();
                break;
            case SpacecraftKind.VonNeumannProbe:
                yield return SummonVonNeumann();
                break;
        }
        hasAimOverride = false;
        IsBusy = false;
        yield break;
    }

    Vector3 FaceDir()
    {
        if (hasAimOverride)
            return aimOverride;
        if (cam == null)
            return Random.onUnitSphere;
        Vector3 d = (cam.transform.position - earth.transform.position).normalized;
        return d.sqrMagnitude < 1e-6f ? Random.onUnitSphere : d;
    }

    Vector3 OrbitPos(Vector3 dir, float altitudeMul, float side = 0f)
    {
        Vector3 sideAxis = Vector3.Cross(dir, Vector3.up);
        if (sideAxis.sqrMagnitude < 1e-4f)
            sideAxis = Vector3.Cross(dir, Vector3.right);
        sideAxis.Normalize();
        Vector3 p = (dir + sideAxis * side).normalized;
        return earth.transform.position + p * (earth.Radius * altitudeMul);
    }

    public static GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, float emission = 0.4f)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = pos;
        go.transform.localScale = scale;
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(color, emission);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    public static void AlignBeam(Transform beam, Vector3 from, Vector3 to, float width = 0.04f)
    {
        Vector3 mid = (from + to) * 0.5f;
        float len = Vector3.Distance(from, to) * 0.5f;
        beam.position = mid;
        beam.up = (from - to).normalized;
        beam.localScale = new Vector3(width, Mathf.Max(0.01f, len), width);
    }

    public static IEnumerator FireLaser(
        Vector3 from,
        Vector3 to,
        Color color,
        float life,
        StrikeImpactKind kind = StrikeImpactKind.Generic)
    {
        float scale = LaserVfxSpawner.FleetBeamScale(kind);
        var prefab = LaserVfxSpawner.ForFleetStrike(kind, impact: false);
        var beam = LaserVfxSpawner.SpawnBeam(prefab, from, to, scale, life);
        if (beam != null)
        {
            var impactPrefab = LaserVfxSpawner.ForFleetStrike(kind, impact: true);
            Vector3 hitNormal = (from - to).normalized;
            if (hitNormal.sqrMagnitude < 1e-6f)
                hitNormal = Vector3.up;
            LaserVfxSpawner.SpawnImpact(
                impactPrefab, to, hitNormal, LaserVfxSpawner.FleetImpactScale(kind), Mathf.Clamp(life, 0.14f, 0.55f));
            yield return new WaitForSecondsRealtime(life);
            yield break;
        }

        beam = Prim(PrimitiveType.Cylinder, "Laser", from, Vector3.one, color, 3f);
        AlignBeam(beam.transform, from, to, width: 0.035f);
        float t = 0f;
        while (t < life && beam != null)
        {
            t += Time.deltaTime;
            AlignBeam(beam.transform, from, to, width: 0.035f);
            float a = 1f - t / life;
            var r = beam.GetComponent<Renderer>();
            if (r != null && r.material != null)
            {
                var c = color;
                c.a = a;
                if (r.material.HasProperty("_Color"))
                    r.material.SetColor("_Color", c);
            }
            yield return null;
        }

        if (beam != null)
            Object.Destroy(beam);
    }

    /// <summary>날아가는 레이저 탄 — 배틀쉽 UFO 격추용.</summary>
    public static IEnumerator FireBoltLaser(
        Vector3 from,
        Vector3 to,
        Color color,
        StrikeImpactKind kind = StrikeImpactKind.BattleshipBeam)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.01f)
            yield break;

        Vector3 dir = delta / dist;
        float boltLength = Mathf.Clamp(dist * 0.14f, 0.1f, 0.65f);
        float speed = Mathf.Clamp(dist * 3.5f, 10f, 55f);
        float headDist = 0f;

        var prefab = LaserVfxSpawner.ForFleetStrike(kind, impact: false);
        float scale = LaserVfxSpawner.FleetBeamScale(kind);
        GameObject vfxBolt = null;
        GameObject primBolt = null;

        while (headDist < dist)
        {
            headDist += speed * Time.deltaTime;
            float head = Mathf.Min(headDist, dist);
            float tail = Mathf.Max(0f, head - boltLength);
            Vector3 headPos = from + dir * head;
            Vector3 tailPos = from + dir * tail;

            if (prefab != null)
            {
                if (vfxBolt == null)
                    vfxBolt = LaserVfxSpawner.SpawnBeam(prefab, tailPos, headPos, scale, lifetime: -1f);
                else
                    LaserVfxSpawner.AimBeam(vfxBolt.transform, tailPos, headPos, scale);
            }
            else
            {
                if (primBolt == null)
                {
                    primBolt = Prim(PrimitiveType.Cylinder, "BoltLaser", tailPos, Vector3.one, color, 4f);
                    Object.Destroy(primBolt.GetComponent<Collider>());
                }

                AlignBeam(primBolt.transform, tailPos, headPos, width: 0.02f);
            }

            yield return null;
        }

        if (vfxBolt != null)
            Object.Destroy(vfxBolt);
        if (primBolt != null)
            Object.Destroy(primBolt);

        var impactPrefab = LaserVfxSpawner.ForFleetStrike(kind, impact: true);
        Vector3 hitNormal = (from - to).normalized;
        if (hitNormal.sqrMagnitude < 1e-6f)
            hitNormal = Vector3.up;
        LaserVfxSpawner.SpawnImpact(impactPrefab, to, hitNormal, LaserVfxSpawner.FleetImpactScale(kind), 0.24f);
    }

    static GameObject MakeUfoTractorBeam(Vector3 pos, float earthRadius)
    {
        if (sharedTractorBeamMat == null)
            sharedTractorBeamMat = RuntimeMaterial.UnlitTransparent(new Color(0.38f, 0.95f, 0.58f, 0.2f));

        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "TractorBeam";
        Object.Destroy(beam.GetComponent<Collider>());
        var rend = beam.GetComponent<Renderer>();
        rend.sharedMaterial = sharedTractorBeamMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.transform.position = pos;
        beam.transform.localScale = new Vector3(0.022f, earthRadius * 0.16f, 0.022f);
        return beam;
    }

    void SummonUfo()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 1.55f);

        GameObject ufo = FleetShipModels.SpawnUfo(pos, dir, earth.Radius);
        if (ufo == null)
        {
            ufo = Prim(PrimitiveType.Sphere, "UFO", pos, new Vector3(0.55f, 0.12f, 0.55f),
                new Color(0.75f, 0.85f, 0.95f), 1.2f);
            var dome = Prim(PrimitiveType.Sphere, "Dome", pos + Vector3.up * 0.06f,
                new Vector3(0.28f, 0.18f, 0.28f), new Color(0.4f, 1f, 0.7f), 2f);
            dome.transform.SetParent(ufo.transform, true);
        }

        var beam = MakeUfoTractorBeam(pos, earth.Radius);
        beam.transform.SetParent(ufo.transform, true);
        Transform beamTf = beam.transform;

        var ctrl = ufo.GetComponent<FleetUfo>();
        if (ctrl == null)
            ctrl = ufo.AddComponent<FleetUfo>();
        ctrl.Init(earth, dir, beamTf);
    }

    void SummonBattleship()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 1.85f);
        var rot = FleetShipModels.BuildOrbitRotation(pos, earth.transform.position);

        GameObject ship = FleetShipModels.SpawnBattleship(pos, rot, earth.Radius);
        if (ship == null)
        {
            ship = Prim(PrimitiveType.Cube, "Battleship", pos, new Vector3(1.4f, 0.28f, 0.45f),
                new Color(0.25f, 0.28f, 0.32f), 0.2f);
            ship.transform.rotation = rot;
            var bridge = Prim(PrimitiveType.Cube, "Bridge", pos + dir * 0.12f,
                new Vector3(0.35f, 0.18f, 0.22f), new Color(0.4f, 0.45f, 0.5f), 0.5f);
            bridge.transform.SetParent(ship.transform, true);
        }

        var ctrl = ship.GetComponent<FleetBattleship>();
        if (ctrl == null)
            ctrl = ship.AddComponent<FleetBattleship>();
        ctrl.Init(earth, pos, dir);
    }

    IEnumerator SummonOrbitalCannon()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 1.7f, 0.2f);
        var rot = FleetShipModels.BuildOrbitRotation(pos, earth.transform.position);
        var gun = FleetShipModels.SpawnOrbitalCannon(pos, rot, earth.Radius);
        if (gun == null)
        {
            gun = Prim(PrimitiveType.Cylinder, "OrbitalCannon", pos, new Vector3(0.12f, 0.7f, 0.12f),
                new Color(0.35f, 0.38f, 0.42f), 0.3f);
            gun.transform.rotation = Quaternion.LookRotation(earth.transform.position - pos) * Quaternion.Euler(90f, 0f, 0f);
        }

        float life = 8f;
        float t = 0f;
        float nextShot = 0.6f;
        while (t < life && gun != null)
        {
            t += Time.deltaTime;
            gun.transform.RotateAround(earth.transform.position, Vector3.up, 12f * Time.deltaTime);
            gun.transform.rotation = FleetShipModels.BuildOrbitRotation(
                gun.transform.position, earth.transform.position);

            Vector3 aim = earth.transform.position - gun.transform.position;
            if (t >= nextShot)
            {
                nextShot = t + 1.1f;
                Vector3 hit = earth.transform.position - aim.normalized * earth.Radius;
                StartCoroutine(FireLaser(
                    gun.transform.position, hit, new Color(1f, 0.25f, 0.1f), 0.35f, StrikeImpactKind.OrbitalCannon));
                StrikeImpactFx.Play(earth, hit, -aim.normalized, 0.2f, StrikeImpactKind.OrbitalCannon);
            }
            yield return null;
        }

        if (gun != null)
            Object.Destroy(gun);
    }

    IEnumerator SummonFighterWing()
    {
        // 낮게 깔려 착륙하듯 폭격 → 나비처럼 솟아 퇴장
        Vector3 dir = FaceDir();
        Vector3 center = earth.transform.position;
        float R = earth.Radius;
        Vector3 target = center + dir * R;

        Vector3 flyDir = Vector3.Cross(dir, Vector3.up);
        if (flyDir.sqrMagnitude < 1e-4f)
            flyDir = Vector3.Cross(dir, Vector3.right);
        flyDir.Normalize();
        Vector3 wingAxis = Vector3.Cross(flyDir, dir).normalized;

        var root = new GameObject("FighterWing");
        var fighters = new Transform[5];
        float[] lane = { -0.5f, -0.22f, 0f, 0.22f, 0.5f };
        float[] lag = { 0.06f, 0.025f, 0f, 0.025f, 0.06f };

        // 시작: 아직 조금 높은 접근
        Vector3 startSurf = (target - flyDir * (R * 2.1f) - center).normalized;
        Vector3 start = center + startSurf * (R * 1.38f);

        for (int i = 0; i < fighters.Length; i++)
        {
            Vector3 offset = wingAxis * (lane[i] * R * 0.12f);
            Vector3 spawnPos = start + offset;
            var spawnRot = FleetShipModels.BuildFlightRotation(spawnPos, center, flyDir, -8f, lane[i] * 8f);
            var f = FleetShipModels.SpawnFighter(spawnPos, spawnRot, R, i);
            if (f == null)
            {
                f = Prim(PrimitiveType.Cube, "Fighter", start + offset, new Vector3(0.11f, 0.04f, 0.22f),
                    new Color(0.9f, 0.55f, 0.2f), 0.9f);
            }
            else
            {
                f.transform.SetParent(root.transform, true);
            }

            fighters[i] = f.transform;
        }

        float duration = 3.8f;
        float t = 0f;
        bool volleyA = false, volleyB = false, volleyC = false, volleyD = false;

        while (t < duration && root != null)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            // 초반 조금 느리다가 통과 후 가속 퇴장
            float ease = u < 0.55f
                ? Mathf.SmoothStep(0f, 0.55f, u) / 0.55f * 0.55f
                : 0.55f + Mathf.SmoothStep(0f, 1f, (u - 0.55f) / 0.45f) * 0.45f;

            // 고도: 접근 → 거의 착륙(스킴) → 급상승 퇴장
            float altMul;
            if (u < 0.28f)
                altMul = Mathf.Lerp(1.42f, 1.08f, u / 0.28f);
            else if (u < 0.62f)
                altMul = Mathf.Lerp(1.08f, 1.035f, (u - 0.28f) / 0.34f); // 지표면에 바짝
            else
                altMul = Mathf.Lerp(1.035f, 2.15f, Mathf.Pow((u - 0.62f) / 0.38f, 0.85f)); // 나비처럼 솟음

            float along = Mathf.Lerp(-2.0f, 2.3f, ease); // 타겟 기준 진행(R 배수)

            for (int i = 0; i < fighters.Length; i++)
            {
                if (fighters[i] == null)
                    continue;

                float ui = Mathf.Clamp01(ease - lag[i] * 0.12f);
                float alongI = Mathf.Lerp(-2.0f, 2.3f, ui);
                float altI = altMul + lag[i] * 0.02f;

                Vector3 ground = target + flyDir * (alongI * R) + wingAxis * (lane[i] * R * 0.1f);
                Vector3 radial = (ground - center).normalized;
                Vector3 pos = center + radial * (R * altI);

                // 진행 방향 (다음 샘플)
                float alongNext = Mathf.Lerp(-2.0f, 2.3f, Mathf.Clamp01(ui + 0.04f));
                Vector3 groundNext = target + flyDir * (alongNext * R);
                Vector3 posNext = center + (groundNext - center).normalized * (R * altI);
                Vector3 look = (posNext - pos).normalized;
                if (look.sqrMagnitude < 1e-6f)
                    look = flyDir;

                // 다이브 때는 코를 아래로, 상승 때는 위로
                float pitch = u < 0.55f ? -12f : Mathf.Lerp(-4f, 18f, (u - 0.55f) / 0.45f);
                fighters[i].position = pos;
                fighters[i].rotation = FleetShipModels.BuildFlightRotation(
                    pos, center, look, pitch, lane[i] * 8f);
            }

            // 초저공 구간에서 벌처럼 연사
            if (!volleyA && u > 0.30f) { volleyA = true; FireStrafe(root.transform, target - flyDir * (R * 0.06f), dir, 0.55f); }
            if (!volleyB && u > 0.40f) { volleyB = true; FireStrafe(root.transform, target, dir, 0.85f); }
            if (!volleyC && u > 0.48f) { volleyC = true; FireStrafe(root.transform, target + flyDir * (R * 0.05f), dir, 0.7f); }
            if (!volleyD && u > 0.56f) { volleyD = true; FireStrafe(root.transform, target + flyDir * (R * 0.1f), dir, 0.5f); }

            yield return null;
        }

        if (root != null)
            Object.Destroy(root);
    }

    void FireStrafe(Transform wingRoot, Vector3 hit, Vector3 normal, float power)
    {
        Vector3 from = hit + normal * (earth.Radius * 0.5f);
        if (wingRoot != null && wingRoot.childCount > 0)
        {
            int idx = Random.Range(0, wingRoot.childCount);
            from = wingRoot.GetChild(idx).position;
        }
        StartCoroutine(FireLaser(from, hit, new Color(1f, 0.55f, 0.15f), 0.22f, StrikeImpactKind.FighterStrafe));
        StrikeImpactFx.Play(earth, hit, normal, Mathf.Lerp(0.14f, 0.24f, power), StrikeImpactKind.FighterStrafe);
        EarthCraterDeform.Ensure(earth)?.Dig(hit, 0.07f, 0.035f, false);
    }

    IEnumerator SummonPlanetKiller()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 2.4f);
        var rot = FleetShipModels.BuildOrbitRotation(pos, earth.transform.position);
        var ship = FleetShipModels.SpawnPlanetKiller(pos, rot, earth.Radius);
        Transform muzzle;
        if (ship == null)
        {
            ship = Prim(PrimitiveType.Cube, "PlanetKiller", pos, new Vector3(2.2f, 0.5f, 0.7f),
                new Color(0.15f, 0.05f, 0.05f), 0.6f);
            var muzzleGo = Prim(PrimitiveType.Sphere, "Muzzle", pos - dir * 0.6f,
                Vector3.one * 0.35f, new Color(1f, 0.2f, 0.05f), 3f);
            muzzleGo.transform.SetParent(ship.transform, true);
            muzzle = muzzleGo.transform;
        }
        else
        {
            var muzzleGo = Prim(PrimitiveType.Sphere, "Muzzle", Vector3.zero,
                Vector3.one * 0.35f, new Color(1f, 0.2f, 0.05f), 3f);
            muzzleGo.transform.SetParent(ship.transform, false);
            muzzleGo.transform.localPosition = Vector3.forward * 0.42f;
            muzzle = muzzleGo.transform;
        }

        float charge = 2.2f;
        float t = 0f;
        Vector3 earthCenter = earth.transform.position;
        Vector3 hitPreview = earthCenter + dir * earth.Radius;
        GameObject chargeBeam = null;
        float chargeScale = LaserVfxSpawner.FleetBeamScale(StrikeImpactKind.PlanetKiller);
        if (LaserVfxSpawner.HasCatalog)
        {
            var chargePrefab = LaserVfxSpawner.ForFleetStrike(StrikeImpactKind.PlanetKiller, impact: false);
            chargeBeam = LaserVfxSpawner.SpawnBeam(
                chargePrefab, muzzle.position, hitPreview, chargeScale * 0.55f, lifetime: -1f);
        }

        while (t < charge && ship != null)
        {
            t += Time.deltaTime;
            float pulse = 0.35f + 0.15f * Mathf.Sin(t * 12f);
            muzzle.transform.localScale = Vector3.one * pulse;
            ship.transform.rotation = FleetShipModels.BuildOrbitRotation(
                ship.transform.position, earthCenter);
            hitPreview = earthCenter + (ship.transform.position - earthCenter).normalized * earth.Radius;
            if (chargeBeam != null)
            {
                float grow = Mathf.Lerp(0.55f, 1f, t / charge);
                LaserVfxSpawner.AimBeam(chargeBeam.transform, muzzle.position, hitPreview, chargeScale * grow);
            }
            yield return null;
        }

        if (chargeBeam != null)
            Object.Destroy(chargeBeam);

        if (ship == null)
            yield break;

        Vector3 hitDir = (ship.transform.position - earth.transform.position).normalized;
        Vector3 hit = earth.transform.position + hitDir * earth.Radius;
        StartCoroutine(FireLaser(
            muzzle.transform.position, hit, new Color(1f, 0.15f, 0.05f), 1.2f, StrikeImpactKind.PlanetKiller));
        StrikeImpactFx.Play(earth, hit, hitDir, 0.46f, StrikeImpactKind.PlanetKiller);
        NuclearBlast.Play(earth, hit, hitDir, 1.45f);
        EarthCraterDeform.Ensure(earth)?.Dig(hit, 0.22f, 0.1f, true);
        CameraShake.Shake(0.14f, 0.22f);

        yield return new WaitForSecondsRealtime(2.5f);
        if (ship != null)
            Object.Destroy(ship);
    }

    IEnumerator SummonVonNeumann()
    {
        Vector3 dir = FaceDir();
        var root = new GameObject("VonNeumannSwarm");
        int n = 8;
        var probes = new Transform[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 d = (dir + Random.insideUnitSphere * 0.7f).normalized;
            Vector3 probePos = OrbitPos(d, 1.25f + Random.Range(0f, 0.2f));
            var rot = FleetShipModels.BuildOrbitRotation(probePos, earth.transform.position);
            var p = FleetShipModels.SpawnProbe(probePos, rot, earth.Radius);
            if (p == null)
            {
                p = Prim(PrimitiveType.Sphere, "Probe", probePos,
                    Vector3.one * Random.Range(0.06f, 0.12f), new Color(0.7f, 0.9f, 1f), 1.5f);
            }

            p.transform.SetParent(root.transform, true);
            probes[i] = p.transform;
        }

        float life = 8f;
        float t = 0f;
        while (t < life && root != null)
        {
            t += Time.deltaTime;
            for (int i = 0; i < probes.Length; i++)
            {
                if (probes[i] == null)
                    continue;
                Vector3 toEarth = (earth.transform.position - probes[i].position).normalized;
                probes[i].position += toEarth * (0.35f * Time.deltaTime);
                probes[i].rotation = FleetShipModels.BuildOrbitRotation(
                    probes[i].position, earth.transform.position);
            }

            if (Time.frameCount % 10 == 0)
            {
                Vector3 hitDir = (FaceDir() + Random.insideUnitSphere * 0.5f).normalized;
                Vector3 hit = earth.transform.position + hitDir * earth.Radius;
                EarthCraterDeform.Ensure(earth)?.Dig(hit, 0.08f, 0.045f, false);
                if (Time.frameCount % 30 == 0)
                {
                    StrikeImpactFx.Play(earth, hit, -hitDir, 0.17f, StrikeImpactKind.VonNeumannProbe);
                    if (probes.Length > 0 && probes[0] != null)
                    {
                        StartCoroutine(FireLaser(
                            probes[0].position,
                            hit,
                            new Color(0.55f, 0.85f, 1f),
                            0.16f,
                            StrikeImpactKind.VonNeumannProbe));
                    }
                }
                if (Time.frameCount % 45 == 0)
                {
                    PopulationCasualtySystem.ApplyAt(
                        earth,
                        hit,
                        PopulationCasualtySystem.DigNormToDegrees(0.08f),
                        0.07f,
                        0.5f);
                }
            }
            yield return null;
        }

        if (root != null)
            Object.Destroy(root);
    }
}

/// <summary>장시간 체공. UFO가 있을 때만 격추 — 지구 지킴이.</summary>
public class FleetBattleship : MonoBehaviour
{
    EarthPlanet earth;
    Vector3 holdPos;
    Vector3 holdDir;
    float life = 120f;
    float age;
    float nextShot = 1.2f;
    float shotInterval = 1.6f;

    public void Init(EarthPlanet planet, Vector3 pos, Vector3 dir)
    {
        earth = planet;
        holdPos = pos;
        holdDir = dir.normalized;
        transform.position = holdPos;
        transform.rotation = FleetShipModels.BuildOrbitRotation(holdPos, earth.transform.position);

        SpacecraftFleetSystem.RegisterBattleship(this);
    }

    void OnDestroy()
    {
        SpacecraftFleetSystem.UnregisterBattleship(this);
    }

    void Update()
    {
        if (earth == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        // 소환 지점에 고정 — 지구를 향해 수평 유지
        transform.position = holdPos;

        Vector3 earthCenter = earth.transform.position;
        Quaternion faceEarth = FleetShipModels.BuildOrbitRotation(holdPos, earthCenter);
        FleetUfo ufo = SpacecraftFleetSystem.FindNearestUfo(holdPos);

        if (ufo == null)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, faceEarth, Time.deltaTime * 2f);
            return;
        }

        Vector3 aimPoint = ufo.transform.position;
        Quaternion want = FleetShipModels.BuildOrbitRotation(holdPos, earthCenter, aimPoint);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * 3f);

        if (age < nextShot)
            return;

        int ufoCount = SpacecraftFleetSystem.LiveUfoCount;
        float interval = ufoCount > 6 ? 2.8f : ufoCount > 3 ? 2.1f : shotInterval;
        nextShot = age + interval;

        StartCoroutine(SpacecraftFleetSystem.FireBoltLaser(
            transform.position, aimPoint, new Color(1f, 0.82f, 0.2f), StrikeImpactKind.BattleshipBeam));
        ufo.TakeHit(1f, transform.position);
    }
}

/// <summary>궤도 UFO. 배틀쉽에 맞으면 파괴.</summary>
public class FleetUfo : MonoBehaviour
{
    EarthPlanet earth;
    Transform beam;
    Vector3 orbitAxis;
    float hp = 4f;
    float life = 90f;
    float age;

    public void Init(EarthPlanet planet, Vector3 faceDir, Transform tractorBeam)
    {
        earth = planet;
        beam = tractorBeam;
        orbitAxis = Vector3.Cross(faceDir, Vector3.up);
        if (orbitAxis.sqrMagnitude < 1e-4f)
            orbitAxis = Vector3.right;
        orbitAxis.Normalize();
        SpacecraftFleetSystem.RegisterUfo(this);
    }

    void OnDestroy()
    {
        SpacecraftFleetSystem.UnregisterUfo(this);
    }

    public void TakeHit(float damage, Vector3 from)
    {
        hp -= damage;
        if (hp > 0f)
            return;

        if (earth != null)
        {
            Vector3 n = (transform.position - earth.transform.position).normalized;
            StrikeImpactFx.Play(earth, transform.position, n, 0.18f, StrikeImpactKind.UfoPop);
        }
        CameraShake.Shake(0.025f, 0.05f);
        Destroy(gameObject);
    }

    void Update()
    {
        if (earth == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        transform.RotateAround(earth.transform.position, orbitAxis, 22f * Time.deltaTime);

        int phase = Time.frameCount + GetInstanceID();
        if ((phase & 1) == 0)
        {
            transform.rotation = FleetShipModels.BuildOrbitRotation(
                transform.position, earth.transform.position);
            if (beam != null)
                SpacecraftFleetSystem.AlignBeam(beam, transform.position, earth.transform.position, 0.022f);
        }

        if ((phase % 96) == 0 && SpacecraftFleetSystem.TryConsumeUfoGroundFx())
        {
            Vector3 hit = earth.transform.position +
                (transform.position - earth.transform.position).normalized * earth.Radius;
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(hit, 0.012f, 0.32f);
        }
    }
}
