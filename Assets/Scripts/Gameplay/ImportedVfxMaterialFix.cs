using UnityEngine;

/// <summary>임포트 VFX 프리팹의 깨진(마젠타) 머티리얼을 Built-in 파티클 셰이더로 복구.</summary>
public static class ImportedVfxMaterialFix
{
    static Material fallbackParticleMat;

    public static bool IsBroken(Material mat)
    {
        if (mat == null || mat.shader == null)
            return true;
        string n = mat.shader.name;
        return n.Contains("InternalErrorShader") || n.Contains("Hidden/");
    }

    public static bool PrefabLooksValid(GameObject prefab)
    {
        if (prefab == null)
            return false;

        bool sawRenderer = false;
        foreach (var psr in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            sawRenderer = true;
            var mats = psr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (IsBroken(mats[i]))
                    return false;
            }
        }

        foreach (var mr in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            sawRenderer = true;
            var mats = mr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (IsBroken(mats[i]))
                    return false;
            }
        }

        return sawRenderer;
    }

    /// <summary>카탈로그만 연결된 불완전 임포트 — 스폰 후 FixHierarchy로 복구 가능한지.</summary>
    public static bool CanRuntimeFix(GameObject prefab)
    {
        if (prefab == null)
            return false;

        foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps != null)
                return true;
        }

        return prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Length > 0
            || prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0;
    }

    public static void FixHierarchy(GameObject root)
    {
        if (root == null)
            return;

        foreach (var psr in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (psr == null)
                continue;

            var mats = psr.materials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!IsBroken(mats[i]))
                    continue;
                mats[i] = Remap(mats[i]);
                changed = true;
            }
            if (changed)
                psr.materials = mats;

            if (!RendererHasValidMaterial(psr))
                psr.enabled = false;
        }

        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null)
                continue;

            var meshFilter = mr.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                mr.enabled = false;
                continue;
            }

            var mats = mr.materials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!IsBroken(mats[i]))
                    continue;
                mats[i] = Remap(mats[i]);
                changed = true;
            }
            if (changed)
                mr.materials = mats;

            if (!RendererHasValidMaterial(mr))
                mr.enabled = false;
        }
    }

    static bool RendererHasValidMaterial(Renderer renderer)
    {
        var mats = renderer.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (!IsBroken(mats[i]))
                return true;
        }
        return mats.Length == 0;
    }

    static Material Remap(Material source)
    {
        Texture tex = null;
        Color tint = Color.white;
        if (source != null)
        {
            if (source.HasProperty("_MainTex"))
                tex = source.GetTexture("_MainTex");
            if (tex == null && source.HasProperty("_BaseMap"))
                tex = source.GetTexture("_BaseMap");
            if (source.HasProperty("_Color"))
                tint = source.GetColor("_Color");
            else if (source.HasProperty("_BaseColor"))
                tint = source.GetColor("_BaseColor");
        }

        var mat = FallbackParticleMat();
        if (tex != null)
        {
            mat.mainTexture = tex;
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
        }
        mat.color = tint;
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);
        return mat;
    }

    static Material FallbackParticleMat()
    {
        if (fallbackParticleMat != null)
            return fallbackParticleMat;

        Shader sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null)
            sh = Shader.Find("Particles/Alpha Blended");
        if (sh == null)
            sh = EarthTextureLoader.SafeShader("Sprites/Default", "Unlit/Transparent");

        fallbackParticleMat = new Material(sh);
        fallbackParticleMat.name = "ImportedVfxFallback";
        if (fallbackParticleMat.HasProperty("_ZWrite"))
            fallbackParticleMat.SetFloat("_ZWrite", 0f);
        fallbackParticleMat.renderQueue = 3000;
        return fallbackParticleMat;
    }
}
