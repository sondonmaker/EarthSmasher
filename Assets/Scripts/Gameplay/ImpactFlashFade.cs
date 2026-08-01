using UnityEngine;

public class ImpactFlashFade : MonoBehaviour
{
    Light _light;
    float _duration;
    float _startIntensity;
    float _t;

    public void Begin(float duration)
    {
        _light = GetComponent<Light>();
        _duration = Mathf.Max(0.05f, duration);
        _startIntensity = _light != null ? _light.intensity : 1f;
        _t = 0f;
        Destroy(gameObject, _duration + 0.05f);
    }

    void Update()
    {
        if (_light == null) return;
        _t += Time.deltaTime;
        float k = 1f - Mathf.Clamp01(_t / _duration);
        _light.intensity = _startIntensity * (k * k);
    }
}
