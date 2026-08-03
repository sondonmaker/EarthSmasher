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
