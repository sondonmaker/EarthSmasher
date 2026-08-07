/// <summary>줌 UI 제거 후 호환용 — 항상 false.</summary>
public static class ZoomUiBlocker
{
    public static bool BlocksGameplay { get; private set; }
    public static void SetBlocked(bool v) => BlocksGameplay = v;
}
