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

    bool _focusing;
    float _focusYaw;
    float _focusPitch;
    float _focusDistance;
    float _focusDuration;
    float _focusT;
    float _fromYaw;
    float _fromPitch;
    float _fromDistance;

    bool _chasing;
    Transform _chaseSubject;
    Transform _chaseLookAt;
    float _chaseDistance;
    float _chaseSide;

    bool _surfaceFocus;
    Vector3 _focusFromPos;
    Vector3 _focusToPos;
    Vector3 _focusLookAt;
    float _focusLockUntil;

    public float Distance => distance;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
    public bool IsDragging => _dragging;
    public bool IsChasing => _chasing;

    public void SetTarget(Transform t) => target = t;

    /// <summary>
    /// 대상(달) 뒤를 따라가며 lookAt(지구) 쪽을 본다. 드래그하면 해제.
    /// </summary>
    public void BeginChase(Transform subject, Transform lookAt, float distanceBehind, float sideBias = 0.35f)
    {
        if (subject == null)
            return;
        _chasing = true;
        _focusing = false;
        _dragging = false;
        _chaseSubject = subject;
        _chaseLookAt = lookAt;
        _chaseDistance = Mathf.Max(0.5f, distanceBehind);
        _chaseSide = sideBias;
    }

    public void EndChase(bool resyncOrbit = true)
    {
        if (!_chasing)
            return;
        _chasing = false;
        _chaseSubject = null;
        _chaseLookAt = null;
        if (resyncOrbit)
            ResyncOrbitFromPose();
    }

    void ResyncOrbitFromPose()
    {
        if (target == null)
            return;
        Vector3 toCam = transform.position - target.position;
        float d = toCam.magnitude;
        if (d < 1e-4f)
            return;
        toCam /= d;
        distance = Mathf.Clamp(d, minDistance, maxDistance);
        yaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
        pitch = Mathf.Asin(Mathf.Clamp(toCam.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    /// <summary>
    /// 지구 표면 방향(월드)이 화면 중앙에 오도록 궤도를 돌린다.
    /// </summary>
    public void FocusOnWorldDirection(Vector3 worldDirFromCenter, float duration = 0.85f, float zoomFill = 0.72f)
    {
        if (target == null)
            return;

        Vector3 toCam = worldDirFromCenter.normalized;
        if (toCam.sqrMagnitude < 1e-6f)
            return;

        // ApplyTransform: offset = Euler(pitch,yaw,0) * (0,0,-distance)
        _focusYaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
        _focusPitch = Mathf.Asin(Mathf.Clamp(toCam.y, -1f, 1f)) * Mathf.Rad2Deg;
        _focusPitch = Mathf.Clamp(_focusPitch, minPitch, maxPitch);

        float planetR = 2.5f;
        var earth = target.GetComponent<EarthPlanet>();
        if (earth != null)
            planetR = earth.Radius;
        float fov = Camera.main != null ? Camera.main.fieldOfView : 50f;
        float half = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        _focusDistance = Mathf.Clamp(
            (planetR / Mathf.Max(0.05f, half)) / Mathf.Clamp(zoomFill, 0.4f, 0.95f),
            minDistance, maxDistance);

        _fromYaw = yaw;
        _fromPitch = pitch;
        _fromDistance = distance;
        // shortest yaw lerp
        float dy = Mathf.DeltaAngle(_fromYaw, _focusYaw);
        _focusYaw = _fromYaw + dy;

        _focusDuration = Mathf.Max(0.05f, duration);
        _focusT = 0f;
        _focusing = true;
        _dragging = false;
    }

    /// <summary>
    /// 달이 지구로 돌진하는 3/4 시네마틱 구도.
    /// approachOutward = 충돌점 바깥 방향(달 시작 쪽).
    /// </summary>
    public void FrameApproachShot(Vector3 approachOutward, float planetRadius, float duration = 0.75f)
    {
        if (target == null)
            return;

        Vector3 approach = approachOutward.normalized;
        Vector3 side = Vector3.Cross(approach, Vector3.up);
        if (side.sqrMagnitude < 1e-4f)
            side = Vector3.Cross(approach, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, approach).normalized;

        // 옆·살짝 위 — 달이 프레임 가장자리에서 지구로 들어오는 구도
        Vector3 camDir = (-approach * 0.25f + side * 0.9f + up * 0.32f).normalized;

        _focusYaw = Mathf.Atan2(camDir.x, camDir.z) * Mathf.Rad2Deg;
        _focusPitch = Mathf.Asin(Mathf.Clamp(camDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        _focusPitch = Mathf.Clamp(_focusPitch, minPitch, maxPitch);

        float fov = Camera.main != null ? Camera.main.fieldOfView : 50f;
        float half = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        // 멀리 잡아 달+지구가 같이 보이게
        float want = (planetRadius / Mathf.Max(0.05f, half)) / 0.28f;
        _focusDistance = Mathf.Clamp(want, minDistance, maxDistance);

        _fromYaw = yaw;
        _fromPitch = pitch;
        _fromDistance = distance;
        _focusYaw = _fromYaw + Mathf.DeltaAngle(_fromYaw, _focusYaw);
        _focusDuration = Mathf.Max(0.05f, duration);
        _focusT = 0f;
        _focusing = true;
        _dragging = false;
    }

    /// <summary>클릭한 지표면 지점을 화면 중앙에 — yaw/pitch 클램프 오차 없이 직접 이동.</summary>
    public void FocusOnSurfaceHit(Vector3 worldHit, float heightAboveSurfaceMul = 0.22f, float duration = 0.45f)
    {
        if (target == null)
            return;

        Vector3 center = target.position;
        Vector3 toHit = worldHit - center;
        if (toHit.sqrMagnitude < 1e-6f)
            return;

        float planetR = 2.5f;
        var earth = target.GetComponent<EarthPlanet>();
        if (earth != null)
            planetR = earth.Radius;

        Vector3 outward = toHit.normalized;
        float fov = Camera.main != null ? Camera.main.fieldOfView : 50f;
        float half = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float wantDist = (planetR / Mathf.Max(0.05f, half)) / 0.62f + planetR * heightAboveSurfaceMul * 0.12f;
        wantDist = Mathf.Clamp(wantDist, minDistance, maxDistance);

        _focusFromPos = transform.position;
        _focusToPos = center + outward * wantDist;
        _focusLookAt = worldHit + outward * (planetR * heightAboveSurfaceMul);
        _focusDuration = Mathf.Max(0.05f, duration);
        _focusT = 0f;
        _focusing = true;
        _surfaceFocus = true;
        _chasing = false;
        _dragging = false;
        _focusLockUntil = Time.unscaledTime + _focusDuration + 0.2f;
    }

    /// <summary>밈 빌보드(트럼프 등)가 지구와 함께 잘 보이도록 해당 면을 바라보며 줌아웃.</summary>
    public void FrameMemeBillboard(Vector3 surfaceNormalWorld, float heightAboveSurfaceMul, float duration = 0.95f)
    {
        if (target == null)
            return;

        float planetR = 2.5f;
        var earth = target.GetComponent<EarthPlanet>();
        if (earth != null)
            planetR = earth.Radius;

        Vector3 worldHit = target.position + surfaceNormalWorld.normalized * planetR;
        FocusOnSurfaceHit(worldHit, heightAboveSurfaceMul, duration);
    }

    void EndSurfaceFocus()
    {
        _surfaceFocus = false;
        ResyncOrbitFromPose();
    }

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

    public void GetOrbitState(out float outYaw, out float outPitch, out float outDistance)
    {
        outYaw = yaw;
        outPitch = pitch;
        outDistance = distance;
    }

    public void SetOrbitState(float outYaw, float outPitch, float outDistance)
    {
        yaw = outYaw;
        pitch = Mathf.Clamp(outPitch, minPitch, maxPitch);
        distance = Mathf.Clamp(outDistance, minDistance, maxDistance);
        _focusing = false;
        _surfaceFocus = false;
        _chasing = false;
    }

    public void ResetToDefaults()
    {
        EndChase(false);
        _focusing = false;
        _surfaceFocus = false;
        _dragging = false;
        _chaseSubject = null;
        _chaseLookAt = null;
        yaw = 30f;
        pitch = 12f;

        if (target != null)
        {
            var earth = target.GetComponent<EarthPlanet>();
            if (earth != null)
                FramePlanet(earth.Radius, 0.58f);
        }

        ApplyTransform();
    }

    void LateUpdate()
    {
        if (target == null && !_chasing) return;

        // 사이드/HUD/줌 UI 위에 있을 때만 막음. 재해 연출·리포트 열려 있어도 카메라 자유.
        bool uiBlocks = EarthLayerToolbar.BlocksGameplayInput
            || ZoomUiBlocker.BlocksGameplay
            || WorldStatusHud.BlocksGameplay
            || WeaponRailPanel.BlocksGameplay;

        // 드래그/줌이면 자동 포커스·추적 즉시 끊고 같은 프레임에 조작
        if (!uiBlocks && WantsOrbitInterrupt())
        {
            if (_focusing)
                _focusing = false;
            if (_chasing)
                EndChase(true);
        }

        if (_chasing && _chaseSubject != null)
        {
            ApplyChaseTransform();
            HandleZoom();
            return;
        }

        if (target == null) return;

        if (_focusing)
        {
            _focusT += Time.unscaledDeltaTime / _focusDuration;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_focusT));

            if (_surfaceFocus)
            {
                transform.position = Vector3.Lerp(_focusFromPos, _focusToPos, u);
                transform.LookAt(_focusLookAt);
                if (_focusT >= 1f)
                {
                    _focusing = false;
                    EndSurfaceFocus();
                }
            }
            else
            {
                yaw = Mathf.Lerp(_fromYaw, _focusYaw, u);
                pitch = Mathf.Lerp(_fromPitch, _focusPitch, u);
                distance = Mathf.Lerp(_fromDistance, _focusDistance, u);
                if (_focusT >= 1f)
                    _focusing = false;
            }
        }

        HandleZoom();
        if (!_focusing && !uiBlocks && !WeaponRailPanel.BlocksOrbitCamera)
            HandleOrbit();

        if (!_surfaceFocus || !_focusing)
            ApplyTransform();
    }

    void ApplyChaseTransform()
    {
        Vector3 subject = _chaseSubject.position;
        Vector3 lookPt = _chaseLookAt != null ? _chaseLookAt.position : subject;
        Vector3 toLook = lookPt - subject;
        if (toLook.sqrMagnitude < 1e-6f)
            toLook = -transform.forward;
        toLook.Normalize();

        Vector3 side = Vector3.Cross(toLook, Vector3.up);
        if (side.sqrMagnitude < 1e-4f)
            side = Vector3.Cross(toLook, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, toLook).normalized;

        // 달 뒤·옆에서 지구 방향 — 달이 항상 프레임에 들어오게
        transform.position = subject
            - toLook * _chaseDistance
            + side * (_chaseDistance * _chaseSide)
            + up * (_chaseDistance * 0.18f);
        transform.LookAt(Vector3.Lerp(subject, lookPt, 0.42f), up);
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

    bool WantsOrbitInterrupt()
    {
        if (Time.unscaledTime < _focusLockUntil)
            return false;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame)
                return true;
            if (mouse.scroll.ReadValue().y != 0f)
                return true;
        }

        var touchscreen = Touchscreen.current;
        if (touchscreen != null && ActiveTouchCount(touchscreen) > 0)
            return true;

        var kb = Keyboard.current;
        if (kb != null && (kb.equalsKey.isPressed || kb.minusKey.isPressed || kb.numpadPlusKey.isPressed || kb.numpadMinusKey.isPressed))
            return true;

        return false;
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
