using System;
using UnityEngine;

/// <summary>
/// 지구 본체. 운석은 제한 없이 계속 떨어질 수 있다.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class EarthPlanet : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] Renderer crustRenderer;
    [SerializeField] Transform coreVisual;
    [SerializeField] Color healthyTint = Color.white;
    [SerializeField] Color damagedTint = new Color(1f, 0.45f, 0.25f);
    [SerializeField] int coreRevealAfterHits = 8;

    float _heat;
    int _impactCount;
    MaterialPropertyBlock _mpb;

    public int ImpactCount => _impactCount;
    public float Radius => GetComponent<SphereCollider>().radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

    public event Action<float, Vector3> Damaged;

    public void SetVisualRefs(Renderer crust, Transform core)
    {
        crustRenderer = crust;
        coreVisual = core;
        ApplyVisuals();
    }

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (crustRenderer == null)
            crustRenderer = GetComponentInChildren<Renderer>();
        ApplyVisuals();
    }

    public void ApplyImpact(Vector3 worldPoint, float damageAmount)
    {
        _impactCount++;
        _heat = Mathf.Min(1f, _heat + Mathf.Max(0.02f, damageAmount * 0.01f));
        ApplyVisuals();
        Damaged?.Invoke(_heat * 100f, worldPoint);
    }

    void ApplyVisuals()
    {
        float t = _heat;

        if (crustRenderer != null)
        {
            crustRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", Color.Lerp(healthyTint, damagedTint, t));
            _mpb.SetColor("_BaseColor", Color.Lerp(healthyTint, damagedTint, t));
            crustRenderer.SetPropertyBlock(_mpb);
        }

        if (coreVisual != null)
        {
            float reveal = Mathf.InverseLerp(coreRevealAfterHits, coreRevealAfterHits * 4f, _impactCount);
            coreVisual.gameObject.SetActive(reveal > 0.01f);
            float scale = Mathf.Lerp(0.35f, 0.92f, Mathf.Clamp01(reveal));
            coreVisual.localScale = Vector3.one * scale;
        }
    }
}
