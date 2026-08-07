using UnityEngine;

/// <summary>
/// 핵폭발: 짧은 빨간 섬광만 띄우고, 영구 자국은 지표면 텍스처 어둡게 칠함.
/// (구체/원기둥 데칼은 쓰지 않음 — 곡면에 붙으면 깨져 보임)
/// </summary>
public class NuclearBlast : MonoBehaviour
{
    public static void Play(EarthPlanet earth, Vector3 worldPoint, Vector3 normal, float power = 1f)
    {
        power = Mathf.Clamp(power, 0.35f, 1.65f);
        bool usedPack = ProFxParticleSpawner.TryNuclearExplosion(worldPoint, normal, power);
        SpawnFlash(worldPoint, normal, power * 0.65f);
        if (!usedPack)
            SpawnFireball(worldPoint, normal, power * 0.55f);
        CameraShake.Shake(0.045f * power, 0.08f * power);

        if (earth != null)
        {
            var scorch = EarthSurfaceScorch.Ensure(earth);
            if (scorch != null)
                scorch.BurnAt(worldPoint, 0.028f * power, 0.78f);

            PopulationCasualtySystem.ApplyAt(
                earth,
                worldPoint,
                PopulationCasualtySystem.ScorchNormToDegrees(0.028f * power),
                Mathf.Clamp01(0.55f + power * 0.18f),
                power);
        }
    }

    static void SpawnFlash(Vector3 point, Vector3 normal, float power)
    {
        var lightGo = new GameObject("NukeFlash");
        lightGo.transform.position = point + normal * 0.35f;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.35f, 0.12f);
        light.intensity = 16f * power;
        light.range = 8f * power;
        lightGo.AddComponent<ImpactFlashFade>().Begin(0.45f);
        Object.Destroy(lightGo, 1.0f);
    }

    static void SpawnFireball(Vector3 point, Vector3 normal, float power)
    {
        // 짧게만 보이고 사라짐 — 영구 메시 남기지 않음
        CreateExpanding(point + normal * 0.04f, 0.06f * power, 0.42f * power,
            new Color(1f, 0.95f, 0.75f, 0.9f), 0.22f);
        CreateExpanding(point + normal * 0.02f, 0.1f * power, 0.75f * power,
            new Color(1f, 0.22f, 0.05f, 0.7f), 0.4f);
        CreateExpanding(point, 0.14f * power, 1.05f * power,
            new Color(0.75f, 0.06f, 0.02f, 0.35f), 0.65f);
    }

    static void CreateExpanding(Vector3 point, float start, float end, Color color, float life)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "NukeFireball";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = point;
        go.transform.localScale = Vector3.one * start;

        var rend = go.GetComponent<Renderer>();
        var mat = RuntimeMaterial.UnlitTransparent(color);
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var anim = go.AddComponent<NukeExpandFade>();
        anim.Init(start, end, life, color, mat);
    }
}

public class NukeExpandFade : MonoBehaviour
{
    float start;
    float end;
    float life;
    float t;
    Color color;
    Material mat;

    public void Init(float s, float e, float lifeSec, Color c, Material m)
    {
        start = s;
        end = e;
        life = Mathf.Max(0.05f, lifeSec);
        color = c;
        mat = m;
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / life);
        float ease = 1f - (1f - u) * (1f - u);
        transform.localScale = Vector3.one * Mathf.Lerp(start, end, ease);

        Color c = color;
        c.a = color.a * (1f - u);
        if (mat != null)
        {
            mat.color = c;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", c);
        }

        if (u >= 1f)
            Destroy(gameObject);
    }
}
