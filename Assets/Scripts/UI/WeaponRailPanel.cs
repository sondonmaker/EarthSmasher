using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Solar Smash 스타일 2단 무기 레일.
/// 메뉴 선택 → 지구 클릭으로 발동.
/// </summary>
public class WeaponRailPanel : MonoBehaviour
{
    public static WeaponRailPanel Instance { get; private set; }
    public static bool BlocksGameplay { get; private set; }
    public static bool IsArmed => Instance != null && !string.IsNullOrEmpty(Instance.armedId);

    const float CatSize = 56f;
    const float CatGap = 8f;
    const float SubW = 96f;
    const float SubH = 62f;
    const float SubGap = 5f;
    const float Pad = 12f;
    const float Top = 64f;
    const float TapMoveThreshold = 14f;

    static readonly Color Border = new Color(0.55f, 0.58f, 0.62f, 0.95f);
    static readonly Color BorderOn = new Color(1f, 0.45f, 0.12f, 1f);
    static readonly Color Face = new Color(0.12f, 0.13f, 0.15f, 0.92f);
    static readonly Color FaceOn = new Color(0.18f, 0.14f, 0.1f, 0.95f);

    int selectedCat = 0;
    int selectedSub = -1;
    bool submenuOpen = true;
    string toast;
    string armedId;
    string armedTitle;

    bool pressTracking;
    Vector2 pressPos;

    Texture2D pxFace;
    Texture2D pxFaceOn;
    Texture2D pxBorder;
    Texture2D pxBorderOn;
    GUIStyle labelStyle;
    GUIStyle toastStyle;

    struct Cat
    {
        public string id;
        public string icon;
        public string tip;
        public Sub[] subs;
    }

    struct Sub
    {
        public string id;
        public string title;
        public string icon;
        public bool locked;
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

    public void ClearArm()
    {
        armedId = null;
        armedTitle = null;
        toast = null;
        pressTracking = false;
    }

    void BuildCatalog()
    {
        cats = new[]
        {
            new Cat
            {
                id = "impact",
                icon = "1",
                tip = "Impact",
                subs = new[]
                {
                    SubOf("asteroid", "*", "Asteroid"),
                    SubOf("shower", "S", "Meteor Shower", ShowerBusy),
                    SubOf("moon_crash", "O", "Moon Crash", MoonBusy),
                    SubOf("blackhole", "B", "Black Hole"),
                    SubOf("spike", "X", "Spike Erupt"),
                    SubOf("vortex", "V", "Vortex")
                }
            },
            new Cat
            {
                id = "missile",
                icon = "2",
                tip = "Missile",
                subs = new[]
                {
                    SubOf("nuke_missile", "N", "Nuke Missile"),
                    SubOf("fusion", "F", "Fusion Core"),
                    SubOf("station", "T", "Missile Station", null, true),
                    SubOf("remote", "R", "Remote Detonate", null, true),
                    SubOf("antimatter", "A", "Antimatter"),
                    SubOf("drill", "D", "Mining Drill"),
                    SubOf("guided", "G", "Guided Missile")
                }
            },
            new Cat
            {
                id = "war",
                icon = "3",
                tip = "War",
                subs = new[]
                {
                    SubOf("nuke_war", "N", "Nuclear War", NukeBusy),
                    SubOf("quake", "E", "Earthquake", QuakeBusy)
                }
            },
            new Cat
            {
                id = "fleet",
                icon = "4",
                tip = "Fleet",
                subs = new[]
                {
                    SubOf("ufo", "U", "UFO"),
                    SubOf("cannon", "C", "Orbital Cannon"),
                    SubOf("fighters", "F", "Fighter Wing"),
                    SubOf("battleship", "B", "Battleship"),
                    SubOf("planet_killer", "P", "Planet Killer"),
                    SubOf("von_neumann", "V", "Von Neumann")
                }
            },
            new Cat
            {
                id = "laser",
                icon = "5",
                tip = "Laser",
                subs = new[]
                {
                    SubOf("laser_fire", "1", "Fire Laser"),
                    SubOf("laser_ice", "2", "Ice Laser"),
                    SubOf("laser_pierce", "3", "Pierce Laser"),
                    SubOf("laser_plasma", "4", "Plasma Laser"),
                    SubOf("laser_bolt", "5", "Lightning")
                }
            },
            new Cat
            {
                id = "meme",
                icon = "M",
                tip = "Meme",
                subs = new[]
                {
                    SubOf("doge", "D", "Doge Strike", MemeBusy),
                    SubOf("to_the_moon", "T", "To The Moon", MoonRunBusy),
                    SubOf("pengu_coin", "G", "Pengu Coin", PenguBusy),
                    SubOf("trump_tariff", "R", "Trump Tariff", TrumpBusy),
                    SubOf("trump_market_crash", "C", "Market Crash", TrumpCrashBusy),
                    SubOf("pepe", "P", "Pepe Punch"),
                    SubOf("cat", "=", "Giant Cat"),
                    SubOf("shark", "S", "Sneaker Shark"),
                    SubOf("trojan_horse", "H", "Trojan Horse", TrojanBusy)
                }
            }
        };
    }

    static Sub SubOf(string id, string icon, string title, Func<bool> busy = null, bool locked = false)
    {
        return new Sub
        {
            id = id,
            icon = icon,
            title = title,
            busy = busy ?? (() => false),
            locked = locked
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

    void Update()
    {
        if (string.IsNullOrEmpty(armedId))
            return;
        if (DisasterUiGate.ModalOpen)
        {
            ClearArm();
            return;
        }

        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame))
        {
            ClearArm();
            return;
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            ClearArm();
            return;
        }

        if (BlocksGameplay || EarthLayerToolbar.BlocksGameplayInput
            || ZoomUiBlocker.BlocksGameplay || WorldStatusHud.BlocksGameplay)
            return;

        if (!TryConsumeTap(out Vector2 screenPos))
            return;
        if (!TryGetEarthHit(screenPos, out Vector3 point, out Vector3 normal))
            return;

        ExecuteArmed(point, normal);
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

        MobileUi.Begin();
        DrawRail();
        MobileUi.End();
    }

    void DrawRail()
    {
        float screenW = MobileUi.Width;
        float catX = screenW - Pad - CatSize;
        float hitL = catX;
        float hitR = screenW - Pad;
        float hitT = Top;
        float hitB = Top + cats.Length * (CatSize + CatGap);

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

        if (submenuOpen && selectedCat >= 0 && selectedCat < cats.Length)
        {
            var subs = cats[selectedCat].subs;
            float subX = catX - SubGap - SubW;
            hitL = Mathf.Min(hitL, subX);

            float catY = Top + selectedCat * (CatSize + CatGap);
            float subH = SubH;
            float subGap = SubGap;
            float panelH = subs.Length * (subH + subGap) - subGap;
            float availH = MobileUi.Height - Top - Pad * 2f;
            if (panelH > availH && subs.Length > 0)
            {
                subGap = 3f;
                subH = Mathf.Max(42f, (availH + subGap) / subs.Length - subGap);
                panelH = subs.Length * (subH + subGap) - subGap;
            }

            // 선택한 카테고리 버튼 위쪽으로 펼쳐서 하단(캣/샤크/카우)이 잘리지 않게
            float subBaseY = catY + CatSize - panelH;
            if (subBaseY < Top)
                subBaseY = Top;
            float maxBaseY = MobileUi.Height - Pad - panelH;
            if (subBaseY > maxBaseY)
                subBaseY = Mathf.Max(Top, maxBaseY);

            for (int i = 0; i < subs.Length; i++)
            {
                float y = subBaseY + i * (subH + subGap);
                hitB = Mathf.Max(hitB, y + subH);
                hitT = Mathf.Min(hitT, y);
                Rect r = new Rect(subX, y, SubW, subH);
                bool on = selectedSub == i || armedId == subs[i].id;
                bool busy = subs[i].busy != null && subs[i].busy();
                bool locked = subs[i].locked;

                if (DrawSubButton(r, subs[i].icon, subs[i].title, on, locked || busy))
                {
                    selectedSub = i;
                    if (locked)
                        toast = subs[i].title + " — coming soon";
                    else if (busy)
                        toast = "Already running...";
                    else
                        ArmWeapon(subs[i].id, subs[i].title);
                }
            }
        }

        float screenH = MobileUi.Height;
        bool touch = MobileUi.IsTouchDevice;

        string hint = !string.IsNullOrEmpty(armedId)
            ? (armedTitle ?? armedId).ToUpperInvariant() + (touch ? " armed — tap Earth" : " armed — click Earth  (Esc/RMB cancel)")
            : toast;
        if (!string.IsNullOrEmpty(hint))
            GUI.Label(new Rect(screenW * 0.5f - 220f, screenH - 48f, 440f, 28f), hint, toastStyle);

        Rect block = new Rect(hitL - 4f, hitT - 4f, hitR - hitL + 8f, hitB - hitT + 8f);
        bool blocking = Event.current != null && block.Contains(Event.current.mousePosition);

        // 폰에는 Esc/우클릭이 없으니 조준 해제 버튼을 띄운다.
        if (touch && !string.IsNullOrEmpty(armedId))
        {
            Rect cancel = new Rect(Pad, screenH - 62f, 104f, 46f);
            if (DrawSubButton(cancel, "X", "Cancel", false, false))
                ClearArm();
            if (Event.current != null && cancel.Contains(Event.current.mousePosition))
                blocking = true;
        }

        BlocksGameplay = blocking;
    }

    void ArmWeapon(string id, string title)
    {
        if (armedId == id)
        {
            ClearArm();
            return;
        }

        armedId = id;
        armedTitle = title;
        toast = null;
        pressTracking = false;
    }

    void ExecuteArmed(Vector3 point, Vector3 normal)
    {
        string id = armedId;
        if (string.IsNullOrEmpty(id))
            return;

        switch (id)
        {
            case "asteroid":
            {
                var launcher = FindObjectOfType<MeteorLauncher>();
                if (launcher != null)
                    launcher.FireAt(point, normal);
                break;
            }
            case "shower":
                MeteorShowerSystem.Ensure().TryStartAt(point);
                break;
            case "moon_crash":
                MoonImpactSystem.Instance?.TryStartAt(MoonImpactMode.Crash, point);
                break;
            case "blackhole":
                CosmicAnomalySystem.Ensure().SpawnAt(CosmicAnomalyKind.BlackHole, point, normal);
                break;
            case "spike":
                CosmicAnomalySystem.Ensure().SpawnAt(CosmicAnomalyKind.SpikeErupt, point, normal);
                break;
            case "vortex":
                CosmicAnomalySystem.Ensure().SpawnAt(CosmicAnomalyKind.Vortex, point, normal);
                break;
            case "nuke_missile":
                NuclearMissileStrike.Ensure().FireAtKind(NukeStrikeKind.Nuclear, point, normal);
                break;
            case "fusion":
                NuclearMissileStrike.Ensure().FireAtKind(NukeStrikeKind.FusionCore, point, normal);
                break;
            case "antimatter":
                NuclearMissileStrike.Ensure().FireAtKind(NukeStrikeKind.Antimatter, point, normal);
                break;
            case "drill":
                NuclearMissileStrike.Ensure().FireAtKind(NukeStrikeKind.MiningDrill, point, normal);
                break;
            case "guided":
                NuclearMissileStrike.Ensure().FireAtKind(NukeStrikeKind.Guided, point, normal);
                break;
            case "nuke_war":
                NuclearWarSystem.Instance?.TryStart(100);
                break;
            case "laser_fire":
                LaserStrikeSystem.Ensure().FireAt(PlanetLaserKind.Fire, point, normal);
                break;
            case "laser_ice":
                LaserStrikeSystem.Ensure().FireAt(PlanetLaserKind.Ice, point, normal);
                break;
            case "laser_pierce":
                LaserStrikeSystem.Ensure().FireAt(PlanetLaserKind.Pierce, point, normal);
                break;
            case "laser_plasma":
                LaserStrikeSystem.Ensure().FireAt(PlanetLaserKind.Plasma, point, normal);
                break;
            case "laser_bolt":
                LaserStrikeSystem.Ensure().FireAt(PlanetLaserKind.Lightning, point, normal);
                break;
            case "ufo":
                SpacecraftFleetSystem.Ensure().TrySummonAt(SpacecraftKind.Ufo, point);
                break;
            case "cannon":
                SpacecraftFleetSystem.Ensure().TrySummonAt(SpacecraftKind.OrbitalCannon, point);
                break;
            case "fighters":
                SpacecraftFleetSystem.Ensure().TrySummonAt(SpacecraftKind.FighterWing, point);
                break;
            case "battleship":
                SpacecraftFleetSystem.Ensure().TrySummonAt(SpacecraftKind.Battleship, point);
                break;
            case "planet_killer":
                SpacecraftFleetSystem.Ensure().TrySummonAt(SpacecraftKind.PlanetKiller, point);
                break;
            case "von_neumann":
                SpacecraftFleetSystem.Ensure().TrySummonAt(SpacecraftKind.VonNeumannProbe, point);
                break;
            case "doge":
            case "to_the_moon":
            case "pengu_coin":
            case "trump_tariff":
            case "trump_market_crash":
            case "trojan_horse":
            case "pepe":
            case "cat":
            case "shark":
                if (!MemeAttackSystem.Ensure().TryFire(id, point, normal))
                {
                    if (id == "cat" && MemeCatUnit.ActiveCount >= 1)
                        toast = "Giant Cat already scratching";
                    else if (id == "to_the_moon" && MemeAttackSystem.Instance != null && MemeAttackSystem.Instance.IsMoonRunBusy)
                        toast = "Too many To The Moon runs";
                    else if (id == "pengu_coin" && MemeAttackSystem.Instance != null && MemeAttackSystem.Instance.IsPenguRunning)
                        toast = "Pengu Coin already running";
                    else if (id == "trump_tariff" && MemeAttackSystem.Instance != null && MemeAttackSystem.Instance.IsTrumpRunning)
                        toast = "Trump Tariff already running";
                    else if (id == "trump_market_crash" && MemeAttackSystem.Instance != null && MemeAttackSystem.Instance.IsTrumpCrashRunning)
                        toast = "Market Crash already running";
                    else if (id == "trojan_horse" && MemeAttackSystem.Instance != null && MemeAttackSystem.Instance.IsTrojanRunning)
                        toast = "Trojan Horse already running";
                    else if (id == "doge" && MemeAttackSystem.Instance != null && MemeAttackSystem.Instance.IsDogeRunning)
                        toast = "Doge Strike already running";
                    else if (MemeUnitBase.LiveCount >= 4)
                        toast = "Too many meme units — wait for one to finish";
                    else
                        toast = "Cannot fire meme weapon";
                }
                break;
            case "quake":
            {
                var earth = FindObjectOfType<EarthPlanet>();
                if (earth != null)
                {
                    Vector3 local = earth.transform.InverseTransformPoint(point).normalized;
                    EarthGeo.DirectionToLatLon(local, out float lat, out float lon);
                    EarthquakeSystem.Instance?.TryStart(7.5f, lat, lon);
                }
                break;
            }
        }

        // 같은 무기 연속 사용 가능 — 조준 유지
    }

    bool TryConsumeTap(out Vector2 screenPos)
    {
        screenPos = default;

        // 터치 우선 — 폰에서는 Mouse.current가 없거나 터치를 흉내 내기만 한다.
        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            int active = 0;
            int idx = -1;
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var t = touchscreen.touches[i];
                if (t.press.isPressed || t.press.wasPressedThisFrame || t.press.wasReleasedThisFrame)
                {
                    active++;
                    idx = i;
                }
            }

            // 두 손가락 이상은 카메라 조작 — 발사하지 않는다.
            if (active > 1)
            {
                pressTracking = false;
                return false;
            }

            if (active == 1)
            {
                var t = touchscreen.touches[idx];
                if (t.press.wasPressedThisFrame)
                {
                    pressTracking = true;
                    pressPos = t.position.ReadValue();
                    return false;
                }

                if (pressTracking && t.press.wasReleasedThisFrame)
                {
                    pressTracking = false;
                    Vector2 up = t.position.ReadValue();
                    if ((up - pressPos).magnitude <= TapMoveThreshold * MobileUi.Scale)
                    {
                        screenPos = up;
                        return true;
                    }
                }
                return false;
            }
        }

        var mouse = Mouse.current;
        if (mouse == null)
            return false;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressTracking = true;
            pressPos = mouse.position.ReadValue();
            return false;
        }

        if (pressTracking && mouse.leftButton.wasReleasedThisFrame)
        {
            pressTracking = false;
            Vector2 up = mouse.position.ReadValue();
            if ((up - pressPos).magnitude <= TapMoveThreshold)
            {
                screenPos = up;
                return true;
            }
        }

        return false;
    }

    bool TryGetEarthHit(Vector2 screenPos, out Vector3 point, out Vector3 normal)
    {
        point = default;
        normal = Vector3.up;
        var cam = Camera.main;
        var earth = FindObjectOfType<EarthPlanet>();
        if (cam == null || earth == null)
            return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
            return false;
        if (hit.collider == null || hit.collider.GetComponentInParent<EarthPlanet>() != earth)
            return false;

        point = hit.point;
        normal = hit.normal;
        return true;
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

    static bool QuakeBusy()
    {
        var q = EarthquakeSystem.Instance;
        return q != null && q.IsRunning;
    }

    static bool MemeBusy()
    {
        var m = MemeAttackSystem.Instance;
        return m != null && m.IsDogeRunning;
    }

    static bool MoonRunBusy()
    {
        var m = MemeAttackSystem.Instance;
        return m != null && m.IsMoonRunBusy;
    }

    static bool PenguBusy()
    {
        var m = MemeAttackSystem.Instance;
        return m != null && m.IsPenguRunning;
    }

    static bool TrumpBusy()
    {
        var m = MemeAttackSystem.Instance;
        return m != null && m.IsTrumpRunning;
    }

    static bool TrumpCrashBusy()
    {
        var m = MemeAttackSystem.Instance;
        return m != null && m.IsTrumpCrashRunning;
    }

    static bool TrojanBusy()
    {
        var m = MemeAttackSystem.Instance;
        return m != null && m.IsTrojanRunning;
    }
}
