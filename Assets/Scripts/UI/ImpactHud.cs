using UnityEngine;

/// <summary>
/// 컨셉 HUD — 데미지% 제한 없음. 충돌 횟수만 표시.
/// </summary>
public class ImpactHud : MonoBehaviour
{
    bool _targeting;
    float _impactSeconds;
    float _hitRate = 100f;
    int _impactCount;
    GUIStyle _style;
    GUIStyle _accentStyle;

    public void Bind(object target, object impact, object hitRate, object damage)
    {
        SetTargeting(false);
        SetImpactCountdown(0f);
        SetHitRate(100f);
        SetImpactCount(0);
    }

    public void SetTargeting(bool targeting) => _targeting = targeting;
    public void SetImpactCountdown(float seconds) => _impactSeconds = seconds;
    public void SetHitRate(float percent) => _hitRate = percent;
    public void SetImpactCount(int count) => _impactCount = count;

    // 구버전 호출 호환
    public void SetDamage(float percent) { }

    void EnsureStyles()
    {
        if (_style != null) return;
        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };
        _style.normal.textColor = new Color(0.75f, 0.95f, 1f, 0.95f);

        _accentStyle = new GUIStyle(_style);
        _accentStyle.normal.textColor = new Color(1f, 0.75f, 0.35f, 1f);
    }

    void OnGUI()
    {
        EnsureStyles();
        float w = 360f;
        float x = Screen.width - w - 16f;
        float y = 58f; // 상단 바 아래
        float h = 28f;

        string target = _targeting ? "TARGET: EARTH" : "TARGET: ---";
        string impact = (!_targeting || _impactSeconds <= 0.001f)
            ? "IMPACT IN: --"
            : $"IMPACT IN: {_impactSeconds:0.0} SEC";

        GUI.Label(new Rect(x, y, w, h), target, _style);
        GUI.Label(new Rect(x, y + 28f, w, h), impact, _style);
        GUI.Label(new Rect(x, y + 56f, w, h), $"HITS: {_impactCount}", _accentStyle);
    }
}
