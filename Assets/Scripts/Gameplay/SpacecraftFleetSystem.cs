using System.Collections;
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

/// <summary>4번 메뉴: 우주선 소환 (궤도에 띄우고 간단한 연출).</summary>
public class SpacecraftFleetSystem : MonoBehaviour
{
    public static SpacecraftFleetSystem Instance { get; private set; }

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

    public bool TrySummon(SpacecraftKind kind)
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;
        if (cam == null)
            cam = Camera.main;

        StartCoroutine(Run(kind));
        return true;
    }

    IEnumerator Run(SpacecraftKind kind)
    {
        IsBusy = true;
        switch (kind)
        {
            case SpacecraftKind.Ufo:
                yield return SummonUfo();
                break;
            case SpacecraftKind.OrbitalCannon:
                yield return SummonOrbitalCannon();
                break;
            case SpacecraftKind.FighterWing:
                yield return SummonFighterWing();
                break;
            case SpacecraftKind.Battleship:
                yield return SummonBattleship();
                break;
            case SpacecraftKind.PlanetKiller:
                yield return SummonPlanetKiller();
                break;
            case SpacecraftKind.VonNeumannProbe:
                yield return SummonVonNeumann();
                break;
        }
        IsBusy = false;
    }

    Vector3 FaceDir()
    {
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

    static GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, float emission = 0.4f)
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

    IEnumerator OrbitSpin(Transform tf, Vector3 center, Vector3 axis, float degPerSec, float life, System.Action onTick = null)
    {
        float t = 0f;
        while (t < life && tf != null)
        {
            t += Time.deltaTime;
            tf.RotateAround(center, axis, degPerSec * Time.deltaTime);
            onTick?.Invoke();
            yield return null;
        }
    }

    IEnumerator SummonUfo()
    {
        Vector3 dir = FaceDir();
        var ufo = Prim(PrimitiveType.Sphere, "UFO", OrbitPos(dir, 1.55f), new Vector3(0.55f, 0.12f, 0.55f),
            new Color(0.75f, 0.85f, 0.95f), 1.2f);
        var dome = Prim(PrimitiveType.Sphere, "Dome", ufo.transform.position + Vector3.up * 0.06f,
            new Vector3(0.28f, 0.18f, 0.28f), new Color(0.4f, 1f, 0.7f), 2f);
        dome.transform.SetParent(ufo.transform, true);

        // tractor beam
        var beam = Prim(PrimitiveType.Cylinder, "Beam", ufo.transform.position,
            new Vector3(0.08f, earth.Radius * 0.35f, 0.08f), new Color(0.3f, 1f, 0.5f, 1f), 2.5f);
        beam.transform.SetParent(ufo.transform, true);
        AlignBeam(beam.transform, ufo.transform.position, earth.transform.position);

        Vector3 axis = Vector3.Cross(dir, Vector3.up).normalized;
        if (axis.sqrMagnitude < 1e-4f)
            axis = Vector3.right;

        float life = 7f;
        float t = 0f;
        while (t < life)
        {
            t += Time.deltaTime;
            ufo.transform.RotateAround(earth.transform.position, axis, 28f * Time.deltaTime);
            AlignBeam(beam.transform, ufo.transform.position, earth.transform.position);
            if (Time.frameCount % 12 == 0)
                EarthCraterDeform.Ensure(earth)?.Dig(earth.transform.position +
                    (ufo.transform.position - earth.transform.position).normalized * earth.Radius, 0.06f, 0.02f, false);
            yield return null;
        }

        Object.Destroy(ufo);
    }

    static void AlignBeam(Transform beam, Vector3 from, Vector3 to)
    {
        Vector3 mid = (from + to) * 0.5f;
        float len = Vector3.Distance(from, to) * 0.5f;
        beam.position = mid;
        beam.up = (from - to).normalized;
        var s = beam.localScale;
        s.y = len;
        beam.localScale = s;
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
        while (t < life)
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

        Object.Destroy(gun);
    }

    IEnumerator FireLaser(Vector3 from, Vector3 to, Color color, float life)
    {
        var beam = Prim(PrimitiveType.Cylinder, "Laser", from, Vector3.one, color, 3f);
        AlignBeam(beam.transform, from, to);
        float t = 0f;
        while (t < life)
        {
            t += Time.deltaTime;
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
        Object.Destroy(beam);
    }

    IEnumerator SummonFighterWing()
    {
        Vector3 dir = FaceDir();
        var root = new GameObject("FighterWing");
        root.transform.position = earth.transform.position;
        var fighters = new Transform[6];
        for (int i = 0; i < fighters.Length; i++)
        {
            float ang = i / (float)fighters.Length * Mathf.PI * 2f;
            Vector3 side = new Vector3(Mathf.Cos(ang), 0.15f * Mathf.Sin(ang * 2f), Mathf.Sin(ang));
            Vector3 p = OrbitPos((dir + side * 0.4f).normalized, 1.45f + (i % 3) * 0.08f);
            var f = Prim(PrimitiveType.Cube, "Fighter", p, new Vector3(0.12f, 0.04f, 0.22f),
                new Color(0.9f, 0.55f, 0.2f), 0.8f);
            f.transform.SetParent(root.transform, true);
            f.transform.rotation = Quaternion.LookRotation(earth.transform.position - p);
            fighters[i] = f.transform;
        }

        float life = 7f;
        float t = 0f;
        float nextPass = 0.8f;
        while (t < life)
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

        Object.Destroy(root);
    }

    IEnumerator SummonBattleship()
    {
        Vector3 dir = FaceDir();
        Vector3 pos = OrbitPos(dir, 1.85f);
        var ship = Prim(PrimitiveType.Cube, "Battleship", pos, new Vector3(1.4f, 0.28f, 0.45f),
            new Color(0.25f, 0.28f, 0.32f), 0.2f);
        ship.transform.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up));

        var bridge = Prim(PrimitiveType.Cube, "Bridge", pos + Vector3.up * 0.18f,
            new Vector3(0.35f, 0.18f, 0.22f), new Color(0.4f, 0.45f, 0.5f), 0.5f);
        bridge.transform.SetParent(ship.transform, true);

        float life = 9f;
        float t = 0f;
        float next = 1f;
        Vector3 axis = Vector3.up;
        while (t < life)
        {
            t += Time.deltaTime;
            ship.transform.RotateAround(earth.transform.position, axis, 10f * Time.deltaTime);
            if (t >= next)
            {
                next = t + 1.4f;
                Vector3 hitDir = (earth.transform.position - ship.transform.position).normalized * -1f;
                // toward earth
                hitDir = (ship.transform.position - earth.transform.position).normalized;
                Vector3 hit = earth.transform.position + hitDir * earth.Radius;
                StartCoroutine(FireLaser(ship.transform.position, hit, new Color(1f, 0.8f, 0.2f), 0.4f));
                NuclearBlast.Play(earth, hit, hitDir, 1.0f);
            }
            yield return null;
        }

        Object.Destroy(ship);
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

        // charge then fire
        float charge = 2.2f;
        float t = 0f;
        while (t < charge)
        {
            t += Time.deltaTime;
            float pulse = 0.35f + 0.15f * Mathf.Sin(t * 12f);
            muzzle.transform.localScale = Vector3.one * pulse;
            yield return null;
        }

        Vector3 hitDir = (ship.transform.position - earth.transform.position).normalized;
        Vector3 hit = earth.transform.position + hitDir * earth.Radius;
        StartCoroutine(FireLaser(muzzle.transform.position, hit, new Color(1f, 0.15f, 0.05f), 1.2f));
        NuclearBlast.Play(earth, hit, hitDir, 2.2f);
        EarthCraterDeform.Ensure(earth)?.Dig(hit, 0.22f, 0.1f, true);
        CameraShake.Shake(0.25f, 0.45f);

        yield return new WaitForSecondsRealtime(2.5f);
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
        while (t < life)
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

        Object.Destroy(root);
    }
}
