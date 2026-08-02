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
        // 오른쪽 무기 레일과 겹치던 TARGET/IMPACT/HITS 표시 제거
    }
}
