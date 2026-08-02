using System;
using UnityEngine;

/// <summary>
/// Solar Smash 스타일 2단 무기 레일: 오른쪽 카테고리 → 왼쪽 세부 무기.
/// </summary>
public class WeaponRailPanel : MonoBehaviour
{
    public static WeaponRailPanel Instance { get; private set; }
    public static bool BlocksGameplay { get; private set; }

    const float CatSize = 56f;
    const float CatGap = 8f;
    const float SubW = 92f;
    const float SubH = 78f;
    const float SubGap = 6f;
    const float Pad = 12f;
    const float Top = 64f;

    static readonly Color Border = new Color(0.55f, 0.58f, 0.62f, 0.95f);
    static readonly Color BorderOn = new Color(1f, 0.45f, 0.12f, 1f);
    static readonly Color Face = new Color(0.12f, 0.13f, 0.15f, 0.92f);
    static readonly Color FaceOn = new Color(0.18f, 0.14f, 0.1f, 0.95f);

    int selectedCat = 1; // Impact default
    int selectedSub = -1;
    bool submenuOpen = true;
    string toast;

    Texture2D pxFace;
    Texture2D pxFaceOn;
    Texture2D pxBorder;
    Texture2D pxBorderOn;
    GUIStyle labelStyle;
    GUIStyle toastStyle;

    struct Cat
    {
        public string id;
        public string icon; // simple glyph
        public string tip;
        public Sub[] subs;
    }

    struct Sub
    {
        public string id;
        public string title;
        public string icon;
        public Action fire;
        public Func<bool> busy;
    }

    Cat[] cats;

    void Awake()
    {
        Instance = this;
        BuildCatalog();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        BlocksGameplay = false;
    }

    public void OpenCategory(int index)
    {
        if (index < 0 || index >= cats.Length)
            return;
        selectedCat = index;
        submenuOpen = true;
        selectedSub = -1;
    }

    void BuildCatalog()
    {
        cats = new[]
        {
            new Cat
            {
                id = "space",
                icon = "@",
                tip = "Space",
                subs = new[]
                {
                    SubOf("Soon", "~", "Coming soon", null, null)
                }
            },
            new Cat
            {
                id = "impact",
                icon = ">",
                tip = "Impact",
                subs = new[]
                {
                    SubOf("asteroid", "*", "Asteroid", FireSmallAsteroid, () => false),
                    SubOf("big_meteor", "#", "Big Meteor", FireBigMeteor, () => false),
                    SubOf("moon_orbit", "o", "Moon Orbit", () => FireMoon(MoonImpactMode.Orbit), MoonBusy),
                    SubOf("moon_crash", "O", "Moon Crash", () => FireMoon(MoonImpactMode.Crash), MoonBusy)
                }
            },
            new Cat
            {
                id = "energy",
                icon = "+",
                tip = "Energy",
                subs = new[]
                {
                    SubOf("laser", "/", "Laser Soon", null, null)
                }
            },
            new Cat
            {
                id = "war",
                icon = "N",
                tip = "War",
                subs = new[]
                {
                    SubOf("nuke", "N", "Nuclear War", FireNuclear, NukeBusy)
                }
            },
            new Cat
            {
                id = "quake",
                icon = "E",
                tip = "Quake",
                subs = new[]
                {
                    SubOf("quake", "E", "Earthquake", FireQuake, QuakeBusy)
                }
            },
            new Cat
            {
                id = "meme",
                icon = "^",
                tip = "Meme",
                subs = new[]
                {
                    SubOf("cat", "=", "Giant Cat", null, null)
                }
            }
        };
    }

    static Sub SubOf(string id, string icon, string title, Action fire, Func<bool> busy)
    {
        return new Sub
        {
            id = id,
            icon = icon,
            title = title,
            fire = fire,
            busy = busy ?? (() => false)
        };
    }

    void EnsureGfx()
    {
        if (pxFace != null)
            return;
        pxFace = Solid(Face);
        pxFaceOn = Solid(FaceOn);
        pxBorder = Solid(Border);
        pxBorderOn = Solid(BorderOn);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            fontStyle = FontStyle.Bold
        };
        labelStyle.normal.textColor = Color.white;

        toastStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        toastStyle.normal.textColor = new Color(1f, 0.85f, 0.55f);
    }

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.SetPixels(new[] { c, c, c, c });
        t.Apply(false, true);
        return t;
    }

    void OnGUI()
    {
        EnsureGfx();
        if (cats == null || cats.Length == 0)
            return;

        if (DisasterUiGate.ModalOpen)
        {
            BlocksGameplay = false;
            return;
        }

        float screenW = Screen.width;
        float catX = screenW - Pad - CatSize;
        float hitL = catX;
        float hitR = screenW - Pad;
        float hitT = Top;
        float hitB = Top + cats.Length * (CatSize + CatGap);

        // Category column (right)
        for (int i = 0; i < cats.Length; i++)
        {
            float y = Top + i * (CatSize + CatGap);
            Rect r = new Rect(catX, y, CatSize, CatSize);
            bool on = selectedCat == i && submenuOpen;
            if (DrawSquareButton(r, cats[i].icon, on))
            {
                if (selectedCat == i && submenuOpen)
                    submenuOpen = false;
                else
                {
                    selectedCat = i;
                    submenuOpen = true;
                    selectedSub = -1;
                }
            }
        }

        // Submenu column (to the left of categories)
        if (submenuOpen && selectedCat >= 0 && selectedCat < cats.Length)
        {
            var subs = cats[selectedCat].subs;
            float subX = catX - SubGap - SubW;
            hitL = Mathf.Min(hitL, subX);
            for (int i = 0; i < subs.Length; i++)
            {
                float y = Top + i * (SubH + SubGap);
                hitB = Mathf.Max(hitB, y + SubH);
                Rect r = new Rect(subX, y, SubW, SubH);
                bool on = selectedSub == i;
                bool busy = subs[i].busy != null && subs[i].busy();
                bool locked = subs[i].fire == null;

                if (DrawSubButton(r, subs[i].icon, subs[i].title, on, locked || busy))
                {
                    selectedSub = i;
                    if (locked)
                        toast = subs[i].title + " — coming soon";
                    else if (busy)
                        toast = "Already running...";
                    else
                    {
                        toast = null;
                        subs[i].fire?.Invoke();
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(toast))
            GUI.Label(new Rect(screenW * 0.5f - 160f, Screen.height - 48f, 320f, 28f), toast, toastStyle);

        Rect block = new Rect(hitL - 4f, hitT - 4f, hitR - hitL + 8f, hitB - hitT + 8f);
        BlocksGameplay = Event.current != null && block.Contains(Event.current.mousePosition);
    }

    bool DrawSquareButton(Rect r, string icon, bool on)
    {
        DrawFramed(r, on);
        var iconStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        iconStyle.normal.textColor = Color.white;
        GUI.Label(r, icon, iconStyle);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    bool DrawSubButton(Rect r, string icon, string title, bool on, bool dimmed)
    {
        DrawFramed(r, on);
        Color prev = GUI.color;
        if (dimmed)
            GUI.color = new Color(1f, 1f, 1f, 0.45f);

        var iconStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        iconStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x, r.y + 6f, r.width, 36f), icon, iconStyle);
        GUI.Label(new Rect(r.x + 4f, r.yMax - 28f, r.width - 8f, 24f), title.ToUpperInvariant(), labelStyle);

        GUI.color = prev;
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    void DrawFramed(Rect r, bool on)
    {
        GUI.DrawTexture(r, on ? pxBorderOn : pxBorder);
        Rect inner = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);
        GUI.DrawTexture(inner, on ? pxFaceOn : pxFace);
        Rect core = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);
        GUI.DrawTexture(core, on ? pxFaceOn : pxFace);
    }

    static bool IsMouseInRect(Rect r)
    {
        if (Event.current == null)
            return false;
        return r.Contains(Event.current.mousePosition);
    }

    // --- Actions ---

    static bool MoonBusy()
    {
        var m = MoonImpactSystem.Instance;
        return m != null && m.IsRunning;
    }

    static bool NukeBusy()
    {
        var w = NuclearWarSystem.Instance;
        return w != null && w.IsRunning;
    }

    static bool QuakeBusy()
    {
        var q = EarthquakeSystem.Instance;
        return q != null && q.IsRunning;
    }

    static void FireMoon(MoonImpactMode mode)
    {
        var moon = MoonImpactSystem.Instance;
        if (moon == null)
            return;
        moon.TryStart(mode);
    }

    static void FireNuclear()
    {
        var war = NuclearWarSystem.Instance;
        if (war == null)
            return;
        war.TryStart(100);
    }

    static void FireQuake()
    {
        EarthquakeConfirmUI.Ensure().Open(7.5f);
    }

    /// <summary>좌클릭과 동일 — 소행성</summary>
    static void FireSmallAsteroid()
    {
        var launcher = UnityEngine.Object.FindObjectOfType<MeteorLauncher>();
        if (launcher != null)
            launcher.FireTowardCamera();
    }

    /// <summary>우클릭/Space와 동일 — 큰 운석</summary>
    static void FireBigMeteor()
    {
        var big = UnityEngine.Object.FindObjectOfType<BigMeteorStrike>();
        if (big != null)
            big.FireRandom();
    }
}
