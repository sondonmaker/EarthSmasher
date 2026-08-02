using System;
using UnityEngine;

/// <summary>
/// 참고 UI 스타일 상단 바: 날짜 / 속도 / 아이콘 / 인구 / 과학점수.
/// </summary>
public class WorldStatusHud : MonoBehaviour
{
    public static WorldStatusHud Instance { get; private set; }
    public static bool BlocksGameplay { get; private set; }

    [SerializeField] int sciencePoints = 600;
    [SerializeField] float minSpeed = 0.01f;
    [SerializeField] float maxSpeed = 100f;
    [SerializeField] float simSpeed = 0.1f;

    DateTime simDate = new DateTime(2026, 8, 23);
    float dayAccumulator;

    Texture2D barBg;
    Texture2D pillBg;
    Texture2D accentBg;
    Texture2D knobTex;

    GUIStyle dateStyle;
    GUIStyle speedStyle;
    GUIStyle pillStyle;
    GUIStyle iconStyle;
    GUIStyle alertStyle;

    OrbitCamera orbit;
    Rect barRect;

    public int SciencePoints
    {
        get => sciencePoints;
        set => sciencePoints = Mathf.Max(0, value);
    }

    public float SimSpeed => Mathf.Clamp(simSpeed, minSpeed, maxSpeed);

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        float speed = SimSpeed;
        // 인구 성장도 속도에 맞춤
        var pop = PopulationSystem.Instance;
        if (pop != null)
            pop.GrowthSpeedMultiplier = speed;

        // 날짜 진행 (x1 ≈ 하루/실시간 20초)
        dayAccumulator += Time.unscaledDeltaTime * speed / 20f;
        while (dayAccumulator >= 1f)
        {
            dayAccumulator -= 1f;
            simDate = simDate.AddDays(1);
        }
    }

    void EnsureStyles()
    {
        if (dateStyle != null)
            return;

        barBg = MakeTex(new Color(0.04f, 0.05f, 0.07f, 0.55f));
        pillBg = MakeTex(new Color(0.12f, 0.13f, 0.16f, 0.92f));
        accentBg = MakeTex(new Color(0.25f, 0.55f, 0.95f, 1f));
        knobTex = MakeTex(Color.white);

        dateStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        dateStyle.normal.textColor = Color.white;

        speedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        speedStyle.normal.textColor = Color.white;

        pillStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        pillStyle.normal.textColor = Color.white;

        iconStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        iconStyle.normal.textColor = Color.white;

        alertStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        alertStyle.normal.textColor = new Color(1f, 0.35f, 0.2f);
    }

    static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.SetPixels(new[] { c, c, c, c });
        t.Apply(false, true);
        return t;
    }

    void OnGUI()
    {
        EnsureStyles();
        if (orbit == null)
            orbit = FindObjectOfType<OrbitCamera>();

        float h = 44f;
        float pad = 10f;
        barRect = new Rect(0, 0, Screen.width, h + 8f);
        GUI.DrawTexture(barRect, barBg);

        float x = pad;
        float y = 8f;
        float rowH = 32f;

        // Date
        string dateStr = simDate.ToString("yyyy-MM-dd");
        GUI.Label(new Rect(x, y, 110, rowH), dateStr, dateStyle);
        x += 118f;

        // Speed: -  slider  +  label
        x = DrawSpeedControls(x, y, rowH);
        x += 16f;

        // Center icons
        x = DrawIconButton(x, y, rowH, "X", "Disaster", () =>
        {
            var panel = FindObjectOfType<EarthControlPanel>();
            if (panel != null)
                panel.OpenTab(EarthControlPanel.Tab.Disaster);
        });
        x = DrawIconButton(x, y, rowH, "O", "Recenter", () =>
        {
            var earth = FindObjectOfType<EarthPlanet>();
            if (orbit != null && earth != null)
            {
                orbit.SetTarget(earth.transform);
                orbit.FramePlanet(earth.Radius, 0.82f);
            }
        });
        x = DrawIconButton(x, y, rowH, "N", "Tech", null);
        x = DrawIconButton(x, y, rowH, "S", "Shop", null);
        x = DrawIconButton(x, y, rowH, "A", "Ads", null);
        x = DrawIconButton(x, y, rowH, "C", "Calendar", null);

        // Right pills: science then population (pop farthest right like reference)
        float right = Screen.width - pad;
        right = DrawSciencePill(right, y, rowH);
        right -= 10f;
        DrawPopulationPill(right, y, rowH);

        var war = NuclearWarSystem.Instance;
        if (war != null && war.IsRunning)
            GUI.Label(new Rect(0, h + 6f, Screen.width, 18f), "NUCLEAR WAR IN PROGRESS", alertStyle);

        BlocksGameplay = IsMouseInRect(barRect);
    }

    float DrawSpeedControls(float x, float y, float h)
    {
        float btn = 28f;
        if (CircleButton(new Rect(x, y + (h - btn) * 0.5f, btn, btn), "-"))
            StepSpeed(1f / 2f);
        x += btn + 6f;

        float sliderW = 90f;
        Rect sliderR = new Rect(x, y + h * 0.5f - 6f, sliderW, 12f);
        float t = SpeedToSlider(SimSpeed);
        float nt = GUI.HorizontalSlider(sliderR, t, 0f, 1f);
        if (Mathf.Abs(nt - t) > 0.0001f)
            simSpeed = SliderToSpeed(nt);

        float kx = sliderR.x + SpeedToSlider(SimSpeed) * sliderR.width - 5f;
        GUI.DrawTexture(new Rect(kx, sliderR.y - 2f, 10f, 16f), accentBg);
        x += sliderW + 6f;

        if (CircleButton(new Rect(x, y + (h - btn) * 0.5f, btn, btn), "+"))
            StepSpeed(2f);
        x += btn + 8f;

        GUI.Label(new Rect(x, y, 110, h), $"Speed x {FormatSpeed(SimSpeed)}", speedStyle);
        x += 108f;
        return x;
    }

    void StepSpeed(float factor)
    {
        simSpeed = Mathf.Clamp(SimSpeed * factor, minSpeed, maxSpeed);
        // snap to nice values when close
        float[] nice =
        {
            0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.5f,
            1f, 2f, 5f, 10f, 20f, 50f, 100f
        };
        float best = simSpeed;
        float bestDist = float.MaxValue;
        for (int i = 0; i < nice.Length; i++)
        {
            float d = Mathf.Abs(Mathf.Log(simSpeed / nice[i]));
            if (d < bestDist)
            {
                bestDist = d;
                best = nice[i];
            }
        }
        if (bestDist < 0.15f)
            simSpeed = best;
    }

    float SpeedToSlider(float speed)
    {
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        return Mathf.InverseLerp(Mathf.Log10(minSpeed), Mathf.Log10(maxSpeed), Mathf.Log10(speed));
    }

    float SliderToSpeed(float t)
    {
        float log = Mathf.Lerp(Mathf.Log10(minSpeed), Mathf.Log10(maxSpeed), Mathf.Clamp01(t));
        return Mathf.Pow(10f, log);
    }

    float DrawIconButton(float x, float y, float h, string icon, string tip, Action onClick)
    {
        Rect r = new Rect(x, y, 34f, h);
        if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            onClick?.Invoke();
        GUI.Label(r, icon, iconStyle);
        return x + 38f;
    }

    void DrawPopulationPill(float rightEdge, float y, float h)
    {
        var pop = PopulationSystem.Instance;
        string text = pop != null ? PopulationSystem.Format(pop.Population) : "—";
        float w = Mathf.Clamp(Measure(text) + 56f, 160f, 260f);
        Rect r = new Rect(rightEdge - w, y, w, h);
        GUI.DrawTexture(r, pillBg);
        GUI.Label(new Rect(r.x + 8, r.y, 28, h), "P", iconStyle);
        GUI.Label(new Rect(r.x + 36, r.y, r.width - 42, h), text, pillStyle);
    }

    float DrawSciencePill(float rightEdge, float y, float h)
    {
        string text = sciencePoints.ToString("#,0");
        float w = Mathf.Clamp(Measure(text) + 70f, 100f, 180f);
        Rect r = new Rect(rightEdge - w, y, w, h);
        GUI.DrawTexture(r, pillBg);
        GUI.Label(new Rect(r.x + 8, r.y, 28, h), "Sci", speedStyle);
        GUI.Label(new Rect(r.x + 40, r.y, r.width - 70, h), text, pillStyle);

        Rect plus = new Rect(r.xMax - 28, r.y + 4, 24, h - 8);
        GUI.DrawTexture(plus, accentBg);
        var plusStyle = new GUIStyle(iconStyle) { fontSize = 16 };
        GUI.Label(plus, "+", plusStyle);
        if (GUI.Button(plus, GUIContent.none, GUIStyle.none))
            sciencePoints += 50;

        return r.x;
    }

    bool CircleButton(Rect r, string label)
    {
        GUI.DrawTexture(r, accentBg);
        var s = new GUIStyle(iconStyle) { fontSize = 18 };
        GUI.Label(r, label, s);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    float Measure(string text)
    {
        EnsureStyles();
        return pillStyle.CalcSize(new GUIContent(text)).x;
    }

    static string FormatSpeed(float s)
    {
        if (s < 0.1f)
            return s.ToString("0.00");
        if (s < 1f)
            return s.ToString("0.0");
        if (s < 10f && !Mathf.Approximately(s, Mathf.Round(s)))
            return s.ToString("0.0");
        if (s >= 10f && Mathf.Abs(s - Mathf.Round(s)) < 0.05f)
            return Mathf.RoundToInt(s).ToString();
        return s.ToString("0.#");
    }

    static bool IsMouseInRect(Rect r)
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null)
            return false;
        Vector2 p = mouse.position.ReadValue();
        p.y = Screen.height - p.y;
        return r.Contains(p);
    }
}
