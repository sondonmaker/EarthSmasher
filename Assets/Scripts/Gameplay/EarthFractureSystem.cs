using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지구 표면을 미리 쪼갠 지각 조각으로 만들고, 임팩트 지점 주변 조각을 물리로 뜯어낸다.
/// </summary>
public class EarthFractureSystem : MonoBehaviour
{
    [SerializeField] EarthPlanet earth;
    [SerializeField] int latitudeBands = 10;
    [SerializeField] int longitudeBands = 16;
    [SerializeField] float shardThickness = 0.08f;
    [SerializeField] float detachRadius = 1.35f;
    [SerializeField] float blastForce = 14f;
    [SerializeField] float torqueForce = 10f;
    [SerializeField] int maxDetachPerHit = 10;

    readonly List<CrustShard> _shards = new List<CrustShard>();
    Transform _shardRoot;
    bool _built;

    class CrustShard
    {
        public Transform Transform;
        public Rigidbody Body;
        public Vector3 LocalDir;
        public bool Detached;
    }

    void Awake()
    {
        if (earth == null) earth = GetComponent<EarthPlanet>();
        if (earth != null) earth.Damaged += OnDamaged;
    }

    void Start()
    {
        BuildShards();
    }

    void OnDestroy()
    {
        if (earth != null) earth.Damaged -= OnDamaged;
    }

    void BuildShards()
    {
        if (_built) return;
        _built = true;

        _shardRoot = new GameObject("CrustShards").transform;
        _shardRoot.SetParent(transform, false);

        float radius = 0.5f; // unit sphere local
        var crustMat = EarthTextureLoader.CreateCrustMaterial();
        // 조각은 약간 어둡게 — 본체 텍스처 위에 균열감
        crustMat.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        for (int lat = 1; lat < latitudeBands; lat++)
        {
            float v = (float)lat / latitudeBands;
            float pitch = Mathf.Lerp(-90f, 90f, v) * Mathf.Deg2Rad;
            float y = Mathf.Sin(pitch);
            float ringR = Mathf.Cos(pitch);

            for (int lon = 0; lon < longitudeBands; lon++)
            {
                float u = (float)lon / longitudeBands;
                float yaw = u * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(yaw) * ringR, y, Mathf.Sin(yaw) * ringR).normalized;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Shard_{lat}_{lon}";
                go.transform.SetParent(_shardRoot, false);
                go.transform.localPosition = dir * (radius * 0.98f);
                go.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);

                float latScale = Mathf.Lerp(0.22f, 0.38f, 1f - Mathf.Abs(y));
                go.transform.localScale = new Vector3(latScale, shardThickness, latScale * 0.9f);

                var rend = go.GetComponent<Renderer>();
                rend.material = crustMat;
                // 본체와 겹침 방지: 살짝만 보이게 시작은 비활성에 가깝게 — 히트 시 강조
                rend.enabled = false;

                Destroy(go.GetComponent<Collider>());
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 0.4f;

                _shards.Add(new CrustShard
                {
                    Transform = go.transform,
                    Body = rb,
                    LocalDir = dir,
                    Detached = false
                });
            }
        }
    }

    void OnDamaged(float damagePercent, Vector3 worldPoint)
    {
        if (!_built) BuildShards();

        Vector3 localHit = transform.InverseTransformPoint(worldPoint).normalized;
        float radiusWorld = earth != null ? earth.Radius : 2.5f;
        float localDetach = detachRadius / Mathf.Max(0.01f, transform.lossyScale.x);

        // 데미지 높을수록 더 넓게 뜯김
        float radiusMul = Mathf.Lerp(0.75f, 1.6f, damagePercent / 100f);
        int detached = 0;

        // 가까운 순 정렬
        _shards.Sort((a, b) =>
        {
            float da = Vector3.Dot(a.LocalDir, localHit);
            float db = Vector3.Dot(b.LocalDir, localHit);
            return db.CompareTo(da);
        });

        foreach (var shard in _shards)
        {
            if (shard.Detached) continue;
            float ang = Vector3.Angle(shard.LocalDir, localHit);
            if (ang > 28f * radiusMul) continue;

            DetachShard(shard, worldPoint, radiusWorld);
            detached++;
            if (detached >= maxDetachPerHit) break;
        }

        // 데미지 마일스톤마다 랜덤 추가 붕괴
        if (Mathf.FloorToInt(damagePercent) % 20 < 8)
            DetachRandom(Mathf.Clamp(2 + (int)(damagePercent / 25f), 2, 6), worldPoint, radiusWorld);
    }

    void DetachRandom(int count, Vector3 worldPoint, float radiusWorld)
    {
        int n = 0;
        for (int i = 0; i < _shards.Count && n < count; i++)
        {
            int idx = Random.Range(0, _shards.Count);
            var shard = _shards[idx];
            if (shard.Detached) continue;
            DetachShard(shard, worldPoint, radiusWorld);
            n++;
        }
    }

    void DetachShard(CrustShard shard, Vector3 blastOrigin, float radiusWorld)
    {
        shard.Detached = true;
        var t = shard.Transform;
        var rend = t.GetComponent<Renderer>();
        if (rend != null) rend.enabled = true;

        // 월드로 분리
        t.SetParent(null, true);
        shard.Body.isKinematic = false;
        shard.Body.useGravity = false;

        Vector3 outward = (t.position - transform.position).normalized;
        Vector3 fromBlast = (t.position - blastOrigin).normalized;
        Vector3 force = (outward * 0.7f + fromBlast * 0.5f + Random.insideUnitSphere * 0.35f).normalized;
        shard.Body.AddForce(force * Random.Range(blastForce * 0.7f, blastForce * 1.3f), ForceMode.Impulse);
        shard.Body.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);

        // 용암 면 느낌
        if (Random.value > 0.55f && rend != null)
        {
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.05f) * 2.5f);
        }

        Destroy(t.gameObject, 6f);
    }
}
