using UnityEngine;

/// <summary>밈 유닛 공통 — 클릭 지점(로컬 방향)을 따라 지구가 돌아도 조준 유지.</summary>
public abstract class MemeUnitBase : MonoBehaviour
{
    public static int LiveCount { get; private set; }

    protected EarthPlanet earth;
    protected Vector3 localAim;
    protected float life = 22f;
    protected float age;
    protected float nextAttack = 1f;
    protected int attackCount;
    bool exiting;
    float exitAge;
    Vector3 exitStartScale;
    Vector3 exitDir;

    const float ExitDuration = 0.42f;

    public void Init(EarthPlanet planet, Vector3 localDirection)
    {
        earth = planet;
        localAim = localDirection.normalized;
        age = 0f;
        attackCount = 0;
        nextAttack = FirstAttackDelay();
        OnSpawned();
    }

    protected abstract float FirstAttackDelay();
    protected abstract float AttackInterval();
    protected abstract int MaxAttacks();
    protected abstract void OnSpawned();
    protected abstract void TickOrbit(float dt);
    protected abstract void DoAttack();

    /// <summary>실제 공격이 land했을 때 호출 — N회 후 퇴장.</summary>
    protected void RegisterAttack()
    {
        attackCount++;
        if (attackCount >= MaxAttacks())
            BeginExit();
    }

    protected void BeginExit()
    {
        if (exiting)
            return;
        exiting = true;
        exitAge = 0f;
        exitStartScale = transform.localScale;
        exitDir = AimDirWorld();
        if (exitDir.sqrMagnitude < 1e-6f)
            exitDir = Vector3.up;
    }

    void TickExit(float dt)
    {
        exitAge += dt;
        float u = Mathf.Clamp01(exitAge / ExitDuration);
        float shrink = 1f - u * u;
        transform.localScale = exitStartScale * shrink;
        transform.position += exitDir * (earth.Radius * 0.32f * dt);
        if (exitAge >= ExitDuration)
            Destroy(gameObject);
    }

    protected Vector3 AimDirWorld() =>
        earth.transform.TransformDirection(localAim).normalized;

    protected Vector3 SurfacePoint() =>
        earth.transform.position + AimDirWorld() * earth.Radius;

    protected Vector3 OrbitPoint(float altMul, float side = 0f)
    {
        Vector3 dir = AimDirWorld();
        Vector3 sideAxis = Vector3.Cross(dir, Vector3.up);
        if (sideAxis.sqrMagnitude < 1e-4f)
            sideAxis = Vector3.Cross(dir, Vector3.right);
        sideAxis.Normalize();
        Vector3 p = (dir + sideAxis * side).normalized;
        return earth.transform.position + p * (earth.Radius * altMul);
    }

    protected void FaceTangent(Vector3 up)
    {
        Vector3 tangent = Vector3.Cross(up, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(up, Vector3.right);
        if (tangent.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.LookRotation(tangent.normalized, up);
    }

    protected virtual void OnUnitSpawned() { }
    protected virtual void OnUnitDestroyed() { }

    void OnEnable()
    {
        LiveCount++;
        OnUnitSpawned();
    }

    void OnDestroy()
    {
        LiveCount = Mathf.Max(0, LiveCount - 1);
        OnUnitDestroyed();
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

        if (exiting)
        {
            TickExit(Time.deltaTime);
            return;
        }

        TickOrbit(Time.deltaTime);

        if (age < nextAttack)
            return;
        nextAttack = age + AttackInterval();
        DoAttack();
    }
}

public class MemePepeUnit : MemeUnitBase
{
    enum Phase { Orbit, Lunge, Return }
    Phase phase = Phase.Orbit;
    float moveT;
    Vector3 lungeFrom;
    Vector3 lungeTo;

    protected override float FirstAttackDelay() => 0.9f;
    protected override float AttackInterval() => 2.2f;
    protected override int MaxAttacks() => 3;

    protected override void OnSpawned()
    {
        transform.position = OrbitPoint(1.62f, 0.15f);
        FaceTangent(AimDirWorld());
    }

    protected override void TickOrbit(float dt)
    {
        if (phase == Phase.Orbit)
        {
            transform.position = OrbitPoint(1.62f, 0.15f);
            FaceTangent(AimDirWorld());
            return;
        }

        moveT += dt * (phase == Phase.Lunge ? 7f : 3.5f);
        float u = Mathf.Clamp01(moveT);
        if (phase == Phase.Lunge)
        {
            transform.position = Vector3.Lerp(lungeFrom, lungeTo, u * u);
            if (u >= 1f)
            {
                PunchImpact();
                phase = Phase.Return;
                moveT = 0f;
                lungeFrom = transform.position;
                lungeTo = OrbitPoint(1.62f, 0.15f);
            }
        }
        else
        {
            transform.position = Vector3.Lerp(lungeFrom, lungeTo, u);
            if (u >= 1f)
                phase = Phase.Orbit;
        }
    }

    protected override void DoAttack()
    {
        if (phase != Phase.Orbit)
            return;

        float R = earth.Radius;
        Vector3 hit = SurfacePoint();
        Vector3 n = AimDirWorld();
        lungeFrom = OrbitPoint(1.62f, 0.15f);
        lungeTo = hit + n * (R * 0.06f);
        phase = Phase.Lunge;
        moveT = 0f;
    }

    void PunchImpact()
    {
        Vector3 hit = SurfacePoint();
        Vector3 n = AimDirWorld();
        MemeAttackSystem.LightHit(earth, hit, n, 0.06f, 0.028f, 0.03f, 0.52f);
        CameraShake.Shake(0.18f, 0.16f);
        if (attackCount % 2 == 0)
            MemeCaption.Spawn(hit + n * (earth.Radius * 0.18f), "bonk", new Color(0.4f, 1f, 0.35f), earth.Radius * 0.12f);
        MemeAttackSystem.ApplyCasualtiesStatic(0.001f);
        RegisterAttack();
    }
}

public class MemeCatUnit : MemeUnitBase
{
    public static int ActiveCount { get; private set; }

    Vector3 scratchTangent;
    float scratchPhase;
    float lungeT;
    float digRadius;
    float digDepth;
    int clawFlip;

    const float HoverLift = 1.08f;
    const float ClawLift = 0.92f;

    protected override float FirstAttackDelay() => 0.35f;
    protected override float AttackInterval() => 0.52f;
    protected override int MaxAttacks() => 7;

    protected override void OnSpawned()
    {
        digRadius = 0.038f;
        digDepth = 0.012f;

        Vector3 n = AimDirWorld();
        scratchTangent = Vector3.Cross(n, Vector3.up);
        if (scratchTangent.sqrMagnitude < 1e-4f)
            scratchTangent = Vector3.Cross(n, Vector3.right);
        scratchTangent.Normalize();
        PlaceOnTarget(HoverLift);
    }

    protected override void OnUnitSpawned() => ActiveCount++;
    protected override void OnUnitDestroyed() => ActiveCount = Mathf.Max(0, ActiveCount - 1);

    float BodyRadius() => transform.localScale.x * 0.5f;

    void PlaceOnTarget(float liftMul)
    {
        Vector3 hit = SurfacePoint();
        Vector3 n = AimDirWorld();
        transform.position = hit + n * (BodyRadius() * liftMul);
        transform.rotation = Quaternion.LookRotation(scratchTangent, n);
    }

    protected override void TickOrbit(float dt)
    {
        Vector3 hit = SurfacePoint();
        Vector3 n = AimDirWorld();
        scratchPhase += dt * 11f;
        float bodyR = BodyRadius();

        if (lungeT > 0f)
        {
            lungeT -= dt;
            float u = 1f - Mathf.Clamp01(lungeT / 0.13f);
            float lift = Mathf.Lerp(HoverLift, ClawLift, u * u);
            transform.position = hit + n * (bodyR * lift);
        }
        else
        {
            float wiggle = Mathf.Sin(scratchPhase) * bodyR * 0.05f;
            transform.position = hit + n * (bodyR * HoverLift + wiggle);
        }

        transform.rotation = Quaternion.LookRotation(scratchTangent, n);
        transform.Rotate(n, Mathf.Sin(scratchPhase * 1.6f) * 10f, Space.World);
    }

    protected override void DoAttack()
    {
        Vector3 hit = SurfacePoint();
        Vector3 n = AimDirWorld();
        float R = earth.Radius;
        int seed = GetInstanceID() + attackCount * 41;

        lungeT = 0.13f;
        MemeAttackSystem.CatScratch(earth, hit, n, ref digRadius, ref digDepth, attackCount, seed);

        clawFlip ^= 1;
        float slashAng = clawFlip == 0 ? -22f : 20f;
        Vector3 slashDir = (Quaternion.AngleAxis(slashAng, n) * scratchTangent).normalized;
        var claw = MemeVisuals.CreateClawSwipe(R * (0.16f + digRadius * 0.35f), R * 0.024f);
        claw.transform.position = hit + slashDir * (R * 0.02f) + n * (R * 0.006f);
        claw.transform.rotation = Quaternion.LookRotation(slashDir, n);

        if (attackCount % 2 == 0)
            CameraShake.Shake(0.06f, 0.045f);
        if (attackCount % 4 == 0)
            MemeAttackSystem.ApplyCasualtiesStatic(0.00035f);
        RegisterAttack();
    }
}

public class MemeSharkUnit : MemeUnitBase
{
    Vector3 runAxis;
    float runSpeed = 38f;

    protected override float FirstAttackDelay() => 1f;
    protected override float AttackInterval() => 2.2f;
    protected override int MaxAttacks() => 4;

    protected override void OnSpawned()
    {
        transform.position = OrbitPoint(1.38f, 0.35f);
        runAxis = Vector3.Cross(AimDirWorld(), Vector3.up);
        if (runAxis.sqrMagnitude < 1e-4f)
            runAxis = Vector3.right;
        runAxis.Normalize();
        FaceTangent(AimDirWorld());
    }

    protected override void TickOrbit(float dt)
    {
        transform.RotateAround(earth.transform.position, runAxis, runSpeed * dt);
        Vector3 vel = Vector3.Cross(runAxis, transform.position - earth.transform.position);
        if (vel.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.LookRotation(vel.normalized, AimDirWorld());
    }

    protected override void DoAttack()
    {
        Vector3 hit = SurfacePoint();
        Vector3 n = AimDirWorld();
        MemeAttackSystem.LightHit(earth, hit, n, 0.055f, 0.025f, 0.028f, 0.48f);

        if (attackCount % 3 == 0)
            EarthSurfaceScorch.Ensure(earth)?.PaintSneakerPrint(hit, 0.05f);

        CameraShake.Shake(0.14f, 0.12f);
        if (attackCount % 2 == 0)
            MemeCaption.Spawn(hit + n * (earth.Radius * 0.14f), "SPLAT", new Color(0.25f, 0.65f, 1f), earth.Radius * 0.1f);
        MemeAttackSystem.ApplyCasualtiesStatic(0.0008f);
        RegisterAttack();
    }
}

public class MemeCowUnit : MemeUnitBase
{
    float wobble;
    int activeBombs;

    protected override float FirstAttackDelay() => 1.2f;
    protected override float AttackInterval() => 2.8f;
    protected override int MaxAttacks() => 3;

    protected override void OnSpawned()
    {
        transform.position = OrbitPoint(1.72f, -0.2f);
        FaceTangent(AimDirWorld());
    }

    protected override void TickOrbit(float dt)
    {
        wobble += dt * 1.4f;
        float side = Mathf.Sin(wobble) * 0.22f;
        transform.position = OrbitPoint(1.72f, side);
        transform.Rotate(Vector3.forward, 18f * dt, Space.Self);
        FaceTangent(AimDirWorld());
    }

    protected override void DoAttack()
    {
        if (activeBombs >= 2)
            return;

        float R = earth.Radius;
        Vector3 n = AimDirWorld();
        var bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bomb.name = "MilkBomb";
        Object.Destroy(bomb.GetComponent<Collider>());
        bomb.transform.position = transform.position;
        bomb.transform.localScale = Vector3.one * (R * 0.06f);
        bomb.GetComponent<Renderer>().sharedMaterial = MemeVisuals.SharedMilkMat();
        var mb = bomb.AddComponent<MemeMilkBomb>();
        mb.Launch(earth, localAim, transform.position, () => activeBombs--);
        activeBombs++;

        if (attackCount % 2 == 0)
            MemeCaption.Spawn(transform.position + n * (R * 0.06f), "moo", new Color(0.95f, 0.95f, 0.88f), R * 0.08f);
        RegisterAttack();
    }
}

public class MemeMilkBomb : MonoBehaviour
{
    EarthPlanet earth;
    Vector3 localAim;
    Vector3 velocity;
    float t;
    System.Action onDone;

    public void Launch(EarthPlanet planet, Vector3 localDir, Vector3 from, System.Action done)
    {
        earth = planet;
        localAim = localDir.normalized;
        onDone = done;
        transform.position = from;
        velocity = planet.transform.TransformDirection(localAim).normalized * (planet.Radius * 0.55f);
    }

    void OnDestroy()
    {
        onDone?.Invoke();
        onDone = null;
    }

    void Update()
    {
        if (earth == null)
        {
            Destroy(gameObject);
            return;
        }

        t += Time.deltaTime;
        Vector3 target = earth.transform.position
            + earth.transform.TransformDirection(localAim).normalized * earth.Radius;
        Vector3 to = target - transform.position;
        velocity += to.normalized * (earth.Radius * 1.6f * Time.deltaTime);
        velocity *= 0.988f;
        transform.position += velocity * Time.deltaTime;

        if (to.magnitude < earth.Radius * 0.1f || t > 3.5f)
        {
            Vector3 n = earth.transform.TransformDirection(localAim).normalized;
            MemeAttackSystem.LightHit(earth, target, n, 0.05f, 0.022f, 0.025f, 0.42f);
            CameraShake.Shake(0.1f, 0.08f);
            MemeAttackSystem.ApplyCasualtiesStatic(0.0008f);
            onDone = null;
            Destroy(gameObject);
        }
    }
}
