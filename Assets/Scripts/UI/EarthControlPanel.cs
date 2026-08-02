using UnityEngine;

public class EarthControlPanel : MonoBehaviour
{
    public enum Tab { Overview, Climate, Disaster, Tool }

    [SerializeField] EarthBodyData body;
    [SerializeField] EarthLayerController layers;
    [SerializeField] bool expanded = true;
    [SerializeField] Tab tab = Tab.Overview;

    public static bool BlocksGameplayInput { get; private set; }

    const float PanelW = 300f;
    const float ArrowW = 26f;
    const float Top = 56f;
    const float PanelH = 520f;

    static readonly float[] RotationPresets = { 0.1f, 0.5f, 1f, 2f, 10f };
    static readonly string[] RotationLabels = { "x0.1", "x0.5", "x1", "x2", "x10" };
    static readonly string[] TabNames = { "Overview", "Climate", "Disaster", "Tool" };
    static readonly string[] TabIcons = { "O", "C", "D", "T" };

    GUIStyle _title;
    GUIStyle _tabLabel;
    GUIStyle _tabLabelOn;
    GUIStyle _nameStyle;
    GUIStyle _unitStyle;
    GUIStyle _valueStyle;
    GUIStyle _hint;
    GUIStyle _boxStyle;

    Texture2D panelBg;
    Texture2D rowBg;
    Texture2D tabLine;
    Texture2D toggleOn;
    Texture2D toggleOff;
    Texture2D knob;

    Vector2 scroll;
    int tosFlash;
    int nuclearUnits = 100;
    string nuclearUnitsEdit = "100";
    string statusMsg;

    // Disaster accordion
    int expandedDisaster = -1;
    float asteroidDiameter = 1f;
    string asteroidEdit = "1.0";
    float icbmMt = 1f;
    string icbmEdit = "1";
    float earthquakeM = 1f;
    string earthquakeEdit = "1.0";
    string waterEdit = "1e20";
    Texture2D expandBg;
    Texture2D execBg;

    public void Bind(EarthBodyData data, EarthLayerController layerController)
    {
        body = data;
        layers = layerController;
    }

    public void OpenTab(Tab t)
    {
        tab = t;
        expanded = true;
    }

    void EnsureStyles()
    {
        if (_title != null)
            return;

        panelBg = MakeTex(new Color(0.06f, 0.07f, 0.09f, 0.92f));
        rowBg = MakeTex(new Color(0.14f, 0.15f, 0.17f, 1f));
        tabLine = MakeTex(Color.white);
        toggleOn = MakeTex(new Color(0.35f, 0.72f, 1f, 1f));
        toggleOff = MakeTex(new Color(0.28f, 0.3f, 0.34f, 1f));
        knob = MakeTex(Color.white);
        expandBg = MakeTex(new Color(0.1f, 0.11f, 0.14f, 0.95f));
        execBg = MakeTex(new Color(0.85f, 0.28f, 0.18f, 1f));

        GameSettings.Load();

        _title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _title.normal.textColor = Color.white;

        _tabLabel = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
        _tabLabel.normal.textColor = new Color(0.55f, 0.55f, 0.58f);

        _tabLabelOn = new GUIStyle(_tabLabel);
        _tabLabelOn.normal.textColor = Color.white;
        _tabLabelOn.fontStyle = FontStyle.Bold;

        _nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft
        };
        _nameStyle.normal.textColor = new Color(0.85f, 0.85f, 0.88f);

        _unitStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        _unitStyle.normal.textColor = new Color(0.55f, 0.55f, 0.58f);

        _valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _valueStyle.normal.textColor = Color.white;

        _hint = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        _hint.normal.textColor = new Color(0.55f, 0.58f, 0.62f);

        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = rowBg;
        _boxStyle.border = new RectOffset(4, 4, 4, 4);
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

        // 모달 중엔 사이드 패널만 숨김 — 카메라 잠그지 않음
        if (EarthquakeConfirmUI.IsOpen || EarthquakeReportUI.IsOpen || NuclearWarReportUI.IsOpen
            || MoonImpactReportUI.IsOpen)
        {
            BlocksGameplayInput = false;
            EarthLayerToolbar.BlocksGameplayInput = false;
            return;
        }

        float x = 10f;
        Rect panelRect = new Rect(x, Top, PanelW, PanelH);
        Rect arrowRect;

        if (expanded)
        {
            DrawPanel(panelRect);
            arrowRect = new Rect(panelRect.xMax - 2f, Top + PanelH * 0.45f, ArrowW, 64f);
            if (GUI.Button(arrowRect, "<"))
                expanded = false;
        }
        else
        {
            arrowRect = new Rect(x, Top + PanelH * 0.45f, ArrowW, 64f);
            if (GUI.Button(arrowRect, ">"))
                expanded = true;
            panelRect = arrowRect;
        }

        BlocksGameplayInput = IsMouseInRect(panelRect) || IsMouseInRect(arrowRect);
        EarthLayerToolbar.BlocksGameplayInput = BlocksGameplayInput;
    }

    void DrawPanel(Rect r)
    {
        GUI.DrawTexture(r, panelBg);
        GUI.Label(new Rect(r.x, r.y + 8, r.width, 36), "Earth", _title);

        float tabY = r.y + 48;
        float tabW = (r.width - 20f) / 4f;
        for (int i = 0; i < 4; i++)
        {
            Tab t = (Tab)i;
            Rect tr = new Rect(r.x + 10 + tabW * i, tabY, tabW, 48);
            bool on = tab == t;

            if (GUI.Button(tr, GUIContent.none, GUIStyle.none))
                tab = t;

            var iconStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = on ? 18 : 14,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            iconStyle.normal.textColor = on ? Color.white : new Color(0.45f, 0.45f, 0.48f);
            GUI.Label(new Rect(tr.x, tr.y, tr.width, 26), TabIcons[i], iconStyle);
            GUI.Label(new Rect(tr.x, tr.y + 24, tr.width, 18), TabNames[i], on ? _tabLabelOn : _tabLabel);

            if (on)
                GUI.DrawTexture(new Rect(tr.x + 10, tr.yMax - 3, tr.width - 20, 2), tabLine);
        }

        Rect content = new Rect(r.x + 12, tabY + 56, r.width - 24, r.height - (tabY + 56 - r.y) - 12);
        GUILayout.BeginArea(content);
        scroll = GUILayout.BeginScrollView(scroll, false, false);

        switch (tab)
        {
            case Tab.Overview:
                DrawOverview();
                break;
            case Tab.Climate:
                DrawClimate();
                break;
            case Tab.Disaster:
                DrawDisaster();
                break;
            case Tab.Tool:
                DrawTool();
                break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawOverview()
    {
        if (body == null)
            body = Object.FindObjectOfType<EarthBodyData>();

        float mul = body != null ? body.RotationMultiplier : 1f;

        StatRow("*", "Mass", FormatMass(EarthBodyData.MassKg), "Kg");
        StatRow("o", "Core Temp", (EarthBodyData.CoreTempKelvin - 273.15).ToString("0"), "C");
        StatRow("R", "Mean Radius", EarthBodyData.MeanRadiusKm.ToString("#,0"), "km");
        StatRow("~", "Rotation Speed", FormatRotationHours(EarthBodyData.SiderealDayHours / mul), "");

        GUILayout.Space(6);
        GUILayout.Label("Rotation multiplier", _unitStyle);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < RotationPresets.Length; i++)
        {
            bool on = Mathf.Approximately(mul, RotationPresets[i]);
            Color prev = GUI.backgroundColor;
            if (on)
                GUI.backgroundColor = new Color(0.35f, 0.55f, 0.95f);
            if (GUILayout.Button(RotationLabels[i], GUILayout.Height(28)))
            {
                if (body != null)
                    body.RotationMultiplier = RotationPresets[i];
            }
            GUI.backgroundColor = prev;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        StatRow("d", "Density", (EarthBodyData.MeanDensityKgM3 / 1000.0).ToString("0.000"), "g/cm3");
        StatRow("g", "Surface Gravity", EarthBodyData.SurfaceGravityMs2.ToString("0.000"), "m/s2");
        StatRow("e", "Escape velocity", EarthBodyData.EscapeVelocityKmS.ToString("0.000"), "km/s");
        StatRow("p", "Orbital Period", EarthBodyData.OrbitalPeriodDays.ToString("0.00"), "days");
    }

    void DrawClimate()
    {
        StatRow("T", "Surface Temp", EarthBodyData.SurfaceTempC.ToString("0.0"), "C");
        StatRow("P", "Atmospheric Pressure", ((int)EarthBodyData.AtmosphericPressureMbar).ToString("#,0"), "mbar");
        StatRow("C", "Carbon Dioxide", EarthBodyData.CarbonDioxidePpm.ToString("0"), "ppm");
    }

    void DrawDisaster()
    {
        DrawDisasterItem(0, "$", "Planetary Collision", "200", "sci", false,
            "Cost 200 science. Crash another planet into Earth.",
            () => ExecuteStub("Planetary Collision"));

        DrawDisasterItem(1, "A", "Asteroid Diameter", asteroidEdit, "km", true,
            "Set asteroid size, then Execute to drop it.",
            () =>
            {
                if (float.TryParse(asteroidEdit, out float v))
                    asteroidDiameter = Mathf.Clamp(v, 0.1f, 500f);
                asteroidEdit = asteroidDiameter.ToString("0.#");
                ExecuteStub($"Asteroid {asteroidDiameter:0.#} km");
            },
            () => DrawFloatEditor(ref asteroidEdit, ref asteroidDiameter, 0.1f, 500f, 0.5f));

        DrawDisasterItem(2, "I", "ICBM", icbmEdit, "Mt", true,
            "Nuclear missile yield (megatons).",
            () =>
            {
                if (float.TryParse(icbmEdit, out float v))
                    icbmMt = Mathf.Clamp(v, 0.1f, 1000f);
                icbmEdit = icbmMt.ToString("0.#");
                ExecuteStub($"ICBM {icbmMt:0.#} Mt");
            },
            () => DrawFloatEditor(ref icbmEdit, ref icbmMt, 0.1f, 1000f, 1f));

        DrawDisasterItem(3, "N", "Nuclear War", nuclearUnitsEdit, "unit", true,
            "Global nuclear exchange. Units scale casualties & blasts.",
            ExecuteNuclearWar,
            () => DrawIntEditor(ref nuclearUnitsEdit, ref nuclearUnits, 1, 500, 10));

        DrawDisasterItem(4, "E", "Earthquake", earthquakeEdit, "M", true,
            "Richter magnitude. Shake, cracks, aftershocks, casualties.",
            ExecuteEarthquake,
            () => DrawFloatEditor(ref earthquakeEdit, ref earthquakeM, 0.1f, 12f, 0.5f));

        string moonVal = "—";
        var moonSys = MoonImpactSystem.Instance;
        if (moonSys != null)
            moonVal = moonSys.LastMode == MoonImpactMode.Crash ? "Crash" : "Orbit";

        DrawDisasterItem(5, "M", "Moon Impact", moonVal, "", false,
            "Orbit: Moon flies past (near-miss, tidal quakes).\nCrash: Moon hits Earth hard.",
            null,
            DrawMoonImpactButtons);

        DrawDisasterItem(6, "W", "Water Planet", waterEdit, "L", true,
            "Flood volume applied to the planet.",
            () => ExecuteStub($"Water Planet {waterEdit} L"),
            () =>
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Volume", _nameStyle, GUILayout.Width(70));
                waterEdit = GUILayout.TextField(waterEdit, _valueStyle, GUILayout.Height(28));
                GUILayout.Label("L", _unitStyle, GUILayout.Width(20));
                GUILayout.EndHorizontal();
            });

        if (!string.IsNullOrEmpty(statusMsg))
        {
            GUILayout.Space(8);
            GUILayout.Label(statusMsg, _hint);
        }
    }

    void DrawDisasterItem(
        int id,
        string icon,
        string name,
        string value,
        string unit,
        bool editable,
        string description,
        System.Action onExecute,
        System.Action drawEditor = null)
    {
        bool open = expandedDisaster == id;
        float h = 40f;
        Rect row = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));

        if (open && expandBg != null)
            GUI.DrawTexture(row, expandBg);

        var iconStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        iconStyle.normal.textColor = new Color(0.75f, 0.78f, 0.85f);
        GUI.Label(new Rect(row.x, row.y, 28, h), icon, iconStyle);
        GUI.Label(new Rect(row.x + 30, row.y, 118, h), name, _nameStyle);

        float boxW = 64f;
        Rect box = new Rect(row.x + 148, row.y + 6, boxW, h - 12);
        GUI.Box(box, GUIContent.none, _boxStyle);
        GUI.Label(box, value, _valueStyle);
        if (!string.IsNullOrEmpty(unit))
            GUI.Label(new Rect(box.xMax + 4, row.y, 40, h), unit, _unitStyle);

        var chev = new GUIStyle(_unitStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        chev.normal.textColor = Color.white;
        GUI.Label(new Rect(row.xMax - 22, row.y, 20, h), open ? "v" : ">", chev);

        if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            expandedDisaster = open ? -1 : id;

        GUILayout.Space(2);
        if (!open)
            return;

        // ScrollView 안에서는 BeginArea 쓰지 말 것 — 버튼이 잘리거나 안 보임
        var descStyle = new GUIStyle(_hint)
        {
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            fontSize = 11
        };

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Space(4);
        GUILayout.Label(description, descStyle);
        GUILayout.Space(4);

        if (drawEditor != null)
            drawEditor();

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
        expanded = false;
        expandedDisaster = -1;
    }

    void DrawFloatEditor(ref string edit, ref float value, float min, float max, float step)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-", GUILayout.Width(32), GUILayout.Height(28)))
        {
            value = Mathf.Max(min, value - step);
            edit = value.ToString("0.##");
        }
        edit = GUILayout.TextField(edit, _valueStyle, GUILayout.Height(28));
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
        edit = GUILayout.TextField(edit, _valueStyle, GUILayout.Height(28));
        if (int.TryParse(edit, out int parsed))
            value = Mathf.Clamp(parsed, min, max);
        if (GUILayout.Button("+", GUILayout.Width(32), GUILayout.Height(28)))
        {
            value = Mathf.Min(max, value + step);
            edit = value.ToString();
        }
        GUILayout.Label("unit", _unitStyle, GUILayout.Width(36));
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
            expanded = false;
            expandedDisaster = -1;
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
        expanded = false;
        expandedDisaster = -1;
    }

    void ExecuteStub(string name)
    {
        statusMsg = $"{name} — Execute queued (effect coming soon)";
    }

    void EditableUnitRow(string icon, string name, ref string edit, ref int value, string unit)
    {
        // kept for compatibility; disaster UI uses accordion now
        DrawIntEditor(ref edit, ref value, 1, 500, 10);
    }

    void ParseNuclearUnits()
    {
        if (int.TryParse(nuclearUnitsEdit, out int v))
            nuclearUnits = Mathf.Clamp(v, 1, 500);
        nuclearUnitsEdit = nuclearUnits.ToString();
    }

    void StatRow(string icon, string name, string value, string unit)
    {
        float h = 40f;
        Rect row = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));

        var iconStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        iconStyle.normal.textColor = new Color(0.75f, 0.78f, 0.85f);
        GUI.Label(new Rect(row.x, row.y, 28, h), icon, iconStyle);

        float nameW = 132f;
        GUI.Label(new Rect(row.x + 30, row.y, nameW, h), name, _nameStyle);

        float unitW = string.IsNullOrEmpty(unit) ? 0f : 42f;
        float boxX = row.x + 30 + nameW + 4;
        float boxW = Mathf.Max(72f, row.width - (boxX - row.x) - unitW - 4);
        Rect box = new Rect(boxX, row.y + 6, boxW, h - 12);
        GUI.Box(box, GUIContent.none, _boxStyle);
        GUI.Label(box, value, _valueStyle);

        if (!string.IsNullOrEmpty(unit))
            GUI.Label(new Rect(box.xMax + 4, row.y, unitW, h), unit, _unitStyle);

        GUILayout.Space(4);
    }

    void DrawTool()
    {
        if (body == null)
            body = Object.FindObjectOfType<EarthBodyData>();
        if (layers == null)
            layers = Object.FindObjectOfType<EarthLayerController>();

        bool rot = body == null || body.RotationEnabled;
        bool newRot = ToggleRow("R", "Earth Rotation", rot);
        if (body != null && newRot != rot)
            body.RotationEnabled = newRot;

        if (layers != null)
        {
            bool clouds = layers.cloudsEnabled;
            bool newClouds = ToggleRow("C", "Cloud Layer", clouds);
            if (newClouds != clouds)
                layers.cloudsEnabled = newClouds;

            bool aurora = layers.auroraEnabled;
            bool newAurora = ToggleRow("A", "Aurora Layer", aurora);
            if (newAurora != aurora)
                layers.auroraEnabled = newAurora;

            bool atmo = layers.atmosphereEnabled;
            bool newAtmo = ToggleRow("O", "Atmosphere Layer", atmo);
            if (newAtmo != atmo)
                layers.atmosphereEnabled = newAtmo;

            layers.ApplyAll();
        }

        GUILayout.Space(8);

        float music = GameSettings.MusicVolume;
        float newMusic = SliderRow("M", "Music Volume", music);
        if (Mathf.Abs(newMusic - music) > 0.001f)
            GameSettings.MusicVolume = newMusic;

        float fx = GameSettings.EffectVolume;
        float newFx = SliderRow("E", "Effect Volume", fx);
        if (Mathf.Abs(newFx - fx) > 0.001f)
            GameSettings.EffectVolume = newFx;

        GUILayout.Space(6);

        if (ActionRow("Q", "Graphic Quality", GameSettings.QualityLabel))
            GameSettings.CycleQuality();

        if (ActionRow("i", "Terms of Service", ">"))
        {
            tosFlash = 90;
            Application.OpenURL("https://unity.com/legal/terms-of-service");
        }

        if (tosFlash > 0)
        {
            tosFlash--;
            GUILayout.Label("Opened Terms of Service", _hint);
        }
    }

    bool ToggleRow(string icon, string label, bool value)
    {
        float h = 40f;
        Rect row = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));
        DrawRowIcon(row, icon);
        GUI.Label(new Rect(row.x + 34, row.y, 150, h), label, _nameStyle);

        float sw = 48f;
        float sh = 26f;
        Rect toggle = new Rect(row.xMax - sw - 4, row.y + (h - sh) * 0.5f, sw, sh);
        GUI.DrawTexture(toggle, value ? toggleOn : toggleOff, ScaleMode.StretchToFill);

        float k = 22f;
        float kx = value ? toggle.xMax - k - 2f : toggle.x + 2f;
        GUI.DrawTexture(new Rect(kx, toggle.y + 2f, k, k), knob, ScaleMode.StretchToFill);

        if (GUI.Button(toggle, GUIContent.none, GUIStyle.none))
            value = !value;

        GUILayout.Space(2);
        return value;
    }

    float SliderRow(string icon, string label, float value)
    {
        float h = 44f;
        Rect row = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));
        DrawRowIcon(row, icon);
        GUI.Label(new Rect(row.x + 34, row.y, 120, 22), label, _nameStyle);
        Rect slider = new Rect(row.x + 34, row.y + 22, row.width - 42, 16);
        return GUI.HorizontalSlider(slider, value, 0f, 1f);
    }

    bool ActionRow(string icon, string label, string trailing)
    {
        float h = 40f;
        Rect row = GUILayoutUtility.GetRect(1, h, GUILayout.ExpandWidth(true));
        DrawRowIcon(row, icon);
        GUI.Label(new Rect(row.x + 34, row.y, 150, h), label, _nameStyle);

        Rect btn = new Rect(row.xMax - 96, row.y + 6, 90, h - 12);
        GUI.Box(btn, GUIContent.none, _boxStyle);
        GUI.Label(btn, trailing, _valueStyle);

        GUILayout.Space(2);
        return GUI.Button(row, GUIContent.none, GUIStyle.none);
    }

    void DrawRowIcon(Rect row, string icon)
    {
        var iconStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        iconStyle.normal.textColor = new Color(0.8f, 0.82f, 0.88f);
        GUI.Label(new Rect(row.x, row.y, 30, row.height), icon, iconStyle);
    }

    void DrawPlaceholder(string msg)
    {
        GUILayout.Space(40);
        GUILayout.Label(msg, _hint);
        GUILayout.Space(8);
        GUILayout.Label("More options coming soon.", _hint);
    }

    static string FormatMass(double kg)
    {
        double exp = System.Math.Floor(System.Math.Log10(kg));
        double mant = kg / System.Math.Pow(10, exp);
        return string.Format("{0:0.000}x10^{1}", mant, (int)exp);
    }

    static string FormatRotationHours(double hours)
    {
        if (double.IsInfinity(hours) || hours > 1e6)
            return "-";
        int h = (int)System.Math.Floor(hours);
        int m = (int)System.Math.Round((hours - h) * 60.0);
        if (m == 60)
        {
            h++;
            m = 0;
        }
        return string.Format("{0} h {1:00} m", h, m);
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


