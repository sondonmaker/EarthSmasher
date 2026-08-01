using UnityEngine;

/// <summary>
/// 운석 낙하 궤적 — 가느다란 화염 꼬리.
/// </summary>
[RequireComponent(typeof(MeteorProjectile))]
public class MeteorTrail : MonoBehaviour
{
    void Awake()
    {
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 0.12f;
        trail.endWidth = 0.01f;
        trail.material = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.6f, 0.2f, 0.85f));
        trail.startColor = new Color(1f, 0.75f, 0.3f, 0.9f);
        trail.endColor = new Color(1f, 0.15f, 0.05f, 0f);
        trail.minVertexDistance = 0.04f;
        trail.numCapVertices = 1;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var glowGo = new GameObject("MeteorGlow");
        glowGo.transform.SetParent(transform, false);
        var glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.55f, 0.2f);
        glow.intensity = 2.2f;
        glow.range = 3f;
    }
}
