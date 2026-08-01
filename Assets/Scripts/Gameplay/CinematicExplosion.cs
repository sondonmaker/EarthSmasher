using UnityEngine;

/// <summary>
/// 참고 영상급 충돌 폭발: 섬광 + 화구 + 파편 + 불씨 + 조명.
/// Particle Prefab 없이 런타임으로 조합한다.
/// </summary>
public class CinematicExplosion : MonoBehaviour
{
    public static void Play(Vector3 point, Vector3 normal, float power = 1f)
    {
        var go = new GameObject("CinematicExplosion");
        go.transform.position = point;
        var fx = go.AddComponent<CinematicExplosion>();
        fx.Begin(point, normal.normalized, Mathf.Clamp(power, 0.5f, 3f));
    }

    void Begin(Vector3 point, Vector3 normal, float power)
    {
        SpawnFlashLight(point, normal, power);
        SpawnFireball(point, power);
        SpawnDebris(point, normal, power);
        SpawnEmbers(point, normal, power);
        SpawnSmokePuffs(point, normal, power);
        CameraShake.Shake(0.45f * power, 0.55f * power);
        Destroy(gameObject, 5f);
    }

    void SpawnFlashLight(Vector3 point, Vector3 normal, float power)
    {
        var lightGo = new GameObject("FlashLight");
        lightGo.transform.position = point + normal * 0.5f;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.72f, 0.35f);
        light.intensity = 25f * power;
        light.range = 18f * power;
        lightGo.AddComponent<ImpactFlashFade>().Begin(0.85f);
    }

    void SpawnFireball(Vector3 point, float power)
    {
        // 밝은 코어
        CreateExpandingSphere(point, 0.2f * power, 1.8f * power, new Color(1f, 0.95f, 0.7f, 0.95f), 4f, 0.35f);
        // 주황 화구
        CreateExpandingSphere(point, 0.4f * power, 3.2f * power, new Color(1f, 0.45f, 0.08f, 0.55f), 2.2f, 0.7f);
        // 바깥 글로우
        CreateExpandingSphere(point, 0.6f * power, 4.5f * power, new Color(1f, 0.25f, 0.05f, 0.25f), 1.2f, 1.0f);
    }

    void CreateExpandingSphere(Vector3 point, float start, float end, Color color, float emission, float life)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Fireball";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = point;
        go.transform.localScale = Vector3.one * start;

        var rend = go.GetComponent<Renderer>();
        var mat = RuntimeMaterial.UnlitTransparent(color);
        // Unlit에 emission이 없으면 opaque+emission으로 폴백
        if (!mat.HasProperty("_Color") || mat.shader == null || mat.shader.name.Contains("Lit") || mat.shader.name.Contains("Standard"))
            mat = RuntimeMaterial.Opaque(new Color(color.r, color.g, color.b, 1f), emission);
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var anim = go.AddComponent<ExpandAndFade>();
        anim.Init(start, end, life, color, mat);
    }

    void SpawnDebris(Vector3 point, Vector3 normal, float power)
    {
        int count = Mathf.RoundToInt(28 * power);
        for (int i = 0; i < count; i++)
        {
            var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "RockDebris";
            Destroy(chunk.GetComponent<Collider>());
            chunk.transform.position = point + normal * 0.2f + Random.insideUnitSphere * 0.15f;
            float s = Random.Range(0.08f, 0.35f) * power;
            chunk.transform.localScale = new Vector3(s, s * Random.Range(0.5f, 1.2f), s * Random.Range(0.5f, 1.2f));
            chunk.transform.rotation = Random.rotation;

            bool molten = Random.value > 0.45f;
            var col = molten
                ? new Color(1f, Random.Range(0.25f, 0.45f), 0.05f)
                : new Color(0.25f, 0.2f, 0.16f);
            chunk.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(col, molten ? Random.Range(1.5f, 3.5f) : 0f);

            var rb = chunk.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = s * 2f;
            Vector3 dir = (normal * 0.55f + Random.onUnitSphere * 0.9f).normalized;
            rb.AddForce(dir * Random.Range(6f, 18f) * power, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 12f, ForceMode.Impulse);
            Destroy(chunk, Random.Range(2.5f, 4.5f));
        }
    }

    void SpawnEmbers(Vector3 point, Vector3 normal, float power)
    {
        int count = Mathf.RoundToInt(40 * power);
        for (int i = 0; i < count; i++)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "Ember";
            Destroy(spark.GetComponent<Collider>());
            spark.transform.position = point + Random.insideUnitSphere * 0.2f;
            float s = Random.Range(0.03f, 0.09f);
            spark.transform.localScale = Vector3.one * s;
            spark.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(
                new Color(1f, Random.Range(0.4f, 0.8f), 0.1f), Random.Range(2f, 5f));

            var rb = spark.AddComponent<Rigidbody>();
            rb.useGravity = false;
            Vector3 dir = (normal + Random.insideUnitSphere).normalized;
            rb.AddForce(dir * Random.Range(4f, 14f) * power, ForceMode.Impulse);
            Destroy(spark, Random.Range(1.2f, 2.5f));
        }
    }

    void SpawnSmokePuffs(Vector3 point, Vector3 normal, float power)
    {
        for (int i = 0; i < 6; i++)
        {
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "Smoke";
            Destroy(puff.GetComponent<Collider>());
            puff.transform.position = point + normal * 0.3f + Random.insideUnitSphere * 0.4f;
            float start = 0.3f * power;
            float end = Random.Range(1.5f, 2.8f) * power;
            var mat = RuntimeMaterial.UnlitTransparent(new Color(0.25f, 0.2f, 0.18f, 0.35f));
            puff.GetComponent<Renderer>().material = mat;
            puff.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puff.AddComponent<ExpandAndFade>().Init(start, end, Random.Range(1.2f, 2f), new Color(0.2f, 0.18f, 0.15f, 0.3f), mat);
        }
    }
}

/// <summary>화구/연기가 커지며 사라짐</summary>
public class ExpandAndFade : MonoBehaviour
{
    float _start;
    float _end;
    float _life;
    float _t;
    Color _color;
    Material _mat;

    public void Init(float start, float end, float life, Color color, Material mat)
    {
        _start = start;
        _end = end;
        _life = Mathf.Max(0.05f, life);
        _color = color;
        _mat = mat;
        transform.localScale = Vector3.one * start;
        Destroy(gameObject, life + 0.05f);
    }

    void Update()
    {
        _t += Time.deltaTime;
        float k = Mathf.Clamp01(_t / _life);
        float s = Mathf.Lerp(_start, _end, EaseOut(k));
        transform.localScale = Vector3.one * s;

        if (_mat != null)
        {
            var c = _color;
            c.a = _color.a * (1f - k);
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", c);
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
            _mat.color = c;
            if (_mat.IsKeywordEnabled("_EMISSION") || _mat.HasProperty("_EmissionColor"))
                _mat.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * (3f * (1f - k)));
        }
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
