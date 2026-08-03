using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 큰 운석이 멀리서 다가와 지구에 충돌하는 시네마틱 스트라이크.
/// Space / 우클릭 / 두 손가락 탭으로 발사.
/// </summary>
public class BigMeteorStrike : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] EarthPlanet earth;
    [SerializeField] float meteorScale = 1.35f;
    [SerializeField] float approachDistanceMul = 4.5f;
    [SerializeField] float speed = 9.5f;
    [SerializeField] float damage = 22f;
    [SerializeField] float explosionPower = 1.6f;
    [SerializeField] float cooldown = 2.5f;

    float _readyAt;

    public void Configure(Camera camera, EarthPlanet planet)
    {
        cam = camera;
        earth = planet;
    }

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (earth == null) earth = FindObjectOfType<EarthPlanet>();
    }

    public void FireRandom()
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null || Time.time < _readyAt)
            return;
        if (cam == null)
            cam = Camera.main;

        Vector3 dir = cam != null
            ? (cam.transform.position - earth.transform.position).normalized
            : Random.onUnitSphere;
        if (dir.sqrMagnitude < 1e-6f)
            dir = Random.onUnitSphere;
        Vector3 impact = earth.transform.position + dir * earth.Radius;
        LaunchBig(impact, dir);
    }

    void Update()
    {
        if (earth == null) return;
        if (EarthLayerToolbar.BlocksGameplayInput || ZoomUiBlocker.BlocksGameplay || WorldStatusHud.BlocksGameplay) return;
        if (WeaponRailPanel.BlocksGameplay) return;
        if (WeaponRailPanel.IsArmed) return;
        if (DisasterUiGate.ModalOpen) return;
        if (Time.time < _readyAt) return;
        if (!WantsBigStrike()) return;

        Vector3 dir = (cam.transform.position - earth.transform.position).normalized;
        Vector3 impact = earth.transform.position + dir * earth.Radius;
        LaunchBig(impact, dir);
    }

    bool WantsBigStrike()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame) return true;

        return false;
    }

    void LaunchBig(Vector3 impactPoint, Vector3 normal)
    {
        _readyAt = Time.time + cooldown;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BigMeteor";
        go.transform.localScale = Vector3.one * meteorScale;
        Destroy(go.GetComponent<Collider>());

        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.35f, 0.22f, 0.14f), 0.8f);

        for (int i = 0; i < 8; i++)
        {
            var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = "LavaSpot";
            Destroy(blob.GetComponent<Collider>());
            blob.transform.SetParent(go.transform, false);
            blob.transform.localPosition = Random.onUnitSphere * 0.48f;
            blob.transform.localScale = Vector3.one * Random.Range(0.12f, 0.28f);
            blob.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
                new Color(1f, 0.35f, 0.05f), 3.5f);
        }

        go.AddComponent<MeteorTrail>();

        var body = go.AddComponent<BigMeteorBody>();
        body.Launch(earth, impactPoint, normal, speed, approachDistanceMul, damage, explosionPower, null);
    }
}

/// <summary>대형 운석 이동/충돌 처리</summary>
public class BigMeteorBody : MonoBehaviour
{
    EarthPlanet _earth;
    Vector3 _impact;
    Vector3 _normal;
    float _speed;
    float _damage;
    float _power;
    System.Action _onDone;
    bool _done;

    public void Launch(
        EarthPlanet earth, Vector3 impact, Vector3 normal,
        float speed, float distMul, float damage, float power, System.Action onDone)
    {
        _earth = earth;
        _impact = impact;
        _normal = normal.normalized;
        _speed = speed;
        _damage = damage;
        _power = power;
        _onDone = onDone;

        transform.position = impact + _normal * (earth.Radius * distMul);
        transform.rotation = Quaternion.LookRotation(-_normal);
    }

    void Update()
    {
        if (_done) return;
        transform.position = Vector3.MoveTowards(transform.position, _impact, _speed * Time.deltaTime);
        transform.Rotate(Vector3.right, 40f * Time.deltaTime, Space.Self);

        if ((transform.position - _impact).sqrMagnitude < 0.01f)
            Hit();
    }

    void Hit()
    {
        if (_done) return;
        _done = true;

        if (_earth != null)
            _earth.ApplyImpact(_impact, _damage);

        CinematicExplosion.Play(_impact, _normal, _power);
        ImpactShockwave.Spawn(_impact, _normal, _earth != null ? _earth.Radius * 0.8f : 2f);
        if (_earth != null)
        {
            ImpactCrater.Spawn(_earth.transform, _impact, _normal, 1.35f);
            var scorch = EarthSurfaceScorch.Ensure(_earth);
            if (scorch != null)
                scorch.PaintMoltenFissures(_impact, 0.1f, 0.65f, 2.3f, 12);
            // 짧은 발광 리본 웨이브 1회
            _earth.StartCoroutine(MoltenCrackFx.Play(_earth, _impact, 0.1f));
        }

        _onDone?.Invoke();
        Destroy(gameObject);
    }
}
