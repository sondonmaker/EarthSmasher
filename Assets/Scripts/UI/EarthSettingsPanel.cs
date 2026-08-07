using UnityEngine;

/// <summary>
/// 상단 ⚙ 버튼으로 여는 재해/실행 설정 패널.
/// </summary>
public class EarthSettingsPanel : MonoBehaviour
{
    public static EarthSettingsPanel Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;
    public static bool BlocksGameplayInput { get; private set; }

    const float PanelW = 300f;
    const float Top = 56f;
    const float PanelH = 520f;

    bool open;
    Vector2 scroll;
    int expandedDisaster = -1;
    int nuclearUnits = 100;
    string nuclearUnitsEdit = "100";
    string statusMsg;

    float asteroidDiameter = 1f;
    string asteroidEdit = "1.0";
    float icbmMt = 1f;
    string icbmEdit = "1";
    float earthquakeM = 1f;
    string earthquakeEdit = "1.0";
    string waterEdit = "1e20";

    Texture2D panelBg;
    Texture2D rowBg;
    Texture2D expandBg;
    GUIStyle titleStyle;
    GUIStyle nameStyle;
    GUIStyle unitStyle;
    GUIStyle valueStyle;
    GUIStyle hintStyle;
    GUIStyle boxStyle;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        BlocksGameplayInput = false;
    }

    public void Toggle()
    {
        open = !open;
        if (!open)
            expandedDisaster = -1;
    }

    public void Close()
    {
        open = false;
        expandedDisaster = -1;
    }

    public void ResetToDefaults()
    {
        open = false;
        expandedDisaster = -1;
        scroll = Vector2.zero;
        nuclearUnits = 100;
        nuclearUnitsEdit = "100";
        statusMsg = null;
        asteroidDiameter = 1f;
        asteroidEdit = "1.0";
        icbmMt = 1f;
        icbmEdit = "1";
        earthquakeM = 1f;
        earthquakeEdit = "1.0";
        waterEdit = "1e20";
    }

    void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        panelBg = MakeTex(new Color(0.06f, 0.07f, 0.09f, 0.94f));
        rowBg = MakeTex(new Color(0.14f, 0.15f, 0.17f, 1f));
        expandBg = MakeTex(new Color(0.1f, 0.11f, 0.14f, 0.95f));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft
        };
        nameStyle.normal.textColor = new Color(0.85f, 0.85f, 0.88f);

        unitStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        unitStyle.normal.textColor = new Color(0.55f, 0.55f, 0.58f);

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        valueStyle.normal.textColor = Color.white;

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        hintStyle.normal.textColor = new Color(0.55f, 0.58f, 0.62f);

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = rowBg;
        boxStyle.border = new RectOffset(4, 4, 4, 4);
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
        BlocksGameplayInput = false;
        if (!open)
            return;

        if (EarthquakeConfirmUI.IsOpen || EarthquakeReportUI.IsOpen || NuclearWarReportUI.IsOpen
            || MoonImpactReportUI.IsOpen)
        {
            Close();
            return;
        }

        EnsureStyles();
        MobileUi.Begin();
        Draw();
        MobileUi.End();

        BlocksGameplayInput = IsMouseInRect(new Rect(
            MobileUi.Width - PanelW - 12f, Top, PanelW, PanelH));
    }

    void Draw()
    {
        float x = MobileUi.Width - PanelW - 12f;
        Rect r = new Rect(x, Top, PanelW, PanelH);
        GUI.DrawTexture(r, panelBg);

        var closeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        closeStyle.normal.textColor = Color.white;
        Rect close = new Rect(r.xMax - 36f, r.y + 6f, 30f, 30f);
        if (GUI.Button(close, "×", closeStyle))
            Close();

        GUI.Label(new Rect(r.x, r.y + 8f, r.width, 32f), "Settings", titleStyle);

        Rect content = new Rect(r.x + 12f, r.y + 48f, r.width - 24f, r.height - 60f);
        GUILayout.BeginArea(content);
        scroll = GUILayout.BeginScrollView(scroll, false, false);
        DrawDisasterContent();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawDisasterContent()
    {
        GUILayout.Label("Weapons: use the right icon rail.\n(Impact / Missile / War / Fleet / Laser / Meme)", hintStyle);
        GUILayout.Space(8);
        if (GUILayout.Button("Open Meme Weapons", GUILayout.Height(36)))
        {
            var rail = FindObjectOfType<WeaponRailPanel>();
            if (rail != null)
                rail.OpenCategoryById("meme");
            Close();
        }
        if (GUILayout.Button("Open Weapon Rail", GUILayout.Height(36)))
        {
            var rail = FindObjectOfType<WeaponRailPanel>();
            if (rail != null)
                rail.OpenCategoryById("impact");
            Close();
        }
        GUILayout.Space(12);

        DrawDisasterItem(0, "Planetary Collision", "200", "sci", false,
            "Cost 200 science. Crash another planet into Earth.",
            () => ExecuteStub("Planetary Collision"));

        DrawDisasterItem(1, "Asteroid Diameter", asteroidEdit, "km", true,
            "Set asteroid size, then Execute to drop it.",
            () =>
            {
                if (float.TryParse(asteroidEdit, out float v))
                    asteroidDiameter = Mathf.Clamp(v, 0.1f, 500f);
                asteroidEdit = asteroidDiameter.ToString("0.#");
                ExecuteStub($"Asteroid {asteroidDiameter:0.#} km");
            },
            () => DrawFloatEditor(ref asteroidEdit, ref asteroidDiameter, 0.1f, 500f, 0.5f));

        DrawDisasterItem(2, "ICBM", icbmEdit, "Mt", true,
            "Nuclear missile yield (megatons).",
            () =>
            {
                if (float.TryParse(icbmEdit, out float v))
                    icbmMt = Mathf.Clamp(v, 0.1f, 1000f);
                icbmEdit = icbmMt.ToString("0.#");
                ExecuteStub($"ICBM {icbmMt:0.#} Mt");
            },
            () => DrawFloatEditor(ref icbmEdit, ref icbmMt, 0.1f, 1000f, 1f));

        DrawDisasterItem(3, "Nuclear War", nuclearUnitsEdit, "unit", true,
            "Global nuclear exchange. Units scale casualties & blasts.",
            ExecuteNuclearWar,
            () => DrawIntEditor(ref nuclearUnitsEdit, ref nuclearUnits, 1, 500, 10));

        DrawDisasterItem(4, "Earthquake", earthquakeEdit, "M", true,
            "Richter magnitude. Shake, cracks, aftershocks, casualties.",
            ExecuteEarthquake,
            () => DrawFloatEditor(ref earthquakeEdit, ref earthquakeM, 0.1f, 12f, 0.5f));

        string moonVal = "—";
        var moonSys = MoonImpactSystem.Instance;
        if (moonSys != null)
            moonVal = moonSys.LastMode == MoonImpactMode.Crash ? "Crash" : "Orbit";

        DrawDisasterItem(5, "Moon Impact", moonVal, "", false,
            "Orbit: Moon flies past (near-miss, tidal quakes).\nCrash: Moon hits Earth hard.",
            null,
            DrawMoonImpactButtons);

        DrawDisasterItem(6, "Water Planet", waterEdit, "L", true,
            "Flood volume applied to the planet.",
            () => ExecuteStub($"Water Planet {waterEdit} L"),
            () =>
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Volume", nameStyle, GUILayout.Width(70));
                waterEdit = GUILayout.TextField(waterEdit, valueStyle, GUILayout.Height(28));
                GUILayout.Label("L", unitStyle, GUILayout.Width(20));
                GUILayout.EndHorizontal();
            });

        if (!string.IsNullOrEmpty(statusMsg))
        {
            GUILayout.Space(8);
            GUILayout.Label(statusMsg, hintStyle);
        }
    }

    void DrawDisasterItem(
        int id,
        string name,
        string value,
        string unit,
        bool editable,
        string description,
        System.Action onExecute,
        System.Action drawEditor = null)
    {
        bool itemOpen = expandedDisaster == id;
        float h = 40f;
        Rect row = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));

        if (itemOpen && expandBg != null)
            GUI.DrawTexture(row, expandBg);

        GUI.Label(new Rect(row.x + 4f, row.y, 138f, h), name, nameStyle);

        float boxW = 64f;
        Rect box = new Rect(row.x + 148f, row.y + 6f, boxW, h - 12f);
        GUI.Box(box, GUIContent.none, boxStyle);
        GUI.Label(box, value, valueStyle);
        if (!string.IsNullOrEmpty(unit))
            GUI.Label(new Rect(box.xMax + 4f, row.y, 40f, h), unit, unitStyle);

        var chev = new GUIStyle(unitStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        chev.normal.textColor = Color.white;
        GUI.Label(new Rect(row.xMax - 22f, row.y, 20f, h), itemOpen ? "v" : ">", chev);

        if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            expandedDisaster = itemOpen ? -1 : id;

        GUILayout.Space(2);
        if (!itemOpen)
            return;

        var descStyle = new GUIStyle(hintStyle)
        {
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            fontSize = 11
        };

        GUILayout.BeginVertical(boxStyle);
        GUILayout.Space(4);
        GUILayout.Label(description, descStyle);
        GUILayout.Space(4);

        drawEditor?.Invoke();

        if (onExecute != null)
        {
            GUILayout.Space(8);
            bool busy = IsDisasterBusy(id);
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = busy
                ? new Color(0.45f, 0.25f, 0.18f)
                : new Color(0.92f, 0.28f, 0.16f);
            GUI.enabled = !busy;

            string execLabel = busy ? "Running..." : "Execute";
            if (GUILayout.Button(execLabel, GUILayout.Height(36)))
                onExecute.Invoke();

            GUI.enabled = true;
            GUI.backgroundColor = prev;
        }

        GUILayout.Space(4);
        GUILayout.EndVertical();
        GUILayout.Space(8);
    }

    void DrawMoonImpactButtons()
    {
        bool busy = IsDisasterBusy(5);
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = busy
            ? new Color(0.45f, 0.25f, 0.18f)
            : new Color(0.92f, 0.28f, 0.16f);
        GUI.enabled = !busy;

        GUILayout.Space(4);
        if (GUILayout.Button(busy ? "Running..." : "Orbit — Flyby (no hit)", GUILayout.Height(36)))
            ExecuteMoonImpact(MoonImpactMode.Orbit);

        GUILayout.Space(6);
        if (GUILayout.Button(busy ? "Running..." : "Crash — Direct Impact", GUILayout.Height(36)))
            ExecuteMoonImpact(MoonImpactMode.Crash);

        GUI.enabled = true;
        GUI.backgroundColor = prev;
    }

    void DrawFloatEditor(ref string edit, ref float value, float min, float max, float step)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-", GUILayout.Width(32), GUILayout.Height(28)))
        {
            value = Mathf.Max(min, value - step);
            edit = value.ToString("0.##");
        }
        edit = GUILayout.TextField(edit, valueStyle, GUILayout.Height(28));
        if (float.TryParse(edit, out float parsed))
            value = Mathf.Clamp(parsed, min, max);
        if (GUILayout.Button("+", GUILayout.Width(32), GUILayout.Height(28)))
        {
            value = Mathf.Min(max, value + step);
            edit = value.ToString("0.##");
        }
        GUILayout.EndHorizontal();
    }

    void DrawIntEditor(ref string edit, ref int value, int min, int max, int step)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-", GUILayout.Width(32), GUILayout.Height(28)))
        {
            value = Mathf.Max(min, value - step);
            edit = value.ToString();
        }
        edit = GUILayout.TextField(edit, valueStyle, GUILayout.Height(28));
        if (int.TryParse(edit, out int parsed))
            value = Mathf.Clamp(parsed, min, max);
        if (GUILayout.Button("+", GUILayout.Width(32), GUILayout.Height(28)))
        {
            value = Mathf.Min(max, value + step);
            edit = value.ToString();
        }
        GUILayout.Label("unit", unitStyle, GUILayout.Width(36));
        GUILayout.EndHorizontal();
    }

    static bool IsDisasterBusy(int id)
    {
        if (id == 3)
        {
            var war = NuclearWarSystem.Instance;
            return war != null && war.IsRunning;
        }
        if (id == 4)
        {
            var quake = EarthquakeSystem.Instance;
            return quake != null && quake.IsRunning;
        }
        if (id == 5)
        {
            var moon = MoonImpactSystem.Instance;
            return moon != null && moon.IsRunning;
        }
        return false;
    }

    void ExecuteNuclearWar()
    {
        ParseNuclearUnits();
        var war = NuclearWarSystem.Instance;
        if (war == null)
        {
            statusMsg = "NuclearWarSystem missing";
            return;
        }
        if (war.TryStart(nuclearUnits))
        {
            statusMsg = $"Executing Nuclear War ({nuclearUnits} unit)...";
            Close();
        }
        else
        {
            statusMsg = "Nuclear War already running";
        }
    }

    void ExecuteEarthquake()
    {
        if (float.TryParse(earthquakeEdit, out float v))
            earthquakeM = Mathf.Clamp(v, 0.1f, 12f);
        earthquakeEdit = earthquakeM.ToString("0.0");

        var quake = EarthquakeSystem.Instance;
        if (quake == null)
        {
            statusMsg = "EarthquakeSystem missing";
            return;
        }
        if (quake.IsRunning)
        {
            statusMsg = "Earthquake already running";
            return;
        }

        EarthquakeConfirmUI.Ensure().Open(earthquakeM);
        statusMsg = "Confirm random epicenter to proceed.";
        Close();
    }

    void ExecuteMoonImpact(MoonImpactMode mode)
    {
        var moon = MoonImpactSystem.Instance;
        if (moon == null)
        {
            statusMsg = "MoonImpactSystem missing";
            return;
        }
        if (!moon.TryStart(mode))
        {
            statusMsg = "Moon event already running";
            return;
        }

        statusMsg = mode == MoonImpactMode.Crash
            ? "Moon Crash in progress..."
            : "Moon Orbit flyby in progress...";
        Close();
    }

    void ExecuteStub(string name)
    {
        statusMsg = $"{name} — Execute queued (effect coming soon)";
    }

    void ParseNuclearUnits()
    {
        if (int.TryParse(nuclearUnitsEdit, out int v))
            nuclearUnits = Mathf.Clamp(v, 1, 500);
        nuclearUnitsEdit = nuclearUnits.ToString();
    }

    static bool IsMouseInRect(Rect r)
    {
        var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var t = touchscreen.touches[i];
                if (t.press.isPressed || t.press.wasPressedThisFrame)
                    return r.Contains(MobileUi.ScreenToGui(t.position.ReadValue()));
            }
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null)
            return false;
        return r.Contains(MobileUi.ScreenToGui(mouse.position.ReadValue()));
    }
}
