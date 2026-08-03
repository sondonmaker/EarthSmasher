using System;
using UnityEngine;

/// <summary>
/// ICBM: 가느다란 궤적선 + 작은 탄두. 착탄 시 핵폭발.
/// </summary>
public class NuclearMissile : MonoBehaviour
{
    EarthPlanet earth;
    Vector3 startDir;
    Vector3 endDir;
    float radius;
    float loft;
    float duration;
    float power;
    float t;
    bool done;
    Action onImpact;
    LineRenderer path;
    Transform pathRoot;

    public static void Launch(
        EarthPlanet earth,
        float launchLat, float launchLon,
        float targetLat, float targetLon,
        float power,
        float flightSeconds,
        Action onImpact)
    {
        if (earth == null)
            return;

        // 탄두는 거의 점 수준 — 궤적선이 주인공
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "NuclearMissile";
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(earth.transform, false);
        go.transform.localScale = Vector3.one * 0.0045f;

        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(1f, 0.95f, 0.85f), 2.2f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.55f;
        trail.startWidth = 0.008f;
        trail.endWidth = 0.0015f;
        trail.material = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.9f, 0.75f, 0.85f));
        trail.startColor = new Color(1f, 0.95f, 0.85f, 0.9f);
        trail.endColor = new Color(1f, 0.55f, 0.25f, 0f);
        trail.minVertexDistance = 0.01f;
        trail.numCapVertices = 1;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var m = go.AddComponent<NuclearMissile>();
        m.Begin(earth, launchLat, launchLon, targetLat, targetLon, power, flightSeconds, onImpact);
    }

    /// <summary>클릭한 월드 지점으로 ICBM 발사. launch 지점은 목표에서 떨어진 임의 원점.</summary>
    public static void LaunchToWorldPoint(
        EarthPlanet earth,
        Vector3 worldTarget,
        float power,
        float flightSeconds,
        Action onImpact)
    {
        if (earth == null)
            return;

        Vector3 endLocal = earth.transform.InverseTransformPoint(worldTarget);
        if (endLocal.sqrMagnitude < 1e-8f)
            return;
        Vector3 endDir = endLocal.normalized;

        Vector3 axis = Vector3.Cross(endDir, Vector3.up);
        if (axis.sqrMagnitude < 1e-4f)
            axis = Vector3.Cross(endDir, Vector3.right);
        axis.Normalize();
        float arc = Random.Range(75f, 125f);
        Vector3 startDir = (Quaternion.AngleAxis(arc, axis) * endDir).normalized;
        startDir = (Quaternion.AngleAxis(Random.Range(-45f, 45f), endDir) * startDir).normalized;

        EarthGeo.DirectionToLatLon(startDir, out float oLat, out float oLon);
        EarthGeo.DirectionToLatLon(endDir, out float tLat, out float tLon);

        float ang = Vector3.Angle(startDir, endDir);
        float flight = flightSeconds > 0f
            ? flightSeconds
            : Mathf.Lerp(1.5f, 3.8f, Mathf.Clamp01(ang / 140f));

        Launch(earth, oLat, oLon, tLat, tLon, power, flight, onImpact);
    }

    void Begin(
        EarthPlanet planet,
        float launchLat, float launchLon,
        float targetLat, float targetLon,
        float blastPower,
        float flightSeconds,
        Action callback)
    {
        earth = planet;
        startDir = EarthGeo.LatLonToDirection(launchLat, launchLon);
        endDir = EarthGeo.LatLonToDirection(targetLat, targetLon);
        radius = 0.5f;
        float ang = Vector3.Angle(startDir, endDir);
        // 참고 이미지처럼 지구에 바짝 붙는 낮은 아크
        loft = Mathf.Lerp(0.04f, 0.16f, Mathf.Clamp01(ang / 140f));
        duration = Mathf.Max(0.8f, flightSeconds);
        power = blastPower;
        onImpact = callback;
        t = 0f;

        DrawPath();
        ApplyPose(0f);
    }

    void DrawPath()
    {
        pathRoot = new GameObject("MissilePath").transform;
        pathRoot.SetParent(earth.transform, false);
        path = pathRoot.gameObject.AddComponent<LineRenderer>();
        path.positionCount = 56;
        path.widthMultiplier = 1f;
        path.startWidth = 0.007f;
        path.endWidth = 0.007f;
        path.material = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.95f, 0.9f, 0.85f));
        path.startColor = new Color(1f, 0.98f, 0.95f, 0.9f);
        path.endColor = new Color(1f, 0.7f, 0.45f, 0.55f);
        path.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        path.useWorldSpace = true;
        path.numCapVertices = 2;
        path.numCornerVertices = 2;

        RefreshPath(0f);
    }

    void RefreshPath(float fromU)
    {
        if (path == null)
            return;
        int count = 56;
        path.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float u = Mathf.Lerp(fromU, 1f, i / (float)(count - 1));
            path.SetPosition(i, earth.transform.TransformPoint(SampleLocal(u)));
        }
    }

    void Update()
    {
        if (done || earth == null)
            return;

        float sim = WorldStatusHud.Instance != null ? WorldStatusHud.Instance.SimSpeed : 1f;
        t += Time.unscaledDeltaTime * sim / duration;
        float u = Mathf.Clamp01(t);
        ApplyPose(u);

        if (Time.frameCount % 2 == 0)
            RefreshPath(u);

        if (u >= 1f)
            Detonate();
    }

    void ApplyPose(float u)
    {
        transform.localPosition = SampleLocal(u);
    }

    Vector3 SampleLocal(float u)
    {
        Vector3 dir = Vector3.Slerp(startDir, endDir, u).normalized;
        float alt = 1f + loft * Mathf.Sin(u * Mathf.PI);
        if (u < 0.06f)
            alt = Mathf.Lerp(1.012f, alt, u / 0.06f);
        if (u > 0.94f)
            alt = Mathf.Lerp(alt, 1.008f, (u - 0.94f) / 0.06f);
        return dir * (radius * alt);
    }

    void Detonate()
    {
        if (done)
            return;
        done = true;

        Vector3 point = earth.transform.TransformPoint(SampleLocal(1f));
        Vector3 normal = (point - earth.transform.position).normalized;
        NuclearBlast.Play(earth, point, normal, power);
        onImpact?.Invoke();

        if (pathRoot != null)
            Destroy(pathRoot.gameObject);
        Destroy(gameObject);
    }
}
