using UnityEngine;

/// <summary>도지 코인 — 동전 면이 지구를 향해 떨어져 착지.</summary>
public class MemeDogeCoinBody : MonoBehaviour
{
    EarthPlanet _earth;
    Vector3 _impact;
    Vector3 _normal;
    Vector3 _startPos;
    float _speed;
    float _damage;
    float _power;
    Quaternion _tiltStart;
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

        _startPos = impact + _normal * (earth.Radius * distMul);
        transform.position = _startPos;

        Quaternion faceEarth = Quaternion.LookRotation(-_normal, Vector3.up);
        if (faceEarth == Quaternion.identity && _normal.sqrMagnitude > 0.5f)
            faceEarth = Quaternion.LookRotation(-_normal);
        _tiltStart = faceEarth * Quaternion.Euler(
            Random.Range(-28f, 28f),
            Random.Range(0f, 360f),
            Random.Range(-18f, 18f));
        transform.rotation = _tiltStart;
    }

    void Update()
    {
        if (_done)
            return;

        transform.position = Vector3.MoveTowards(transform.position, _impact, _speed * Time.deltaTime);

        float total = Vector3.Distance(_startPos, _impact);
        float rem = Vector3.Distance(transform.position, _impact);
        float u = total > 1e-4f ? 1f - rem / total : 1f;
        float align = u * u * u;

        Quaternion faceFlat = Quaternion.LookRotation(-_normal, Vector3.up);
        if (faceFlat == Quaternion.identity)
            faceFlat = Quaternion.LookRotation(-_normal);
        transform.rotation = Quaternion.Slerp(_tiltStart, faceFlat, align);

        Vector3 spinAxis = Vector3.Cross(_normal, Vector3.up);
        if (spinAxis.sqrMagnitude < 1e-4f)
            spinAxis = Vector3.Cross(_normal, Vector3.right);
        transform.Rotate(spinAxis.normalized, (90f + 140f * u) * Time.deltaTime, Space.World);

        if ((transform.position - _impact).sqrMagnitude < 0.01f)
            Hit();
    }

    void Hit()
    {
        if (_done)
            return;
        _done = true;

        transform.rotation = Quaternion.LookRotation(-_normal, Vector3.up);
        transform.position = _impact + _normal * (_earth != null ? _earth.Radius * 0.02f : 0.05f);

        if (_earth != null)
            _earth.ApplyImpact(_impact, _damage);

        MemeAttackSystem.MemeBurst(
            _impact,
            _normal,
            _earth != null ? _earth.Radius : 2.5f,
            _power * 0.5f,
            MemeBurstStyle.DogeCoin);
        if (_earth != null)
        {
            ImpactCrater.Spawn(_earth.transform, _impact, _normal, 1.35f);
            var scorch = EarthSurfaceScorch.Ensure(_earth);
            if (scorch != null)
                scorch.PaintMoltenFissures(_impact, 0.1f, 0.65f, 2.3f, 12);
            _earth.StartCoroutine(MoltenCrackFx.Play(_earth, _impact, 0.1f));
        }

        _onDone?.Invoke();
        Destroy(gameObject);
    }
}
