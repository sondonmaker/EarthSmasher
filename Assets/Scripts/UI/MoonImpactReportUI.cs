using UnityEngine;

/// <summary>
/// Moon Impact Report 모달.
/// </summary>
public class MoonImpactReportUI : MonoBehaviour
{
    public static MoonImpactReportUI Ensure()
    {
        var ui = Object.FindObjectOfType<MoonImpactReportUI>();
        if (ui != null)
            return ui;
        return new GameObject("MoonImpactReportUI").AddComponent<MoonImpactReportUI>();
    }

    public static bool IsOpen
    {
        get
        {
            var ui = Object.FindObjectOfType<MoonImpactReportUI>();
            return ui != null && ui.visible;
        }
    }

    MoonImpactReport report;
    bool visible;
    Texture2D bg;
    Texture2D line;

    GUIStyle titleStyle;
    GUIStyle labelStyle;
    GUIStyle valueStyle;
    GUIStyle bodyStyle;

    public void Show(MoonImpactReport r)
    {
        report = r;
        visible = true;
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
        titleStyle.normal.textColor = new Color(1f, 0.75f, 0.35f);

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
        valueStyle.normal.textColor = new Color(1f, 0.7f, 0.35f);

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.92f);
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

        EnsureStyles();
        GUI.depth = -1000;

        float w = Mathf.Min(400f, Screen.width - 40f);
        float h = Mathf.Min(360f, Screen.height - 40f);
        Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.DrawTexture(panel, bg);

        if (GUI.Button(new Rect(panel.x + 10, panel.y + 10, 36, 28), "X"))
            Hide();

        GUI.Label(new Rect(panel.x, panel.y + 8, panel.width, 32), "Moon Impact Report", titleStyle);

        Rect content = new Rect(panel.x + 18, panel.y + 48, panel.width - 36, panel.height - 70);
        GUILayout.BeginArea(content);

        Stat("Mode", report.modeLabel);
        Stat("Region", report.regionHint);
        Stat("Focus", $"{report.lat:0.0}, {report.lon:0.0}");
        Stat("Death", report.deaths.ToString("#,0"));
        Stat("Injury", report.injuries.ToString("#,0"));

        GUILayout.Space(8);
        GUI.DrawTexture(GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)), line);
        GUILayout.Space(8);

        for (int i = 0; i < report.notes.Count; i++)
            GUILayout.Label("• " + report.notes[i], bodyStyle);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", GUILayout.Height(34)))
            Hide();

        GUILayout.EndArea();
    }

    void Stat(string name, string value)
    {
        Rect r = GUILayoutUtility.GetRect(1, 24, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y, r.width * 0.5f, r.height), name, labelStyle);
        GUI.Label(new Rect(r.x + r.width * 0.4f, r.y, r.width * 0.6f, r.height), value, valueStyle);
    }
}
