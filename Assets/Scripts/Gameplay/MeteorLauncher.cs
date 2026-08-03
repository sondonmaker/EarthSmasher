using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 화면 탭/클릭으로 지구 표면을 조준해 운석을 발사한다.
/// Input System 패키지 사용.
/// </summary>
public class MeteorLauncher : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] EarthPlanet earth;
    [SerializeField] MeteorProjectile meteorPrefab;
    [SerializeField] float damagePerHit = 8f;
    [SerializeField] float cooldown = 0.12f;
    [SerializeField] LayerMask earthMask = ~0;
    [SerializeField] float tapMoveThreshold = 14f;

    float _readyAt;
    ImpactHud _hud;
    OrbitCamera _orbit;
    bool _pressTracking;
    Vector2 _pressPos;

    public float HitRatePercent => 100f;

    public void Configure(Camera camera, EarthPlanet planet, ImpactHud hud = null)
    {
        cam = camera;
        earth = planet;
        _hud = hud;
        if (cam != null) _orbit = cam.GetComponent<OrbitCamera>();
    }

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (_hud == null) _hud = FindObjectOfType<ImpactHud>();
        if (cam != null) _orbit = cam.GetComponent<OrbitCamera>();
    }

    void Update()
    {
        if (earth == null) return;
        if (EarthLayerToolbar.BlocksGameplayInput || ZoomUiBlocker.BlocksGameplay || WorldStatusHud.BlocksGameplay) return;
        if (WeaponRailPanel.BlocksGameplay) return;
        if (DisasterUiGate.ModalOpen) return;
        if (WeaponRailPanel.IsArmed) return;
        if (NuclearMissileStrike.Instance != null && NuclearMissileStrike.Instance.IsAiming) return;
        if (CosmicAnomalySystem.Instance != null && CosmicAnomalySystem.Instance.IsAiming) return;

        RefreshHud();

        if (Time.time < _readyAt) return;
        if (!TryConsumeTap(out Vector2 screenPos)) return;
        if (_orbit != null && _orbit.IsDragging) return;

        if (TryGetEarthHit(screenPos, out RaycastHit hit))
            Fire(hit.point, hit.normal);
    }

    /// <summary>지정 지점에 소행성 발사.</summary>
    public void FireAt(Vector3 point, Vector3 normal)
    {
        if (earth == null || Time.time < _readyAt)
            return;
        Fire(point, normal);
    }

    /// <summary>UI 버튼용: 카메라 쪽 지구 표면에 소행성(좌클릭과 동일)을 떨어뜨린다.</summary>
    public void FireTowardCamera()
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (earth == null || Time.time < _readyAt)
            return;
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        Vector3 dir = (cam.transform.position - earth.transform.position).normalized;
        if (dir.sqrMagnitude < 1e-6f)
            dir = Random.onUnitSphere;
        Vector3 point = earth.transform.position + dir * earth.Radius;
        Fire(point, dir);
    }

    void Fire(Vector3 point, Vector3 normal)
    {
        MeteorProjectile meteor;
        if (meteorPrefab != null)
            meteor = Instantiate(meteorPrefab);
        else
            meteor = CreateRuntimeMeteor();

        meteor.Launch(earth, point, normal, damagePerHit);
        _readyAt = Time.time + cooldown;
        RefreshHud(meteor);
    }

    MeteorProjectile CreateRuntimeMeteor()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Meteor";
        // 울퉁불퉁한 암석 느낌
        go.transform.localScale = new Vector3(0.38f, 0.32f, 0.42f);
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.28f, 0.22f, 0.18f), 0.15f);
        Destroy(go.GetComponent<Collider>());
        go.AddComponent<MeteorTrail>();
        return go.AddComponent<MeteorProjectile>();
    }

    bool TryGetEarthHit(Vector2 screenPos, out RaycastHit hit)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out hit, 500f, earthMask))
            return hit.collider != null && hit.collider.GetComponentInParent<EarthPlanet>() == earth;
        return false;
    }

    void RefreshHud(MeteorProjectile tracking = null)
    {
        if (_hud == null) return;
        float eta = tracking != null ? tracking.CountdownSeconds : 0f;
        _hud.SetImpactCountdown(eta);
        _hud.SetHitRate(HitRatePercent);
        _hud.SetImpactCount(earth != null ? earth.ImpactCount : 0);
        _hud.SetTargeting(tracking != null);
    }

    bool TryConsumeTap(out Vector2 screenPos)
    {
        screenPos = default;

        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            int active = 0;
            int idx = -1;
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var t = touchscreen.touches[i];
                if (t.press.isPressed || t.press.wasPressedThisFrame || t.press.wasReleasedThisFrame)
                {
                    active++;
                    idx = i;
                }
            }

            if (active == 1 && idx >= 0)
            {
                var t = touchscreen.touches[idx];
                if (t.press.wasPressedThisFrame)
                {
                    _pressTracking = true;
                    _pressPos = t.position.ReadValue();
                    return false;
                }

                if (_pressTracking && t.press.wasReleasedThisFrame)
                {
                    _pressTracking = false;
                    Vector2 upPos = t.position.ReadValue();
                    if ((upPos - _pressPos).magnitude <= tapMoveThreshold)
                    {
                        screenPos = upPos;
                        return true;
                    }
                }

                return false;
            }
        }

        var mouse = Mouse.current;
        if (mouse == null) return false;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _pressTracking = true;
            _pressPos = mouse.position.ReadValue();
            return false;
        }

        if (_pressTracking && mouse.leftButton.wasReleasedThisFrame)
        {
            _pressTracking = false;
            Vector2 upPos = mouse.position.ReadValue();
            if ((upPos - _pressPos).magnitude <= tapMoveThreshold)
            {
                screenPos = upPos;
                return true;
            }
        }

        return false;
    }
}
