using UnityEngine;

/// <summary>
/// 화면 고정 줌 버튼. 지구만 확대/축소, 메뉴 크기 유지.
/// </summary>
public class ZoomControls : MonoBehaviour
{
    [SerializeField] OrbitCamera orbit;
    [SerializeField] float buttonZoomStep = 0.85f;

    public void Bind(OrbitCamera cam) => orbit = cam;

    void OnGUI()
    {
        if (orbit == null) orbit = FindObjectOfType<OrbitCamera>();
        if (orbit == null) return;

        float size = 48f;
        float gap = 10f;
        float x = Screen.width - size - 18f;
        float y = Screen.height - size * 2f - gap - 18f;

        var prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.15f, 0.2f, 0.3f, 0.85f);

        if (GUI.Button(new Rect(x, y, size, size), "+"))
            orbit.Zoom(buttonZoomStep);

        if (GUI.Button(new Rect(x, y + size + gap, size, size), "−"))
            orbit.Zoom(-buttonZoomStep);

        // 줌 슬라이더
        float sliderH = size * 2f + gap;
        float t = Mathf.InverseLerp(orbit.MinDistance, orbit.MaxDistance, orbit.Distance);
        // 위=+ 줌인 이므로 슬라이더 반전 표시
        float nt = GUI.VerticalSlider(new Rect(x - 28f, y, 18f, sliderH), 1f - t, 0f, 1f);
        float newT = 1f - nt;
        if (Mathf.Abs(newT - t) > 0.001f)
            orbit.ZoomToward(newT);

        GUI.backgroundColor = prev;

        // 이 영역에서는 운석 발사만 막고, 줌은 허용
        Rect block = new Rect(x - 36f, y - 8f, size + 50f, sliderH + 16f);
        ZoomUiBlocker.SetBlocked(IsMouseInRect(block));
    }

    static bool IsMouseInRect(Rect r)
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return false;
        Vector2 p = mouse.position.ReadValue();
        p.y = Screen.height - p.y;
        return r.Contains(p);
    }
}

/// <summary>줌 UI 위에서는 운석/드래그 차단</summary>
public static class ZoomUiBlocker
{
    public static bool BlocksGameplay { get; private set; }
    public static void SetBlocked(bool v) => BlocksGameplay = v;
}
