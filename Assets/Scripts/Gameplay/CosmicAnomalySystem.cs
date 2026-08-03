using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum CosmicAnomalyKind
{
    BlackHole,
    Vortex
}

/// <summary>블랙홀 / 보텍스: 조준 후 클릭한 지점에 소환.</summary>
public class CosmicAnomalySystem : MonoBehaviour
{
    public static CosmicAnomalySystem Instance { get; private set; }

    [SerializeField] Camera cam;
    [SerializeField] EarthPlanet earth;
    [SerializeField] float tapMoveThreshold = 14f;
    [SerializeField] LayerMask earthMask = ~0;

    CosmicAnomalyKind pending;
    bool pressTracking;
    Vector2 pressPos;

    public bool IsAiming { get; private set; }
    public CosmicAnomalyKind AimKind => pending;

    public static CosmicAnomalySystem Ensure()
    {
        var s = FindObjectOfType<CosmicAnomalySystem>();
        if (s != null)
            return s;
        return new GameObject("CosmicAnomalySystem").AddComponent<CosmicAnomalySystem>();
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

    public void BeginAim(CosmicAnomalyKind kind)
    {
        pending = kind;
        IsAiming = true;
        pressTracking = false;
        if (earth == null)
            earth = FindObjectOfType<EarthPlanet>();
        if (cam == null)
            cam = Camera.main;
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

        if (!TryConsumeTap(out Vector2 screenPos))
            return;
        if (!TryGetEarthHit(screenPos, out Vector3 worldPoint, out Vector3 normal))
            return;

        Spawn(pending, worldPoint, normal);
        CancelAim();
    }

    void Spawn(CosmicAnomalyKind kind, Vector3 point, Vector3 normal)
    {
        if (kind == CosmicAnomalyKind.BlackHole)
            StartCoroutine(RunBlackHole(point, normal));
        else
            StartCoroutine(RunVortex(point, normal));
    }

    IEnumerator RunBlackHole(Vector3 point, Vector3 normal)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BlackHole";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = point + normal * (earth != null ? earth.Radius * 0.35f : 1.2f);
        go.transform.localScale = Vector3.one * 0.15f;
        var rend = go.GetComponent<Renderer>();
        rend.material = RuntimeMaterial.Opaque(new Color(0.02f, 0.02f, 0.05f), 0f);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(go.transform, false);
        ring.transform.localScale = Vector3.one * 1.35f;
        ring.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(new Color(0.55f, 0.2f, 1f, 0.35f));

        CameraShake.Shake(0.12f, 0.25f);
        float t = 0f;
        while (t < 4.5f)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / 4.5f);
            float s = Mathf.Lerp(0.15f, 1.1f, Mathf.Sin(u * Mathf.PI));
            go.transform.localScale = Vector3.one * s;
            go.transform.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
            if (earth != null)
            {
                var deform = EarthCraterDeform.Ensure(earth);
                if (deform != null && Time.frameCount % 8 == 0)
                    deform.Dig(point, 0.12f * u, 0.04f * u, false);
            }
            yield return null;
        }

        Object.Destroy(go);
    }

    IEnumerator RunVortex(Vector3 point, Vector3 normal)
    {
        var root = new GameObject("Vortex");
        root.transform.position = point + normal * 0.2f;
        root.transform.rotation = Quaternion.LookRotation(normal);

        for (int i = 0; i < 5; i++)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float r = 0.25f + i * 0.18f;
            ring.transform.localScale = new Vector3(r, 0.02f, r);
            ring.GetComponent<Renderer>().material = RuntimeMaterial.UnlitTransparent(
                new Color(0.25f, 0.85f, 1f, 0.45f - i * 0.06f));
        }

        CameraShake.Shake(0.08f, 0.2f);
        float t = 0f;
        while (t < 3.8f)
        {
            t += Time.deltaTime;
            root.transform.Rotate(normal, 220f * Time.deltaTime, Space.World);
            float pulse = 1f + 0.08f * Mathf.Sin(t * 8f);
            root.transform.localScale = Vector3.one * pulse;
            if (earth != null && Time.frameCount % 10 == 0)
            {
                var deform = EarthCraterDeform.Ensure(earth);
                if (deform != null)
                    deform.Dig(point, 0.1f, 0.03f, false);
            }
            yield return null;
        }

        Object.Destroy(root);
    }

    bool TryConsumeTap(out Vector2 screenPos)
    {
        screenPos = default;
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
            Vector2 up = mouse.position.ReadValue();
            if ((up - pressPos).magnitude <= tapMoveThreshold)
            {
                screenPos = up;
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
        style.normal.textColor = new Color(0.75f, 0.9f, 1f, 1f);
        string name = pending == CosmicAnomalyKind.BlackHole ? "BLACK HOLE" : "VORTEX";
        GUI.Label(new Rect(0f, Screen.height - 56f, Screen.width, 28f),
            name + " — click Earth to summon  (Esc / RMB cancel)", style);
    }
}
