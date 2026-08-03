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
        LiveUfos.RemoveAll(u => u == null);
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

    public static void AlignBeam(Transform beam, Vector3 from, Vector3 to)
    {
        Vector3 mid = (from + to) * 0.5f;
        float len = Vector3.Distance(from, to) * 0.5f;
        beam.position = mid;
        beam.up = (from - to).normalized;
        var s = beam.localScale;
        s.y = Mathf.Max(0.01f, len);
        beam.localScale = s;
    }

    public static IEnumerator FireLaser(Vector3 from, Vector3 to, Color color, float life)
    {
        var beam = Prim(PrimitiveType.Cylinder, "Laser", from, Vector3.one, color, 3f);
        AlignBeam(beam.transform, from, to);
        float t = 0f;
        while (t < life && beam != null)
        {
            t += Time.deltaTime;
            AlignBeam(beam.transform, from, to);
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

    void SummonUfo()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 1.55f);
        var ufo = Prim(PrimitiveType.Sphere, "UFO", pos, new Vector3(0.55f, 0.12f, 0.55f),
            new Color(0.75f, 0.85f, 0.95f), 1.2f);
        var dome = Prim(PrimitiveType.Sphere, "Dome", pos + Vector3.up * 0.06f,
            new Vector3(0.28f, 0.18f, 0.28f), new Color(0.4f, 1f, 0.7f), 2f);
        dome.transform.SetParent(ufo.transform, true);

        var beam = Prim(PrimitiveType.Cylinder, "Beam", pos,
            new Vector3(0.08f, earth.Radius * 0.35f, 0.08f), new Color(0.3f, 1f, 0.5f, 1f), 2.5f);
        beam.transform.SetParent(ufo.transform, true);

        var ctrl = ufo.AddComponent<FleetUfo>();
        ctrl.Init(earth, dir, beam.transform);
    }

    void SummonBattleship()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 1.85f);
        var ship = Prim(PrimitiveType.Cube, "Battleship", pos, new Vector3(1.4f, 0.28f, 0.45f),
            new Color(0.25f, 0.28f, 0.32f), 0.2f);
        Vector3 tangent = Vector3.Cross(dir, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(dir, Vector3.right);
        ship.transform.rotation = Quaternion.LookRotation(tangent.normalized, dir);

        var bridge = Prim(PrimitiveType.Cube, "Bridge", pos + dir * 0.12f,
            new Vector3(0.35f, 0.18f, 0.22f), new Color(0.4f, 0.45f, 0.5f), 0.5f);
        bridge.transform.SetParent(ship.transform, true);

        var ctrl = ship.AddComponent<FleetBattleship>();
        ctrl.Init(earth, pos, dir);
    }

    IEnumerator SummonOrbitalCannon()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 1.7f, 0.2f);
        var gun = Prim(PrimitiveType.Cylinder, "OrbitalCannon", pos, new Vector3(0.12f, 0.7f, 0.12f),
            new Color(0.35f, 0.38f, 0.42f), 0.3f);
        gun.transform.rotation = Quaternion.LookRotation(earth.transform.position - pos) * Quaternion.Euler(90f, 0f, 0f);

        float life = 8f;
        float t = 0f;
        float nextShot = 0.6f;
        while (t < life && gun != null)
        {
            t += Time.deltaTime;
            gun.transform.RotateAround(earth.transform.position, Vector3.up, 12f * Time.deltaTime);
            Vector3 aim = earth.transform.position - gun.transform.position;
            gun.transform.rotation = Quaternion.LookRotation(aim) * Quaternion.Euler(90f, 0f, 0f);

            if (t >= nextShot)
            {
                nextShot = t + 1.1f;
                Vector3 hit = earth.transform.position - aim.normalized * earth.Radius;
                StartCoroutine(FireLaser(gun.transform.position, hit, new Color(1f, 0.25f, 0.1f), 0.35f));
                NuclearBlast.Play(earth, hit, -aim.normalized, 0.7f);
            }
            yield return null;
        }

        if (gun != null)
            Object.Destroy(gun);
    }

    IEnumerator SummonFighterWing()
    {
        Vector3 dir = FaceDir();
        var root = new GameObject("FighterWing");
        root.transform.position = earth.transform.position;
        for (int i = 0; i < 6; i++)
        {
            float ang = i / 6f * Mathf.PI * 2f;
            Vector3 side = new Vector3(Mathf.Cos(ang), 0.15f * Mathf.Sin(ang * 2f), Mathf.Sin(ang));
            Vector3 p = OrbitPos((dir + side * 0.4f).normalized, 1.45f + (i % 3) * 0.08f);
            var f = Prim(PrimitiveType.Cube, "Fighter", p, new Vector3(0.12f, 0.04f, 0.22f),
                new Color(0.9f, 0.55f, 0.2f), 0.8f);
            f.transform.SetParent(root.transform, true);
            f.transform.rotation = Quaternion.LookRotation(earth.transform.position - p);
        }

        float life = 7f;
        float t = 0f;
        float nextPass = 0.8f;
        while (t < life && root != null)
        {
            t += Time.deltaTime;
            root.transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
            if (t >= nextPass)
            {
                nextPass = t + 0.9f;
                Vector3 hitDir = (FaceDir() + Random.insideUnitSphere * 0.35f).normalized;
                Vector3 hit = earth.transform.position + hitDir * earth.Radius;
                NuclearBlast.Play(earth, hit, hitDir, 0.45f);
            }
            yield return null;
        }

        if (root != null)
            Object.Destroy(root);
    }

    IEnumerator SummonPlanetKiller()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 2.4f);
        var ship = Prim(PrimitiveType.Cube, "PlanetKiller", pos, new Vector3(2.2f, 0.5f, 0.7f),
            new Color(0.15f, 0.05f, 0.05f), 0.6f);
        var muzzle = Prim(PrimitiveType.Sphere, "Muzzle", pos - dir * 0.6f,
            Vector3.one * 0.35f, new Color(1f, 0.2f, 0.05f), 3f);
        muzzle.transform.SetParent(ship.transform, true);

        float charge = 2.2f;
        float t = 0f;
        while (t < charge && ship != null)
        {
            t += Time.deltaTime;
            float pulse = 0.35f + 0.15f * Mathf.Sin(t * 12f);
            muzzle.transform.localScale = Vector3.one * pulse;
            yield return null;
        }

        if (ship == null)
            yield break;

        Vector3 hitDir = (ship.transform.position - earth.transform.position).normalized;
        Vector3 hit = earth.transform.position + hitDir * earth.Radius;
        StartCoroutine(FireLaser(muzzle.transform.position, hit, new Color(1f, 0.15f, 0.05f), 1.2f));
        NuclearBlast.Play(earth, hit, hitDir, 2.2f);
        EarthCraterDeform.Ensure(earth)?.Dig(hit, 0.22f, 0.1f, true);
        CameraShake.Shake(0.25f, 0.45f);

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
            var p = Prim(PrimitiveType.Sphere, "Probe", OrbitPos(d, 1.25f + Random.Range(0f, 0.2f)),
                Vector3.one * Random.Range(0.06f, 0.12f), new Color(0.7f, 0.9f, 1f), 1.5f);
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
                probes[i].Rotate(Vector3.up, 180f * Time.deltaTime, Space.Self);
            }

            if (Time.frameCount % 10 == 0)
            {
                Vector3 hitDir = (FaceDir() + Random.insideUnitSphere * 0.5f).normalized;
                Vector3 hit = earth.transform.position + hitDir * earth.Radius;
                EarthCraterDeform.Ensure(earth)?.Dig(hit, 0.08f, 0.045f, false);
            }
            yield return null;
        }

        if (root != null)
            Object.Destroy(root);
    }
}

/// <summary>장시간 체공. UFO 우선 공격, 없으면 지구에 핵급 임팩트.</summary>
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

        // 소환 지점 근처에 오래 머무름 (살짝만 흔들림)
        float bob = Mathf.Sin(age * 0.7f) * (earth.Radius * 0.03f);
        Vector3 side = Vector3.Cross(holdDir, Vector3.up);
        if (side.sqrMagnitude < 1e-4f)
            side = Vector3.Cross(holdDir, Vector3.right);
        side.Normalize();
        Vector3 target = holdPos + side * (Mathf.Sin(age * 0.35f) * earth.Radius * 0.08f) + holdDir * bob;
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 1.5f);

        FleetUfo ufo = SpacecraftFleetSystem.FindNearestUfo(transform.position);
        Vector3 aimPoint;
        if (ufo != null)
            aimPoint = ufo.transform.position;
        else
        {
            // 지구 표면 (홀드 방향 부근 + 약간의 랜덤)
            Vector3 hitDir = (holdDir + Random.insideUnitSphere * 0.12f).normalized;
            aimPoint = earth.transform.position + hitDir * earth.Radius;
        }

        Vector3 look = (aimPoint - transform.position).normalized;
        if (look.sqrMagnitude > 1e-6f)
        {
            Quaternion want = Quaternion.LookRotation(look, holdDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * 2.5f);
        }

        if (age < nextShot)
            return;
        nextShot = age + shotInterval;

        StartCoroutine(SpacecraftFleetSystem.FireLaser(
            transform.position, aimPoint, new Color(1f, 0.75f, 0.15f), 0.35f));

        if (ufo != null)
        {
            ufo.TakeHit(1f, transform.position);
        }
        else
        {
            Vector3 hitDir = (aimPoint - earth.transform.position).normalized;
            // 핵폭탄급 임팩트
            NuclearBlast.Play(earth, aimPoint, hitDir, 1.25f);
            EarthCraterDeform.Ensure(earth)?.Dig(aimPoint, 0.12f, 0.07f, false);
            EarthSurfaceScorch.Ensure(earth)?.BurnAt(aimPoint, 0.04f, 0.8f);
            CameraShake.Shake(0.1f, 0.18f);
        }
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
        // 피격 플래시
        var rend = GetComponent<Renderer>();
        if (rend != null)
            rend.material = RuntimeMaterial.Opaque(new Color(1f, 0.4f, 0.3f), 2f);

        if (hp > 0f)
            return;

        // 격추 폭발
        if (earth != null)
        {
            Vector3 n = (transform.position - earth.transform.position).normalized;
            NuclearBlast.Play(earth, transform.position, n, 0.6f);
        }
        CameraShake.Shake(0.12f, 0.2f);
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
        if (beam != null)
            SpacecraftFleetSystem.AlignBeam(beam, transform.position, earth.transform.position);

        if (Time.frameCount % 14 == 0)
        {
            Vector3 hit = earth.transform.position +
                (transform.position - earth.transform.position).normalized * earth.Radius;
            EarthCraterDeform.Ensure(earth)?.Dig(hit, 0.05f, 0.018f, false);
        }
    }
}
