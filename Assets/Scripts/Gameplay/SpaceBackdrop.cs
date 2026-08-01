using UnityEngine;

/// <summary>
/// 월드 고정 우주 배경: 별 + 은하수 + 성운.
/// 카메라가 줌되어도 UI는 그대로, 배경만 더 넓게 보임.
/// </summary>
public class SpaceBackdrop : MonoBehaviour
{
    [SerializeField] int starCount = 900;
    [SerializeField] float skyRadius = 120f;

    void Awake()
    {
        transform.position = Vector3.zero;
        BuildMilkyWay();
        BuildNebulae();
        BuildStars();
    }

    void BuildMilkyWay()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MilkyWay";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        // 안쪽이 보이게 스케일 반전
        go.transform.localScale = Vector3.one * -(skyRadius * 2f);

        var tex = BuildMilkyWayTexture(1024, 512);
        var mat = new Material(Shader.Find("Unlit/Texture"));
        if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            mat = RuntimeMaterial.UnlitTransparent(Color.white);
        mat.mainTexture = tex;
        mat.color = Color.white;
        // Unlit/Texture may not support tint; fallback sprites
        if (!mat.HasProperty("_MainTex") && mat.HasProperty("_Color"))
            mat = RuntimeMaterial.UnlitTransparent(Color.white);

        // Prefer unlit textured
        var unlit = Shader.Find("Unlit/Texture");
        if (unlit != null)
        {
            mat = new Material(unlit);
            mat.mainTexture = tex;
        }
        else
        {
            mat = new Material(Shader.Find("Standard"));
            mat.mainTexture = tex;
            mat.color = Color.white;
            mat.SetFloat("_Glossiness", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", Color.white * 0.55f);
        }

        var rend = go.GetComponent<Renderer>();
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        // 은하수 밴드를 약간 기울임 (실제처럼)
        go.transform.rotation = Quaternion.Euler(62f, 15f, 0f);
    }

    void BuildNebulae()
    {
        Color[] colors =
        {
            new Color(0.35f, 0.2f, 0.7f, 0.12f),
            new Color(0.55f, 0.15f, 0.4f, 0.1f),
            new Color(0.15f, 0.35f, 0.7f, 0.1f),
            new Color(0.4f, 0.5f, 0.9f, 0.08f)
        };

        for (int i = 0; i < colors.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Nebula";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            Vector3 dir = Random.onUnitSphere;
            // 은하수 평면 근처에 더 많이
            dir.y *= 0.35f;
            dir.Normalize();
            go.transform.position = dir * (skyRadius * Random.Range(0.55f, 0.85f));
            float s = Random.Range(18f, 40f);
            go.transform.localScale = Vector3.one * s;

            var mat = RuntimeMaterial.UnlitTransparent(colors[i]);
            var rend = go.GetComponent<Renderer>();
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }

    void BuildStars()
    {
        var root = new GameObject("Stars").transform;
        root.SetParent(transform, false);

        for (int i = 0; i < starCount; i++)
        {
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "Star";
            Destroy(star.GetComponent<Collider>());
            star.transform.SetParent(root, false);
            star.transform.position = Random.onUnitSphere * (skyRadius * Random.Range(0.88f, 0.99f));

            float s = Random.value > 0.92f
                ? Random.Range(0.18f, 0.35f)
                : Random.Range(0.04f, 0.12f);
            star.transform.localScale = Vector3.one * s;

            Color c = Color.white;
            float roll = Random.value;
            if (roll < 0.15f) c = new Color(0.7f, 0.85f, 1f);      // 푸른별
            else if (roll < 0.3f) c = new Color(1f, 0.9f, 0.7f);   // 노란별
            else if (roll < 0.38f) c = new Color(1f, 0.75f, 0.65f); // 붉은별

            var rend = star.GetComponent<Renderer>();
            rend.material = RuntimeMaterial.Opaque(c, Random.Range(1.2f, 3.5f));
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }

    static Texture2D BuildMilkyWayTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float band = 1f - Mathf.Abs(v - 0.5f) * 2f; // 적도=은하수
            band = Mathf.Pow(Mathf.Clamp01(band), 1.6f);

            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float noise =
                    0.55f * Mathf.PerlinNoise(u * 6f, v * 4f) +
                    0.30f * Mathf.PerlinNoise(u * 14f + 3f, v * 10f) +
                    0.15f * Mathf.PerlinNoise(u * 40f, v * 30f + 1f);

                float dust = band * noise;
                float core = band * band * (0.4f + 0.6f * Mathf.PerlinNoise(u * 3f, 0.5f));

                float r = 0.04f + dust * 0.55f + core * 0.35f;
                float g = 0.05f + dust * 0.45f + core * 0.25f;
                float b = 0.10f + dust * 0.65f + core * 0.15f;

                // 어두운 우주 바탕
                float space = 0.015f + 0.02f * Mathf.PerlinNoise(u * 2f, v * 2f);
                r = Mathf.Max(r, space);
                g = Mathf.Max(g, space * 1.1f);
                b = Mathf.Max(b, space * 1.4f);

                tex.SetPixel(x, y, new Color(r, g, b, 1f));
            }
        }

        tex.Apply(false, false);
        return tex;
    }
}
