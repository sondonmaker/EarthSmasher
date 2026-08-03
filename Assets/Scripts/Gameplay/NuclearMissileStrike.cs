using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// War 패널의 Nuke Missile: 버튼 → 지구 클릭 → ICBM 비행 → 핵폭발.
/// </summary>
public class NuclearMissileStrike : MonoBehaviour
{
    public static NuclearMissileStrike Instance { get; private set; }

    [SerializeField] Camera cam;
    [SerializeField] EarthPlanet earth;
    [SerializeField] float power = 1.35f;
    [SerializeField] float cooldown = 0.35f;
    [SerializeField] float tapMoveThreshold = 14f;
    [SerializeField] LayerMask earthMask = ~0;

    float readyAt;
    bool pressTracking;
    Vector2 pressPos;
    public bool IsAiming { get; private set; }

    public static NuclearMissileStrike Ensure()
    {
        var s = FindObjectOfType<NuclearMissileStrike>();
        if (s != null)
            return s;
        var go = new GameObject("NuclearMissileStrike");
        return go.AddComponent<NuclearMissileStrike>();
    }

    void Awake()
    {
        Instance = this;
        if (cam == null)
            cam = Camera.main;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void BeginAim()
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (cam == null)
            cam = Camera.main;
        IsAiming = true;
        pressTracking = false;
    }

    public void CancelAim()
    {
        IsAiming = false;
        pressTracking = false;
    }

    void Update()
    {
        if (!IsAiming)
            return;
        if (DisasterUiGate.ModalOpen)
        {
            CancelAim();
            return;
        }

        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame))
        {
            CancelAim();
            return;
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            CancelAim();
            return;
        }

        if (WeaponRailPanel.BlocksGameplay || EarthLayerToolbar.BlocksGameplayInput
            || ZoomUiBlocker.BlocksGameplay || WorldStatusHud.BlocksGameplay)
            return;

        if (Time.time < readyAt)
            return;

        if (!TryConsumeTap(out Vector2 screenPos))
            return;

        if (!TryGetEarthHit(screenPos, out Vector3 worldPoint))
            return;

        NuclearMissile.LaunchToWorldPoint(earth, worldPoint, power, -1f, null);
        readyAt = Time.time + cooldown;
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
                    pressTracking = true;
                    pressPos = t.position.ReadValue();
                    return false;
                }

                if (pressTracking && t.press.wasReleasedThisFrame)
                {
                    pressTracking = false;
                    Vector2 upPos = t.position.ReadValue();
                    if ((upPos - pressPos).magnitude <= tapMoveThreshold)
                    {
                        screenPos = upPos;
                        return true;
                    }
                }

                return false;
            }
        }

        var mouse = Mouse.current;
        if (mouse == null)
            return false;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressTracking = true;
            pressPos = mouse.position.ReadValue();
            return false;
        }

        if (pressTracking && mouse.leftButton.wasReleasedThisFrame)
        {
            pressTracking = false;
            Vector2 upPos = mouse.position.ReadValue();
            if ((upPos - pressPos).magnitude <= tapMoveThreshold)
            {
                screenPos = upPos;
                return true;
            }
        }

        return false;
    }

    bool TryGetEarthHit(Vector2 screenPos, out Vector3 worldPoint)
    {
        worldPoint = default;
        if (cam == null)
            cam = Camera.main;
        if (cam == null || earth == null)
            return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, earthMask))
            return false;
        if (hit.collider == null || hit.collider.GetComponentInParent<EarthPlanet>() != earth)
            return false;

        worldPoint = hit.point;
        return true;
    }

    void OnGUI()
    {
        if (!IsAiming)
            return;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(1f, 0.85f, 0.35f, 1f);
        GUI.Label(new Rect(0f, Screen.height - 56f, Screen.width, 28f),
            "NUKE MISSILE — click Earth to strike  (Esc / RMB cancel)", style);
    }
}
