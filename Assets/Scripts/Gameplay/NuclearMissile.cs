using System;
using UnityEngine;

/// <summary>
/// 핵미사일.
/// - Nuclear War: 지표 저궤도 아크
/// - 플레이어 조준: 우주에서 곡선으로 돌입해 착탄
/// </summary>
public class NuclearMissile : MonoBehaviour
{
    enum FlightMode
    {
        SurfaceArc,
        SpaceDive
    }

    EarthPlanet earth;
    FlightMode mode;

    // surface arc (lat/lon ICBM)
    Vector3 startDir;
    Vector3 endDir;
    float radius;
    float loft;

    // space dive (world Bezier)
    Vector3 p0;
    Vector3 p1;
    Vector3 p2;

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

        var go = CreateBody(earth, tiny: true);
        var m = go.AddComponent<NuclearMissile>();
        m.BeginSurface(earth, launchLat, launchLon, targetLat, targetLon, power, flightSeconds, onImpact);
    }

    /// <summary>우주에서 곡선 돌입 → 클릭 지점 폭발.</summary>
    public static void LaunchToWorldPoint(
        EarthPlanet earth,
        Vector3 worldTarget,
        float power,
        float flightSeconds,
        Action onImpact)
    {
        if (earth == null)
            return;

        Vector3 center = earth.transform.position;
        Vector3 toHit = worldTarget - center;
        if (toHit.sqrMagnitude < 1e-8f)
            return;
        Vector3 normal = toHit.normalized;
        Vector3 impact = center + normal * earth.Radius;

        // 우주 출발점: 타겟 바깥 + 랜덤 측면 (운석처럼 멀리서)
        Vector3 lateral = Vector3.Cross(normal, UnityEngine.Random.onUnitSphere);
        if (lateral.sqrMagnitude < 1e-4f)
            lateral = Vector3.Cross(normal, Vector3.up);
        lateral.Normalize();
        float side = UnityEngine.Random.Range(-1.1f, 1.1f);
        float upBias = UnityEngine.Random.Range(-0.35f, 0.55f);
        Vector3 approach = (normal + lateral * side + Vector3.up * upBias).normalized;
        if (Vector3.Dot(approach, normal) < 0.15f)
            approach = (normal + lateral * 0.6f).normalized;

        float startDist = earth.Radius * UnityEngine.Random.Range(3.2f, 5.2f);
        Vector3 start = center + approach * startDist;

        // 중간 제어점: 옆으로 휘는 곡선 (직선 낙하 방지)
        Vector3 mid = Vector3.Lerp(start, impact, UnityEngine.Random.Range(0.35f, 0.55f));
        Vector3 bendAxis = Vector3.Cross((impact - start).normalized, UnityEngine.Random.onUnitSphere);
        if (bendAxis.sqrMagnitude < 1e-4f)
            bendAxis = Vector3.Cross((impact - start).normalized, normal);
        bendAxis.Normalize();
        mid += bendAxis * (earth.Radius * UnityEngine.Random.Range(0.55f, 1.65f));
        // 약간 더 우주 쪽으로 띄워 아크 느낌
        mid += ((start + impact) * 0.5f - center).normalized * (earth.Radius * UnityEngine.Random.Range(0.15f, 0.55f));

        float flight = flightSeconds > 0f
            ? flightSeconds
            : UnityEngine.Random.Range(2.0f, 3.4f);

        var go = CreateBody(earth, tiny: false);
        go.transform.SetParent(null, true); // 월드 공간 비행
        var m = go.AddComponent<NuclearMissile>();
        m.BeginSpaceDive(earth, start, mid, impact, power, flight, onImpact);
    }

    static GameObject CreateBody(EarthPlanet earth, bool tiny)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = tiny ? "NuclearMissile" : "SpaceNukeMissile";
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());

        if (tiny)
        {
            go.transform.SetParent(earth.transform, false);
            go.transform.localScale = new Vector3(0.003f, 0.008f, 0.003f);
        }
        else
        {
            // 운석보다 길쭉한 미사일 실루엣
            go.transform.localScale = new Vector3(0.12f, 0.38f, 0.12f);
        }

        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.85f, 0.88f, 0.92f), 0.6f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 노즐 불꽃
        var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.Destroy(flame.GetComponent<Collider>());
        flame.name = "Exhaust";
        flame.transform.SetParent(go.transform, false);
        flame.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        flame.transform.localScale = tiny ? Vector3.one * 0.8f : new Vector3(0.7f, 0.9f, 0.7f);
        flame.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(1f, 0.45f, 0.1f), 3.5f);

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = tiny ? 0.55f : 1.1f;
        trail.startWidth = tiny ? 0.008f : 0.14f;
        trail.endWidth = tiny ? 0.0015f : 0.02f;
        trail.material = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.75f, 0.35f, 0.9f));
        trail.startColor = new Color(1f, 0.9f, 0.55f, 0.95f);
        trail.endColor = new Color(1f, 0.35f, 0.05f, 0f);
        trail.minVertexDistance = tiny ? 0.01f : 0.05f;
        trail.numCapVertices = 2;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return go;
    }

    void BeginSurface(
        EarthPlanet planet,
        float launchLat, float launchLon,
        float targetLat, float targetLon,
        float blastPower,
        float flightSeconds,
        Action callback)
    {
        mode = FlightMode.SurfaceArc;
        earth = planet;
        startDir = EarthGeo.LatLonToDirection(launchLat, launchLon);
        endDir = EarthGeo.LatLonToDirection(targetLat, targetLon);
        radius = 0.5f;
        float ang = Vector3.Angle(startDir, endDir);
        loft = Mathf.Lerp(0.04f, 0.16f, Mathf.Clamp01(ang / 140f));
        duration = Mathf.Max(0.8f, flightSeconds);
        power = blastPower;
        onImpact = callback;
        t = 0f;

        DrawPath(true);
        ApplyPose(0f);
    }

    void BeginSpaceDive(
        EarthPlanet planet,
        Vector3 start,
        Vector3 control,
        Vector3 impact,
        float blastPower,
        float flightSeconds,
        Action callback)
    {
        mode = FlightMode.SpaceDive;
        earth = planet;
        p0 = start;
        p1 = control;
        p2 = impact;
        duration = Mathf.Max(1.2f, flightSeconds);
        power = blastPower;
        onImpact = callback;
        t = 0f;

        transform.position = p0;
        DrawPath(false);
        ApplyPose(0f);
    }

    void DrawPath(bool localSpace)
    {
        pathRoot = new GameObject("MissilePath").transform;
        if (localSpace && earth != null)
            pathRoot.SetParent(earth.transform, false);
        path = pathRoot.gameObject.AddComponent<LineRenderer>();
        path.positionCount = 48;
        path.widthMultiplier = 1f;
        path.startWidth = mode == FlightMode.SpaceDive ? 0.06f : 0.007f;
        path.endWidth = mode == FlightMode.SpaceDive ? 0.02f : 0.007f;
        path.material = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.85f, 0.45f, 0.75f));
        path.startColor = new Color(1f, 0.95f, 0.7f, 0.85f);
        path.endColor = new Color(1f, 0.4f, 0.15f, 0.35f);
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
        int count = 48;
        path.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float u = Mathf.Lerp(fromU, 1f, i / (float)(count - 1));
            path.SetPosition(i, SampleWorld(u));
        }
    }

    void Update()
    {
        if (done || earth == null)
            return;

        float sim = WorldStatusHud.Instance != null ? WorldStatusHud.Instance.SimSpeed : 1f;
        // 후반에 조금 가속 (돌입감)
        float easeStep = Mathf.Lerp(0.75f, 1.45f, Mathf.Clamp01(t));
        t += Time.unscaledDeltaTime * sim * easeStep / duration;
        float u = Mathf.Clamp01(t);
        ApplyPose(u);

        if (Time.frameCount % 2 == 0)
            RefreshPath(u);

        if (u >= 1f)
            Detonate();
    }

    void ApplyPose(float u)
    {
        Vector3 pos = SampleWorld(u);
        Vector3 next = SampleWorld(Mathf.Min(1f, u + 0.02f));
        transform.position = pos;
        Vector3 vel = next - pos;
        if (vel.sqrMagnitude > 1e-8f)
            transform.rotation = Quaternion.LookRotation(vel.normalized) * Quaternion.Euler(90f, 0f, 0f);
    }

    Vector3 SampleWorld(float u)
    {
        u = Mathf.Clamp01(u);
        if (mode == FlightMode.SpaceDive)
        {
            // Quadratic Bezier: 우주 → 곡선 → 착탄
            float o = 1f - u;
            return o * o * p0 + 2f * o * u * p1 + u * u * p2;
        }

        // surface local → world
        Vector3 local = SampleLocalSurface(u);
        return earth.transform.TransformPoint(local);
    }

    Vector3 SampleLocalSurface(float u)
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

        Vector3 point = SampleWorld(1f);
        Vector3 normal = (point - earth.transform.position).normalized;
        NuclearBlast.Play(earth, point, normal, power);
        onImpact?.Invoke();

        if (pathRoot != null)
            Destroy(pathRoot.gameObject);
        Destroy(gameObject);
    }
}
