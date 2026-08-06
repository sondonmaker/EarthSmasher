using System.Collections;
using UnityEngine;

/// <summary>거대 트로이 목마 옆/앞에서 튀어나와 지구 표면을 공격하는 chibi 병사.</summary>
public class MemeTrojanSoldierRaid : MonoBehaviour
{
    EarthPlanet _earth;
    Vector3 _emergeFrom;
    Vector3 _localAim;
    Vector3 _fullScale;
    float _delay;

    public void Launch(EarthPlanet earth, Vector3 emergeFrom, Vector3 localAim, float delay)
    {
        _earth = earth;
        _emergeFrom = emergeFrom;
        _localAim = localAim.normalized;
        _delay = delay;
        _fullScale = transform.localScale;
        transform.position = emergeFrom;
        transform.localScale = _fullScale * 0.5f;
        var billboard = GetComponent<MemeBillboard>();
        billboard?.FaceTowardEarth(earth);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(_delay);
        if (_earth == null)
        {
            Destroy(gameObject);
            yield break;
        }

        float R = _earth.Radius;
        Vector3 center = _earth.transform.position;
        Vector3 aimN = _earth.transform.TransformDirection(_localAim).normalized;
        Vector3 targetHit = center + aimN * R;
        Vector3 targetPad = targetHit + aimN * (R * 0.022f);

        Vector3 tangent = Vector3.Cross(aimN, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(aimN, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(aimN, tangent);

        Vector3 toEarth = (targetHit - _emergeFrom).normalized;
        Vector3 burstOut = _emergeFrom
            + toEarth * (R * 0.14f)
            + tangent * Random.Range(-R * 0.06f, R * 0.06f)
            + bitangent * Random.Range(-R * 0.04f, R * 0.04f);

        float emergeDur = 0.2f;
        float emergeT = 0f;
        while (emergeT < emergeDur)
        {
            emergeT += Time.deltaTime;
            float u = emergeT / emergeDur;
            float ease = u * u * (3f - 2f * u);
            transform.position = Vector3.Lerp(_emergeFrom, burstOut, ease);
            transform.localScale = _fullScale * Mathf.Lerp(0.5f, 1f, ease);
            yield return null;
        }

        Vector3 dashFrom = transform.position;
        float dashDur = Mathf.Clamp(Vector3.Distance(dashFrom, targetPad) / (R * 1.1f), 0.32f, 0.85f);
        float dashT = 0f;
        while (dashT < dashDur)
        {
            dashT += Time.deltaTime;
            float u = dashT / dashDur;
            float ease = u * u * (3f - 2f * u);
            transform.position = Vector3.Lerp(dashFrom, targetPad, ease);
            yield return null;
        }

        for (int h = 0; h < 4; h++)
        {
            Vector3 jitter = tangent * Random.Range(-0.045f, 0.045f) * R
                + bitangent * Random.Range(-0.035f, 0.035f) * R;
            Vector3 hN = (targetHit + jitter - center).normalized;
            Vector3 impact = center + hN * R;
            MemeAttackSystem.LightHit(_earth, impact, hN, 0.024f, 0.01f, 0.02f, 0.45f);
            MemeAttackSystem.SpawnFlash(impact, hN, R * 0.035f, new Color(1f, 0.72f, 0.28f, 0.55f));
            CameraShake.Shake(0.04f, 0.03f);
            yield return new WaitForSeconds(0.055f);
        }

        float fade = 0.2f;
        float fadeT = 0f;
        Vector3 fadeScale = transform.localScale;
        while (fadeT < fade)
        {
            fadeT += Time.deltaTime;
            float u = fadeT / fade;
            transform.localScale = fadeScale * (1f - u);
            yield return null;
        }

        Destroy(gameObject);
    }
}
