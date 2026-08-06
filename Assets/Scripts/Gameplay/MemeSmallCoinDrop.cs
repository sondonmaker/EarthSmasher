using UnityEngine;

/// <summary>작은 코인 낙하 — Burn/LightHit만 (펭구 샤워용).</summary>
public class MemeSmallCoinDrop : MonoBehaviour
{
    EarthPlanet _earth;
    Vector3 _impact;
    Vector3 _normal;
    Vector3 _startPos;
    float _speed;
    bool _lightOnly;
    Quaternion _tiltStart;
    System.Action _onDone;
    bool _done;

    public void Launch(
        EarthPlanet earth, Vector3 impact, Vector3 normal, float speed, float distMul,
        bool lightOnly, System.Action onDone)
    {
        _earth = earth;
        _impact = impact;
        _normal = normal.normalized;
        _speed = speed;
        _lightOnly = lightOnly;
        _onDone = onDone;

        _startPos = impact + _normal * (earth.Radius * distMul);
        transform.position = _startPos;

        Quaternion faceEarth = Quaternion.LookRotation(-_normal, Vector3.up);
        _tiltStart = faceEarth * Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
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
        Quaternion faceFlat = Quaternion.LookRotation(-_normal, Vector3.up);
        transform.rotation = Quaternion.Slerp(_tiltStart, faceFlat, u * u * u);

        if ((transform.position - _impact).sqrMagnitude < 0.015f)
            Hit();
    }

    void Hit()
    {
        if (_done)
            return;
        _done = true;

        if (_earth != null)
        {
            if (_lightOnly)
                MemeAttackSystem.LightHit(_earth, _impact, _normal, 0.035f, 0.012f, 0.022f, 0.35f);
            else
                EarthSurfaceScorch.Ensure(_earth)?.BurnAt(_impact, 0.02f, 0.32f);
        }

        _onDone?.Invoke();
        Destroy(gameObject);
    }
}
