using UnityEngine;

/// <summary>
/// Nuclear War Report 모달 (참고 UI 스타일 OnGUI).
/// </summary>
public class NuclearWarReportUI : MonoBehaviour
{
    public static NuclearWarReportUI Ensure()
    {
        var ui = FindObjectOfType<NuclearWarReportUI>();
        if (ui != null)
            return ui;
        var go = new GameObject("NuclearWarReportUI");
        return go.AddComponent<NuclearWarReportUI>();
    }

    public static bool IsOpen
    {
        get
        {
            var ui = FindObjectOfType<NuclearWarReportUI>();
            return ui != null && ui.visible;
        }
    }

    NuclearWarReport report;
    bool visible;
    Vector2 scroll;
    Texture2D bg;
    Texture2D line;

    GUIStyle titleStyle;
    GUIStyle labelStyle;
    GUIStyle valueStyle;
    GUIStyle sectionStyle;
    GUIStyle bodyStyle;

    public bool IsVisible => visible;

    public void Show(NuclearWarReport r)
    {
        report = r;
        visible = true;
        scroll = Vector2.zero;
    }

    public void Hide() => visible = false;

    void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        bg = MakeTex(new Color(0.05f, 0.06f, 0.08f, 0.94f));
        line = MakeTex(new Color(0.35f, 0.35f, 0.38f, 1f));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.55f, 0.2f);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.88f);

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        valueStyle.normal.textColor = new Color(0.45f, 0.75f, 1f);

        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        sectionStyle.normal.textColor = Color.white;

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = new Color(0.92f, 0.92f, 0.94f);
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
        if (!visible || report == null)
            return;

        MobileUi.Begin();
        DrawGui();
        MobileUi.End();
    }

    void DrawGui()
    {
        EnsureStyles();

        float w = Mathf.Min(420f, MobileUi.Width - 40f);
        float h = Mathf.Min(520f, MobileUi.Height - 40f);
        Rect panel = new Rect((MobileUi.Width - w) * 0.5f, (MobileUi.Height - h) * 0.5f, w, h);
        GUI.DrawTexture(panel, bg);

        if (GUI.Button(new Rect(panel.x + 10, panel.y + 10, 36, 28), "X"))
            Hide();

        GUI.Label(new Rect(panel.x, panel.y + 8, panel.width, 32), "Nuclear War Report", titleStyle);

        Rect content = new Rect(panel.x + 16, panel.y + 48, panel.width - 32, panel.height - 60);
        GUILayout.BeginArea(content);
        scroll = GUILayout.BeginScrollView(scroll);

        StatLine("Main Country", report.mainCountryCount.ToString("#,0"));
        StatLine("Main City", report.mainCityCount.ToString("#,0"));
        StatLine("Total Affected City Num", report.totalAffectedCities > 0 ? report.totalAffectedCities.ToString("#,0") : "-");
        StatLine("Death", report.totalDeaths.ToString("#,0"));
        StatLine("Injury", report.totalInjuries.ToString("#,0"));

        GUILayout.Space(8);
        GUI.DrawTexture(GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)), line);
        GUILayout.Space(8);

        GUILayout.Label("=== Top affected countries ===", sectionStyle);
        GUILayout.Space(4);

        for (int i = 0; i < report.topCountries.Count; i++)
        {
            var c = report.topCountries[i];
            string tag = c.indirect ? " [Indirect]" : "";
            string label = c.indirect && c.name.Length <= 3 ? c.code : c.name;
            string lineText = string.Format(
                "{0}. {1}{2} - Deaths {3}, hits {4}, interceptions {5}",
                i + 1, label, tag, c.deaths.ToString("#,0"), c.hits, c.interceptions);
            GUILayout.Label(lineText, bodyStyle);
            GUILayout.Space(2);
        }

        GUILayout.Space(10);
        GUILayout.Label("=== Non-nuclear countries affected ===", sectionStyle);
        GUILayout.Space(4);

        for (int i = 0; i < report.nonNuclearAffected.Count; i++)
        {
            var c = report.nonNuclearAffected[i];
            string lineText = string.Format(
                "{0}. {1} - Deaths {2}",
                i + 1, c.code, c.deaths.ToString("#,0"));
            GUILayout.Label(lineText, bodyStyle);
            GUILayout.Space(2);
        }

        GUILayout.Space(16);
        if (GUILayout.Button("Close", GUILayout.Height(34)))
            Hide();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void StatLine(string name, string value)
    {
        Rect r = GUILayoutUtility.GetRect(1, 24, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y, r.width * 0.55f, r.height), name, labelStyle);
        GUI.Label(new Rect(r.x + r.width * 0.45f, r.y, r.width * 0.55f, r.height), value, valueStyle);
    }
}
