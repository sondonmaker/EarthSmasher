using UnityEngine;

/// <summary>카메라를 향하는 밈 빌보드.</summary>
public class MemeBillboard : MonoBehaviour
{
    EarthPlanet _faceEarth;
    bool _flipTowardEarth;

    /// <summary>스프라이트 기본 방향(오른쪽)을 지구 중심 쪽으로 좌우 반전.</summary>
    public void FaceTowardEarth(EarthPlanet earth)
    {
        _faceEarth = earth;
        _flipTowardEarth = earth != null;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        if (!_flipTowardEarth || _faceEarth == null)
            return;

        Vector3 toEarth = _faceEarth.transform.position - transform.position;
        if (toEarth.sqrMagnitude < 1e-6f)
            return;

        bool flip = Vector3.Dot(toEarth.normalized, cam.transform.right) < 0f;
        var s = transform.localScale;
        float ax = Mathf.Abs(s.x);
        transform.localScale = new Vector3(flip ? -ax : ax, s.y, s.z);
    }
}
