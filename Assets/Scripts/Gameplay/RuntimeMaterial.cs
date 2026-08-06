using UnityEngine;

/// <summary>
/// Built-in / URP 겸용 간단 머티리얼.
/// </summary>
public static class RuntimeMaterial
{
    public static Material Opaque(Color color, float emission = 0f)
    {
        var mat = new Material(PickShader(false));
        ApplyColor(mat, color);
        if (emission > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emission);
        }
        return mat;
    }

    public static Material UnlitTransparent(Color color)
    {
        // 트레일/링용 — 항상 스프라이트 언릿
        var mat = new Material(EarthTextureLoader.SafeShader("Sprites/Default"));
        mat.color = color;
        return mat;
    }

    /// <summary>PNG 알파가 보이도록 — Unlit/Texture 는 알파 무시해서 체커보드가 회색 원으로 보임.</summary>
    public static Material TexturedTransparent(Texture2D tex)
    {
        var mat = new Material(EarthTextureLoader.SafeShader("Sprites/Default", "Unlit/Transparent"));
        mat.mainTexture = tex;
        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", tex);
        mat.color = Color.white;
        mat.renderQueue = 3000;
        return mat;
    }

    static Shader PickShader(bool preferTransparent)
    {
        return EarthTextureLoader.SafeShader(
            "Universal Render Pipeline/Lit", "Standard", "Sprites/Default");
    }

    static void ApplyColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        mat.color = color;
    }
}
