using UnityEngine;

/// <summary>UFO 격추 — UFO 위치에만 중간 크기 화염 폭발.</summary>
public static class UfoDestroyFx
{
    static readonly Color PopColor = new Color(1f, 0.52f, 0.1f, 0.62f);

    public static void Play(Vector3 position, float earthRadius)
    {
        float scale = earthRadius * 0.009f;
        if (!ProFxParticleSpawner.TryUfoDestroy(position, earthRadius))
            SpawnFallbackPop(position, scale);
    }

    static void SpawnFallbackPop(Vector3 position, float scale)
    {
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "UfoPop";
        Object.Destroy(core.GetComponent<Collider>());
        core.transform.position = position;
        core.transform.localScale = Vector3.one * Mathf.Max(scale * 1.8f, 0.04f);
        var rend = core.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.UnlitTransparent(PopColor);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.Destroy(core, 0.16f);
    }
}
