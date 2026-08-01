using UnityEngine;

/// <summary>
/// 임팩트 시 카메라 흔들림.
/// </summary>
public class CameraShake : MonoBehaviour
{
    static CameraShake _instance;
    float _trauma;
    float _timer;
    Vector3 _originLocal;
    bool _hasOrigin;

    public static void Shake(float duration, float strength)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var shake = cam.GetComponent<CameraShake>();
        if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
        shake.Add(duration, strength);
    }

    void Add(float duration, float strength)
    {
        _trauma = Mathf.Max(_trauma, strength);
        _timer = Mathf.Max(_timer, duration);
        if (!_hasOrigin)
        {
            _originLocal = transform.localPosition;
            _hasOrigin = true;
        }
    }

    void LateUpdate()
    {
        if (_timer <= 0f)
        {
            if (_hasOrigin)
            {
                // OrbitCamera가 위치를 매 프레임 덮어쓰므로 복원 불필요에 가깝지만 trauma만 리셋
                _hasOrigin = false;
            }
            return;
        }

        _timer -= Time.deltaTime;
        float t = Mathf.Clamp01(_timer);
        float mag = _trauma * t;
        Vector3 offset = Random.insideUnitSphere * mag * 0.35f;
        transform.position += offset;
    }
}
