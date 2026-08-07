using UnityEngine;

/// <summary>
/// 임팩트 충격파 — 채워진 원판이 아니라 얇은 링으로 퍼진다.
/// </summary>
public class ImpactShockwave : MonoBehaviour
{
    [SerializeField] float lifetime = 0.55f;
    [SerializeField] float startRadius = 0.2f;
    [SerializeField] float endRadius = 2.2f;

    LineRenderer _line;
    float _t;
    Color _color = new Color(1f, 0.55f, 0.15f, 0.9f);

    public static void Spawn(Vector3 position, Vector3 normal, float size)
    {
        Spawn(position, normal, size, new Color(1f, 0.55f, 0.15f, 0.9f));
    }

    public static void Spawn(Vector3 position, Vector3 normal, float size, Color color)
    {
        var go = new GameObject("ShockwaveRing");
        go.transform.position = position + normal * 0.08f;
        go.transform.rotation = Quaternion.LookRotation(normal);

        var sw = go.AddComponent<ImpactShockwave>();
        sw._color = color;
        sw.startRadius = size * 0.12f;
        sw.endRadius = size * 0.85f;
        sw.BuildRing();
        Destroy(go, 0.7f);
    }

    void BuildRing()
    {
        _line = gameObject.AddComponent<LineRenderer>();
        _line.loop = true;
        _line.useWorldSpace = false;
        _line.widthMultiplier = 0.06f;
        _line.positionCount = 48;
        _line.material = RuntimeMaterial.UnlitTransparent(_color);
        _line.startColor = _color;
        _line.endColor = _color;
        SetRadius(startRadius);
    }

    void Update()
    {
        if (_line == null) return;
        _t += Time.deltaTime;
        float k = Mathf.Clamp01(_t / lifetime);
        float r = Mathf.Lerp(startRadius, endRadius, k);
        SetRadius(r);

        var c = _color;
        c.a = (1f - k) * 0.9f;
        _line.startColor = c;
        _line.endColor = c;
        _line.widthMultiplier = Mathf.Lerp(0.08f, 0.02f, k);
    }

    void SetRadius(float radius)
    {
        int n = _line.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f;
            _line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }
}
