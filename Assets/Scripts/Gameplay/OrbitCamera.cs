using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 지구 궤도 카메라. 휠/핀치/+- 키/버튼으로 줌(지구만 확대축소, UI는 화면 고정).
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float distance = 7.2f;
    [SerializeField] float minDistance = 4.5f;
    [SerializeField] float maxDistance = 28f;
    [SerializeField] float orbitSensitivity = 0.25f;
    [SerializeField] float pinchZoomSensitivity = 0.02f;
    [SerializeField] float mouseZoomSensitivity = 0.85f;
    [SerializeField] float yaw = 30f;
    [SerializeField] float pitch = 12f;
    [SerializeField] float minPitch = -70f;
    [SerializeField] float maxPitch = 70f;
    [SerializeField] float dragThresholdPixels = 12f;
    [SerializeField] float keyZoomSpeed = 4f;

    bool _dragging;
    Vector2 _pressPos;
    Vector2 _lastPos;

    public float Distance => distance;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
    public bool IsDragging => _dragging;

    public void SetTarget(Transform t) => target = t;

    /// <summary>시작 시 지구를 크게, 줌아웃하면 은하가 보이게 범위 설정.</summary>
    public void FramePlanet(float radius, float fill = 0.82f)
    {
        float fov = Camera.main != null ? Camera.main.fieldOfView : 50f;
        float half = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fitDistance = (radius / Mathf.Max(0.05f, half)) / Mathf.Clamp(fill, 0.4f, 0.95f);

        distance = fitDistance;
        minDistance = fitDistance * 0.5f;
        maxDistance = fitDistance * 7f; // 멀리 보면 은하수
    }

    /// <summary>양수 = 줌인(가까이), 음수 = 줌아웃</summary>
    public void Zoom(float delta)
    {
        distance = Mathf.Clamp(distance - delta, minDistance, maxDistance);
    }

    public void ZoomToward(float t)
    {
        distance = Mathf.Lerp(minDistance, maxDistance, Mathf.Clamp01(t));
    }

    void LateUpdate()
    {
        if (target == null) return;
        HandleZoom();
        if (!EarthLayerToolbar.BlocksGameplayInput && !ZoomUiBlocker.BlocksGameplay)
            HandleOrbit();
        ApplyTransform();
    }

    void HandleZoom()
    {
        // 마우스 휠 (Input System: 보통 ±120)
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float notches = scroll / 120f;
                if (Mathf.Abs(notches) < 0.01f) notches = Mathf.Sign(scroll);
                Zoom(notches * mouseZoomSensitivity);
            }
        }

        // 핀치
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && ActiveTouchCount(touchscreen) >= 2)
        {
            var t0 = touchscreen.touches[0];
            var t1 = touchscreen.touches[1];
            Vector2 pos0 = t0.position.ReadValue();
            Vector2 pos1 = t1.position.ReadValue();
            float prev = ((pos0 - t0.delta.ReadValue()) - (pos1 - t1.delta.ReadValue())).magnitude;
            float curr = (pos0 - pos1).magnitude;
            Zoom((curr - prev) * pinchZoomSensitivity);
            _dragging = false;
        }

        // 키보드 + / - 또는 [ ]
        var kb = Keyboard.current;
        if (kb != null)
        {
            float key = 0f;
            if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed || kb.leftBracketKey.isPressed)
                key += 1f;
            if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed || kb.rightBracketKey.isPressed)
                key -= 1f;
            if (Mathf.Abs(key) > 0.01f)
                Zoom(key * keyZoomSpeed * Time.deltaTime);
        }
    }

    void HandleOrbit()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && ActiveTouchCount(touchscreen) >= 2)
            return; // 핀치는 줌만

        if (TryGetPointer(out bool down, out bool held, out bool up, out Vector2 pos))
        {
            if (down)
            {
                _pressPos = pos;
                _lastPos = pos;
                _dragging = false;
            }
            else if (held)
            {
                if (!_dragging && (pos - _pressPos).magnitude >= dragThresholdPixels)
                    _dragging = true;

                if (_dragging)
                {
                    Vector2 delta = pos - _lastPos;
                    yaw += delta.x * orbitSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, minPitch, maxPitch);
                }

                _lastPos = pos;
            }
            else if (up)
            {
                _dragging = false;
            }
        }
    }

    void ApplyTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rot * new Vector3(0f, 0f, -distance);
        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }

    static int ActiveTouchCount(Touchscreen ts)
    {
        int n = 0;
        for (int i = 0; i < ts.touches.Count; i++)
        {
            var t = ts.touches[i];
            if (t.press.isPressed || t.press.wasPressedThisFrame || t.press.wasReleasedThisFrame)
                n++;
        }
        return n;
    }

    static bool TryGetPointer(out bool down, out bool held, out bool up, out Vector2 pos)
    {
        down = held = up = false;
        pos = default;

        var touchscreen = Touchscreen.current;
        if (touchscreen != null && ActiveTouchCount(touchscreen) == 1)
        {
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var t = touchscreen.touches[i];
                if (!(t.press.isPressed || t.press.wasPressedThisFrame || t.press.wasReleasedThisFrame))
                    continue;

                pos = t.position.ReadValue();
                down = t.press.wasPressedThisFrame;
                held = t.press.isPressed;
                up = t.press.wasReleasedThisFrame;
                return true;
            }
        }

        var mouse = Mouse.current;
        if (mouse == null) return false;

        pos = mouse.position.ReadValue();
        down = mouse.leftButton.wasPressedThisFrame;
        held = mouse.leftButton.isPressed;
        up = mouse.leftButton.wasReleasedThisFrame;
        return down || held || up;
    }
}
