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
    const float SubW = 96f;
    const float SubH = 62f;
    const float SubGap = 5f;
    const float Pad = 12f;
    const float Top = 64f;

    static readonly Color Border = new Color(0.55f, 0.58f, 0.62f, 0.95f);
    static readonly Color BorderOn = new Color(1f, 0.45f, 0.12f, 1f);
    static readonly Color Face = new Color(0.12f, 0.13f, 0.15f, 0.92f);
    static readonly Color FaceOn = new Color(0.18f, 0.14f, 0.1f, 0.95f);

    int selectedCat = 0; // 1번 Impact
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
            // 1번: 우주/충격
            new Cat
            {
                id = "impact",
                icon = "1",
                tip = "Impact",
                subs = new[]
                {
                    SubOf("asteroid", "*", "Asteroid", FireSmallAsteroid, () => false),
                    SubOf("shower", "S", "Meteor Shower", FireMeteorShower, ShowerBusy),
                    SubOf("moon_crash", "O", "Moon Crash", () => FireMoon(MoonImpactMode.Crash), MoonBusy),
                    SubOf("blackhole", "B", "Black Hole", () => ArmCosmic(CosmicAnomalyKind.BlackHole), () => false),
                    SubOf("vortex", "V", "Vortex", () => ArmCosmic(CosmicAnomalyKind.Vortex), () => false)
                }
            },
            // 2번: 미사일
            new Cat
            {
                id = "missile",
                icon = "2",
                tip = "Missile",
                subs = new[]
                {
                    SubOf("nuke_missile", "N", "Nuke Missile", () => ArmNuke(NukeStrikeKind.Nuclear), () => false),
                    SubOf("fusion", "F", "Fusion Core", () => ArmNuke(NukeStrikeKind.FusionCore), () => false),
                    SubOf("station", "T", "Missile Station", null, null),
                    SubOf("remote", "R", "Remote Detonate", null, null),
                    SubOf("antimatter", "A", "Antimatter", () => ArmNuke(NukeStrikeKind.Antimatter), () => false),
                    SubOf("drill", "D", "Mining Drill", () => ArmNuke(NukeStrikeKind.MiningDrill), () => false),
                    SubOf("guided", "G", "Guided Missile", () => ArmNuke(NukeStrikeKind.Guided), () => false)
                }
            },
            new Cat
            {
                id = "war",
                icon = "3",
                tip = "War",
                subs = new[]
                {
                    SubOf("nuke_war", "N", "Nuclear War", FireNuclear, NukeBusy)
                }
            },
            // 4번: 우주선 소환
            new Cat
            {
                id = "fleet",
                icon = "4",
                tip = "Fleet",
                subs = new[]
                {
                    SubOf("ufo", "U", "UFO", () => SummonShip(SpacecraftKind.Ufo), () => false),
                    SubOf("cannon", "C", "Orbital Cannon", () => SummonShip(SpacecraftKind.OrbitalCannon), () => false),
                    SubOf("fighters", "F", "Fighter Wing", () => SummonShip(SpacecraftKind.FighterWing), () => false),
                    SubOf("battleship", "B", "Battleship", () => SummonShip(SpacecraftKind.Battleship), () => false),
                    SubOf("planet_killer", "P", "Planet Killer", () => SummonShip(SpacecraftKind.PlanetKiller), () => false),
                    SubOf("von_neumann", "V", "Von Neumann", () => SummonShip(SpacecraftKind.VonNeumannProbe), () => false)
                }
            },
            new Cat
            {
                id = "quake",
                icon = "5",
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
                bool on = selectedSub == i || IsSubAiming(subs[i].id);
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
                        if (IsSubAiming(subs[i].id))
                            toast = "Click Earth to use " + subs[i].title;
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

    static bool IsSubAiming(string id)
    {
        var nuke = NuclearMissileStrike.Instance;
        if (nuke != null && nuke.IsAiming)
        {
            switch (id)
            {
                case "nuke_missile": return nuke.AimKind == NukeStrikeKind.Nuclear;
                case "fusion": return nuke.AimKind == NukeStrikeKind.FusionCore;
                case "antimatter": return nuke.AimKind == NukeStrikeKind.Antimatter;
                case "drill": return nuke.AimKind == NukeStrikeKind.MiningDrill;
                case "guided": return nuke.AimKind == NukeStrikeKind.Guided;
            }
        }

        var cosmic = CosmicAnomalySystem.Instance;
        if (cosmic != null && cosmic.IsAiming)
        {
            if (id == "blackhole") return cosmic.AimKind == CosmicAnomalyKind.BlackHole;
            if (id == "vortex") return cosmic.AimKind == CosmicAnomalyKind.Vortex;
        }

        return false;
    }

    static bool MoonBusy()
    {
        var m = MoonImpactSystem.Instance;
        return m != null && m.IsRunning;
    }

    static bool ShowerBusy()
    {
        var s = MeteorShowerSystem.Instance;
        return s != null && s.IsRunning;
    }

    static bool NukeBusy()
    {
        var w = NuclearWarSystem.Instance;
        return w != null && w.IsRunning;
    }

    static void ArmNuke(NukeStrikeKind kind)
    {
        // cancel cosmic aim if any
        CosmicAnomalySystem.Instance?.CancelAim();

        var strike = NuclearMissileStrike.Ensure();
        if (strike == null)
            return;
        if (strike.IsAiming && strike.AimKind == kind)
            strike.CancelAim();
        else
            strike.BeginAim(kind);
    }

    static void ArmCosmic(CosmicAnomalyKind kind)
    {
        NuclearMissileStrike.Instance?.CancelAim();

        var cosmic = CosmicAnomalySystem.Ensure();
        if (cosmic == null)
            return;
        if (cosmic.IsAiming && cosmic.AimKind == kind)
            cosmic.CancelAim();
        else
            cosmic.BeginAim(kind);
    }

    static bool FleetBusy()
    {
        var f = SpacecraftFleetSystem.Instance;
        return f != null && f.IsBusy;
    }

    static void SummonShip(SpacecraftKind kind)
    {
        SpacecraftFleetSystem.Ensure().TrySummon(kind);
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

    static void FireMeteorShower()
    {
        MeteorShowerSystem.Ensure().TryStart();
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
}
