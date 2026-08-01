using UnityEngine;

/// <summary>
/// 운석. 목표 지점까지 낙하 후 지구에 임팩트를 적용한다.
/// </summary>
public class MeteorProjectile : MonoBehaviour
{
    [SerializeField] float speed = 32f;
    [SerializeField] float damage = 8f;
    [SerializeField] float impactFlashIntensity = 12f;
    [SerializeField] float impactFlashDuration = 0.4f;
    [SerializeField] GameObject impactVfxPrefab;

    EarthPlanet _earth;
    Vector3 _impactPoint;
    Vector3 _impactNormal;
    bool _launched;
    bool _impacted;
    float _eta;

    public float CountdownSeconds => Mathf.Max(0f, _eta);
    public bool HasImpacted => _impacted;

    public void Launch(EarthPlanet earth, Vector3 impactPoint, Vector3 impactNormal, float? overrideDamage = null)
    {
        _earth = earth;
        _impactPoint = impactPoint;
        _impactNormal = impactNormal.normalized;
        if (overrideDamage.HasValue) damage = overrideDamage.Value;

        if (GetComponent<MeteorTrail>() == null)
            gameObject.AddComponent<MeteorTrail>();

        Vector3 start = impactPoint + _impactNormal * (earth.Radius * 2.6f);
        transform.position = start;
        transform.rotation = Quaternion.LookRotation(-_impactNormal);
        _launched = true;
        _impacted = false;
        _eta = Vector3.Distance(start, impactPoint) / Mathf.Max(0.01f, speed);
    }

    void Update()
    {
        if (!_launched || _impacted) return;

        float step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, _impactPoint, step);
        _eta = Vector3.Distance(transform.position, _impactPoint) / Mathf.Max(0.01f, speed);

        if ((transform.position - _impactPoint).sqrMagnitude <= 0.0025f)
            Impact();
    }

    void Impact()
    {
        if (_impacted) return;
        _impacted = true;
        _eta = 0f;

        if (_earth != null)
            _earth.ApplyImpact(_impactPoint, damage);

        SpawnImpactFx();
        Destroy(gameObject, 0.05f);
    }

    void SpawnImpactFx()
    {
        if (impactVfxPrefab != null)
        {
            var fx = Instantiate(impactVfxPrefab, _impactPoint, Quaternion.LookRotation(_impactNormal));
            Destroy(fx, 4f);
            return;
        }

        float earthSize = _earth != null ? _earth.Radius : 2.5f;

        var lightGo = new GameObject("ImpactFlash");
        lightGo.transform.position = _impactPoint + _impactNormal * 0.4f;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.65f, 0.25f);
        light.intensity = impactFlashIntensity;
        light.range = earthSize * 3f;
        lightGo.AddComponent<ImpactFlashFade>().Begin(impactFlashDuration);

        ImpactShockwave.Spawn(_impactPoint, _impactNormal, earthSize * 0.55f);

        if (_earth != null)
            ImpactCrater.Spawn(_earth.transform, _impactPoint, _impactNormal, Random.Range(0.35f, 0.7f));

        // 작은 타격도 축소판 시네마틱 폭발
        CinematicExplosion.Play(_impactPoint, _impactNormal, 0.55f);

        for (int i = 0; i < 10; i++)
        {
            var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "Debris";
            chunk.transform.position = _impactPoint + _impactNormal * 0.15f;
            chunk.transform.localScale = Vector3.one * Random.Range(0.06f, 0.16f);
            var rend = chunk.GetComponent<Renderer>();
            bool lava = Random.value > 0.35f;
            var col = lava ? new Color(0.55f, 0.22f, 0.08f) : new Color(0.32f, 0.26f, 0.2f);
            rend.material = RuntimeMaterial.Opaque(col, lava ? 0.35f : 0f);

            var rb = chunk.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.useGravity = false;
            Vector3 dir = (_impactNormal + Random.insideUnitSphere * 0.95f).normalized;
            rb.AddForce(dir * Random.Range(5f, 14f), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);
            Destroy(chunk.GetComponent<Collider>());
            Destroy(chunk, 3.5f);
        }
    }
}
