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
    [SerializeField] Color nuclearTint = new Color(0.42f, 0.36f, 0.30f);
    [SerializeField] int coreRevealAfterHits = 8;

    float _heat;
    float _nuclearScorch;
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

    public void ApplyNuclearScorch(float intensity)
    {
        _nuclearScorch = Mathf.Clamp01(Mathf.Max(_nuclearScorch, intensity));
        ApplyVisuals();
    }

    void ApplyVisuals()
    {
        float t = _heat;

        if (crustRenderer != null)
        {
            Color baseTint = Color.Lerp(healthyTint, damagedTint, t);
            Color finalTint = Color.Lerp(baseTint, nuclearTint, _nuclearScorch * 0.85f);
            crustRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", finalTint);
            _mpb.SetColor("_BaseColor", finalTint);
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
