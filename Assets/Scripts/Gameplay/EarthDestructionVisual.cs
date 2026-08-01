using UnityEngine;

/// <summary>
/// 데미지 구간마다 지각 덩어리를 튀겨 파괴 메시 느낌을 낸다.
/// </summary>
public class EarthDestructionVisual : MonoBehaviour
{
    [SerializeField] EarthPlanet earth;
    [SerializeField] int chunksPerThreshold = 6;
    [SerializeField] float chunkForce = 7f;
    [SerializeField] Color crustColor = new Color(0.25f, 0.45f, 0.2f);
    [SerializeField] Color lavaColor = new Color(1f, 0.35f, 0.05f);

    float _lastMilestone;

    void Awake()
    {
        if (earth == null) earth = GetComponent<EarthPlanet>();
        if (earth != null)
            earth.Damaged += OnDamaged;
    }

    void OnDestroy()
    {
        if (earth != null)
            earth.Damaged -= OnDamaged;
    }

    void OnDamaged(float damagePercent, Vector3 worldPoint)
    {
        float milestone = Mathf.Floor(damagePercent / 10f) * 10f;
        if (milestone <= _lastMilestone) return;
        _lastMilestone = milestone;
        SpawnChunks(worldPoint, Mathf.RoundToInt(chunksPerThreshold * (0.6f + damagePercent / 100f)));
    }

    void SpawnChunks(Vector3 origin, int count)
    {
        Vector3 outward = (origin - transform.position).normalized;
        for (int i = 0; i < count; i++)
        {
            var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "CrustChunk";
            chunk.transform.position = origin + outward * 0.15f + Random.insideUnitSphere * 0.25f;
            chunk.transform.localScale = Vector3.one * Random.Range(0.12f, 0.35f);
            chunk.transform.rotation = Random.rotation;

            var rend = chunk.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Standard"));
            bool lava = Random.value > 0.65f;
            rend.material.color = lava ? lavaColor : crustColor;
            if (lava)
            {
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", lavaColor * 2.2f);
            }

            Destroy(chunk.GetComponent<Collider>());
            var rb = chunk.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            Vector3 dir = (outward + Random.insideUnitSphere * 0.9f).normalized;
            rb.AddForce(dir * Random.Range(chunkForce * 0.6f, chunkForce * 1.4f), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);
            Destroy(chunk, 5f);
        }
    }
}
