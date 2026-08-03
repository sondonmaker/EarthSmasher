using UnityEngine;
using UnityEngine.InputSystem;

public enum NukeStrikeKind
{
    Nuclear,
    Antimatter,
    Guided,
    FusionCore,
    MiningDrill
}

/// <summary>
/// 미사일/코어 조준: 버튼 → 지구 클릭 → 효과.
/// </summary>
public class NuclearMissileStrike : MonoBehaviour
{
    public static NuclearMissileStrike Instance { get; private set; }

    [SerializeField] Camera cam;
    [SerializeField] EarthPlanet earth;
    [SerializeField] float cooldown = 0.35f;
    [SerializeField] float tapMoveThreshold = 14f;
    [SerializeField] LayerMask earthMask = ~0;

    float readyAt;
    bool pressTracking;
    Vector2 pressPos;
    NukeStrikeKind kind = NukeStrikeKind.Nuclear;

    public bool IsAiming { get; private set; }
    public NukeStrikeKind AimKind => kind;

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

    public void BeginAim(NukeStrikeKind strikeKind = NukeStrikeKind.Nuclear)
    {
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (cam == null)
            cam = Camera.main;
        kind = strikeKind;
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

        if (!TryGetEarthHit(screenPos, out Vector3 worldPoint, out Vector3 normal))
            return;

        FireAt(worldPoint, normal);
        readyAt = Time.time + cooldown;
    }

    void FireAt(Vector3 worldPoint, Vector3 normal)
    {
        if (earth == null)
            return;

        switch (kind)
        {
            case NukeStrikeKind.FusionCore:
                NuclearBlast.Play(earth, worldPoint, normal, 1.8f);
                CameraShake.Shake(0.16f, 0.28f);
                break;
            case NukeStrikeKind.MiningDrill:
            {
                var deform = EarthCraterDeform.Ensure(earth);
                if (deform != null)
                    deform.Dig(worldPoint, 0.16f, 0.1f, true);
                CameraShake.Shake(0.06f, 0.12f);
                break;
            }
            case NukeStrikeKind.Antimatter:
                NuclearMissile.LaunchToWorldPoint(earth, worldPoint, 2.1f, -1f, null);
                break;
            case NukeStrikeKind.Guided:
                NuclearMissile.LaunchToWorldPoint(earth, worldPoint, 1.15f, 2.2f, null);
                break;
            default:
                NuclearMissile.LaunchToWorldPoint(earth, worldPoint, 1.35f, -1f, null);
                break;
        }
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

    bool TryGetEarthHit(Vector2 screenPos, out Vector3 worldPoint, out Vector3 normal)
    {
        worldPoint = default;
        normal = Vector3.up;
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
        normal = hit.normal;
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
            LabelFor(kind) + " — click Earth  (Esc / RMB cancel)", style);
    }

    static string LabelFor(NukeStrikeKind k)
    {
        switch (k)
        {
            case NukeStrikeKind.Antimatter: return "ANTIMATTER MISSILE";
            case NukeStrikeKind.Guided: return "GUIDED MISSILE";
            case NukeStrikeKind.FusionCore: return "FUSION CORE";
            case NukeStrikeKind.MiningDrill: return "MINING DRILL";
            default: return "NUKE MISSILE";
        }
    }
}
