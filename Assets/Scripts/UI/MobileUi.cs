using UnityEngine;

/// <summary>
/// IMGUI 전체 배율. 폰 고해상도 화면에서 버튼이 손톱만 하게 나오는 걸 막는다.
///
/// 사용법: OnGUI 시작에 Begin(), 끝에 End(). 그리고 레이아웃에는
/// Screen.width/height 대신 MobileUi.Width/Height를 쓴다.
/// </summary>
public static class MobileUi
{
    /// <summary>가로 화면 기준 설계 높이. 이 높이를 채우도록 배율이 정해진다.</summary>
    const float DesignHeightMobile = 520f;
    const float DesignHeightDesktop = 1080f;

    static float cachedScale = 1f;
    static int cachedW;
    static int cachedH;
    static Matrix4x4 previous;

    public static bool IsTouchDevice =>
        Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null;

    public static float Scale
    {
        get
        {
            if (cachedW != Screen.width || cachedH != Screen.height)
            {
                cachedW = Screen.width;
                cachedH = Screen.height;
                cachedScale = Compute();
            }
            return cachedScale;
        }
    }

    static float Compute()
    {
        float shortSide = Mathf.Min(Screen.width, Screen.height);
        if (Application.isMobilePlatform)
            return Mathf.Clamp(shortSide / DesignHeightMobile, 1.4f, 3.2f);

        // 데스크톱은 기존 크기 유지, 4K 같은 초고해상도에서만 키운다.
        return Mathf.Clamp(shortSide / DesignHeightDesktop, 1f, 2f);
    }

    public static float Width => Screen.width / Scale;
    public static float Height => Screen.height / Scale;

    public static void Begin()
    {
        previous = GUI.matrix;
        float s = Scale;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
    }

    public static void End()
    {
        GUI.matrix = previous;
    }

    /// <summary>포인터 화면 좌표(아래가 0) → GUI 좌표(위가 0, 배율 적용).</summary>
    public static Vector2 ScreenToGui(Vector2 screenPos)
    {
        float s = Scale;
        return new Vector2(screenPos.x / s, (Screen.height - screenPos.y) / s);
    }
}
