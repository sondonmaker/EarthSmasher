using UnityEngine;

/// <summary>
/// 지진 실행 전 확인: 랜덤 진앙을 보여 주고 Confirm 시 진행.
/// </summary>
public class EarthquakeConfirmUI : MonoBehaviour
{
    public static EarthquakeConfirmUI Ensure()
    {
        var ui = Object.FindObjectOfType<EarthquakeConfirmUI>();
        if (ui != null)
            return ui;
        return new GameObject("EarthquakeConfirmUI").AddComponent<EarthquakeConfirmUI>();
    }

    public static bool IsOpen
    {
        get
        {
            var ui = Object.FindObjectOfType<EarthquakeConfirmUI>();
            return ui != null && ui.visible;
        }
    }

    bool visible;
    float magnitude = 7f;
    float lat;
    float lon;
    string regionHint = "";
    string status;

    Texture2D bg;
    Texture2D rowBg;
    Texture2D accentBg;
    GUIStyle titleStyle;
    GUIStyle labelStyle;
    GUIStyle valueStyle;
    GUIStyle hintStyle;
    GUIStyle boxStyle;
    GUIStyle buttonStyle;
    GUIStyle confirmStyle;

    Rect panelRect;

    public void Open(float mag)
    {
        magnitude = Mathf.Clamp(mag, 0.1f, 12f);
        status = null;
        RollLocation();
        visible = true;
    }

    public void Hide() => visible = false;

    void RollLocation()
    {
        var q = EarthquakeSystem.Instance;
        if (q != null && q.TrySuggestLocation(out lat, out lon, out regionHint))
            return;

        lat = Random.Range(-60f, 70f);
        lon = Random.Range(-180f, 180f);
        regionHint = "Unknown";
    }

    void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        bg = MakeTex(new Color(0.05f, 0.06f, 0.08f, 0.96f));
        rowBg = MakeTex(new Color(0.14f, 0.15f, 0.17f, 1f));
        accentBg = MakeTex(new Color(0.92f, 0.35f, 0.18f, 1f));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.75f, 0.35f);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.88f);

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        valueStyle.normal.textColor = Color.white;

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        hintStyle.normal.textColor = new Color(0.65f, 0.68f, 0.72f);

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = rowBg;
        boxStyle.border = new RectOffset(4, 4, 4, 4);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 0f
        };

        confirmStyle = new GUIStyle(buttonStyle);
        confirmStyle.normal.textColor = Color.white;
        confirmStyle.hover.textColor = Color.white;
        confirmStyle.active.textColor = Color.white;
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
        if (!visible)
            return;

        MobileUi.Begin();
        DrawGui();
        MobileUi.End();
    }

    void DrawGui()
    {
        EnsureStyles();

        // 다른 OnGUI보다 위에 그려서 클릭이 먹히게
        GUI.depth = -1000;

        float pad = 20f;
        float rowH = 34f;
        float btnH = 44f;
        float w = Mathf.Min(420f, MobileUi.Width - 32f);
        // 헤더 + 힌트 + 4행 + reroll + gap + cancel/confirm + 여백
        float h = 12f + 32f + 40f + (rowH + 6f) * 4f + 14f + 34f + 16f + btnH + 20f;
        h = Mathf.Min(h, MobileUi.Height - 24f);
        panelRect = new Rect((MobileUi.Width - w) * 0.5f, (MobileUi.Height - h) * 0.5f, w, h);

        Color prevGui = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, MobileUi.Width, MobileUi.Height), Texture2D.whiteTexture);
        GUI.color = prevGui;

        GUI.DrawTexture(panelRect, bg);

        var e = Event.current;
        if (e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)
            && !panelRect.Contains(e.mousePosition))
            e.Use();

        if (GUI.Button(new Rect(panelRect.x + 10, panelRect.y + 10, 40, 30), "X", buttonStyle))
        {
            Hide();
            return;
        }

        GUI.Label(new Rect(panelRect.x, panelRect.y + 12, panelRect.width, 32), "Confirm Earthquake", titleStyle);
        GUI.Label(new Rect(panelRect.x + pad, panelRect.y + 48, panelRect.width - pad * 2f, 36),
            "A random epicenter was selected. Confirm to proceed.", hintStyle);

        float y = panelRect.y + 90f;
        float innerW = panelRect.width - pad * 2f;

        DrawInfoRow(new Rect(panelRect.x + pad, y, innerW, rowH), "Magnitude", $"M {magnitude:0.0}");
        y += rowH + 6f;
        DrawInfoRow(new Rect(panelRect.x + pad, y, innerW, rowH), "Region", regionHint);
        y += rowH + 6f;
        DrawInfoRow(new Rect(panelRect.x + pad, y, innerW, rowH), "Latitude", lat.ToString("0.0"));
        y += rowH + 6f;
        DrawInfoRow(new Rect(panelRect.x + pad, y, innerW, rowH), "Longitude", lon.ToString("0.0"));
        y += rowH + 14f;

        if (GUI.Button(new Rect(panelRect.x + pad, y, innerW, 34f), "Reroll Location", buttonStyle))
        {
            RollLocation();
            status = null;
        }

        if (!string.IsNullOrEmpty(status))
        {
            var err = new GUIStyle(hintStyle) { alignment = TextAnchor.MiddleCenter };
            err.normal.textColor = new Color(1f, 0.45f, 0.35f);
            GUI.Label(new Rect(panelRect.x + pad, panelRect.yMax - btnH - 44f, innerW, 22f), status, err);
        }

        float gap = 10f;
        float btnW = (innerW - gap) * 0.5f;
        float btnY = panelRect.yMax - btnH - 16f;

        Rect cancelR = new Rect(panelRect.x + pad, btnY, btnW, btnH);
        Rect confirmR = new Rect(cancelR.xMax + gap, btnY, btnW, btnH);

        if (GUI.Button(cancelR, "Cancel", buttonStyle))
        {
            Hide();
            return;
        }

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.95f, 0.35f, 0.18f);
        bool confirm = GUI.Button(confirmR, "Confirm", confirmStyle);
        GUI.backgroundColor = prevBg;
        GUI.DrawTexture(new Rect(confirmR.x, confirmR.y, 4f, confirmR.height), accentBg);

        if (confirm)
            TryConfirm();
    }

    void DrawInfoRow(Rect r, string label, string value)
    {
        GUI.Box(r, GUIContent.none, boxStyle);
        GUI.Label(new Rect(r.x + 10, r.y, 120, r.height), label, labelStyle);
        GUI.Label(new Rect(r.x + 120, r.y, r.width - 130, r.height), value, valueStyle);
    }

    void TryConfirm()
    {
        var quake = EarthquakeSystem.Instance;
        if (quake == null)
        {
            status = "EarthquakeSystem missing";
            return;
        }
        if (!quake.TryStart(magnitude, lat, lon))
        {
            status = "Earthquake already running";
            return;
        }
        Hide();
    }
}
