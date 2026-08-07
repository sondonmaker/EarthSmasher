using UnityEngine;

/// <summary>Asset Store SF Fighter / UFO Battleship 등 함대 3D 모델 로드·스케일.</summary>
public static class FleetShipModels
{
    const string FleetCatalogPath = "Fleet/Catalog";

    enum FleetVisualPose
    {
        MiddleForward,
        LongestForward
    }

    static FleetVisualCatalog fleetCatalog;

    /// <summary>지구 궤도용 전함 비주얼. 모델 없으면 null.</summary>
    public static GameObject SpawnBattleship(Vector3 position, Quaternion rotation, float earthRadius)
    {
        return SpawnFromTemplate(
            "Battleship",
            "SF_Fighter",
            LoadBattleshipTemplate(),
            position,
            rotation,
            earthRadius * 0.28f);
    }

    /// <summary>Generic Aircraft 전투기. variant로 기체 모양 섞기.</summary>
    public static GameObject SpawnFighter(Vector3 position, Quaternion rotation, float earthRadius, int variant = 0)
    {
        return SpawnFromTemplate(
            "Fighter",
            "GenericAircraft",
            LoadFighterTemplate(variant),
            position,
            rotation,
            earthRadius * 0.14f,
            FleetVisualPose.LongestForward);
    }

    /// <summary>행성 킬러급 대형 기체.</summary>
    public static GameObject SpawnPlanetKiller(Vector3 position, Quaternion rotation, float earthRadius)
    {
        return SpawnFromTemplate(
            "PlanetKiller",
            "GenericAircraft",
            LoadPlanetKillerTemplate(),
            position,
            rotation,
            earthRadius * 0.52f,
            FleetVisualPose.LongestForward);
    }

    /// <summary>von Neumann 프로브(소형 드론).</summary>
    public static GameObject SpawnProbe(Vector3 position, Quaternion rotation, float earthRadius)
    {
        return SpawnFromTemplate(
            "Probe",
            "GenericAircraft",
            LoadProbeTemplate(),
            position,
            rotation,
            earthRadius * 0.045f,
            FleetVisualPose.LongestForward);
    }

    /// <summary>궤도 포대(헬리/건쉽형).</summary>
    public static GameObject SpawnOrbitalCannon(Vector3 position, Quaternion rotation, float earthRadius)
    {
        return SpawnFromTemplate(
            "OrbitalCannon",
            "GenericAircraft",
            LoadOrbitalCannonTemplate(),
            position,
            rotation,
            earthRadius * 0.2f,
            FleetVisualPose.MiddleForward);
    }

    /// <summary>비행체 진행 방향 + 지구 중심 기준 bank.</summary>
    public static Quaternion BuildFlightRotation(
        Vector3 shipWorldPos,
        Vector3 earthCenter,
        Vector3 forward,
        float pitchDeg = 0f,
        float rollDeg = 0f)
    {
        Vector3 radial = (shipWorldPos - earthCenter).normalized;
        if (radial.sqrMagnitude < 1e-6f)
            radial = Vector3.up;

        forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : -radial;
        Vector3 wing = Vector3.Cross(radial, forward);
        if (wing.sqrMagnitude < 1e-4f)
            wing = Vector3.Cross(radial, Vector3.right);
        wing.Normalize();

        Vector3 up = Vector3.Cross(forward, wing);
        if (up.sqrMagnitude < 1e-4f)
            up = radial;
        up.Normalize();

        var rot = Quaternion.LookRotation(forward, up);
        if (Mathf.Abs(pitchDeg) > 0.01f)
            rot *= Quaternion.AngleAxis(pitchDeg, Vector3.right);
        if (Mathf.Abs(rollDeg) > 0.01f)
            rot *= Quaternion.AngleAxis(rollDeg, Vector3.forward);
        return rot;
    }

    /// <summary>궤도 위에서 지구(또는 조준점)를 향하고, 짧은 축으로 수평 유지.</summary>
    public static Quaternion BuildOrbitRotation(Vector3 shipWorldPos, Vector3 earthCenter, Vector3? aimWorld = null)
    {
        Vector3 radialOut = (shipWorldPos - earthCenter).normalized;
        if (radialOut.sqrMagnitude < 1e-6f)
            radialOut = Vector3.up;

        Vector3 toTarget = aimWorld.HasValue
            ? (aimWorld.Value - shipWorldPos).normalized
            : -radialOut;

        Vector3 wing = Vector3.Cross(radialOut, Vector3.up);
        if (wing.sqrMagnitude < 1e-4f)
            wing = Vector3.Cross(radialOut, Vector3.forward);
        wing.Normalize();

        Vector3 shipUp = Vector3.Cross(wing, toTarget);
        if (shipUp.sqrMagnitude < 1e-4f)
            shipUp = Vector3.Cross(toTarget, wing);
        shipUp.Normalize();

        return Quaternion.LookRotation(toTarget, shipUp);
    }

    /// <summary>지구 궤도 UFO. FlexUnit UFO Battleship(#289193) 등. 없으면 null.</summary>
    public static GameObject SpawnUfo(Vector3 position, Vector3 awayFromEarth, float earthRadius)
    {
        var template = LoadUfoTemplate();
        if (template == null)
            return null;

        Vector3 up = awayFromEarth.normalized;
        Vector3 tangent = Vector3.Cross(up, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f)
            tangent = Vector3.Cross(up, Vector3.right);
        var rotation = Quaternion.LookRotation(tangent.normalized, up);

        var root = new GameObject("UFO");
        var visual = Object.Instantiate(template, root.transform);
        visual.name = "UFO_Battleship";
        PrepareShipRoot(visual);

        float targetSize = earthRadius * 0.22f;
        FitToLength(visual.transform, targetSize);
        OrientVisualForOrbit(visual.transform);
        visual.transform.localPosition = Vector3.zero;

        root.transform.rotation = rotation;
        root.transform.position = position;

        return root;
    }

    /// <summary>짧은 축=up(수평), 중간 축=nose(forward). 날개 span(가장 긴 축)은 옆으로.</summary>
    static void OrientBattleshipVisual(Transform visual)
    {
        var bounds = ComputeLocalBounds(visual);
        Vector3 size = bounds.size;
        if (size.sqrMagnitude < 1e-8f)
        {
            visual.localRotation = Quaternion.identity;
            return;
        }

        int[] order = { 0, 1, 2 };
        float[] dims = { size.x, size.y, size.z };
        System.Array.Sort(order, (a, b) => dims[a].CompareTo(dims[b]));

        Vector3 upAxis = AxisIndex(order[0]);
        Vector3 fwdAxis = AxisIndex(order[1]);

        var toUp = Quaternion.FromToRotation(upAxis, Vector3.up);
        Vector3 fwdRot = toUp * fwdAxis;
        if (Vector3.Dot(fwdRot, Vector3.forward) < 0f)
            fwdAxis = -fwdAxis;
        fwdRot = toUp * fwdAxis;
        var toFwd = Quaternion.FromToRotation(fwdRot, Vector3.forward);
        visual.localRotation = toFwd * toUp;
    }

    /// <summary>Generic Aircraft — 가장 긴 축=nose(forward), 짧은 축=up.</summary>
    static void OrientLongestForwardVisual(Transform visual)
    {
        var bounds = ComputeLocalBounds(visual);
        Vector3 size = bounds.size;
        if (size.sqrMagnitude < 1e-8f)
        {
            visual.localRotation = Quaternion.identity;
            return;
        }

        Vector3 upAxis = AxisVector(size, pickLongest: false);
        Vector3 fwdAxis = AxisVector(size, pickLongest: true);

        var toUp = Quaternion.FromToRotation(upAxis, Vector3.up);
        Vector3 fwdRot = toUp * fwdAxis;
        if (Vector3.Dot(fwdRot, Vector3.forward) < 0f)
            fwdAxis = -fwdAxis;
        fwdRot = toUp * fwdAxis;
        var toFwd = Quaternion.FromToRotation(fwdRot, Vector3.forward);
        visual.localRotation = toFwd * toUp;
    }

    static void OrientVisual(Transform visual, FleetVisualPose pose)
    {
        if (pose == FleetVisualPose.LongestForward)
            OrientLongestForwardVisual(visual);
        else
            OrientBattleshipVisual(visual);
    }

    static Vector3 AxisIndex(int index) =>
        index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;

    /// <summary>짧은 축=지구 밖(up), 긴 축=궤도 접선(forward). UFO용.</summary>
    static void OrientVisualForOrbit(Transform visual)
    {
        var bounds = ComputeLocalBounds(visual);
        Vector3 size = bounds.size;
        if (size.sqrMagnitude < 1e-8f)
        {
            visual.localRotation = Quaternion.identity;
            return;
        }

        Vector3 shortAxis = AxisVector(size, pickLongest: false);
        Vector3 longAxis = AxisVector(size, pickLongest: true);

        var toUp = Quaternion.FromToRotation(shortAxis, Vector3.up);
        Vector3 longRot = toUp * longAxis;
        var toFwd = Quaternion.FromToRotation(longRot, Vector3.forward);
        visual.localRotation = toFwd * toUp;
    }

    static Vector3 AxisVector(Vector3 size, bool pickLongest)
    {
        bool xBest = pickLongest
            ? size.x >= size.y && size.x >= size.z
            : size.x <= size.y && size.x <= size.z;
        bool yBest = pickLongest
            ? size.y >= size.x && size.y >= size.z
            : size.y <= size.x && size.y <= size.z;

        if (xBest)
            return Vector3.right;
        if (yBest)
            return Vector3.up;
        return Vector3.forward;
    }

    static Bounds ComputeLocalBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one * 0.01f);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float inv = 1f / Mathf.Max(root.lossyScale.x, 1e-6f);
        Vector3 center = root.InverseTransformPoint(bounds.center);
        return new Bounds(center, bounds.size * inv);
    }

    static GameObject SpawnFromTemplate(
        string rootName,
        string visualName,
        GameObject template,
        Vector3 position,
        Quaternion rotation,
        float targetLength,
        FleetVisualPose pose = FleetVisualPose.MiddleForward)
    {
        if (template == null)
            return null;

        var root = new GameObject(rootName);
        var visual = Object.Instantiate(template, root.transform);
        visual.name = visualName;
        PrepareShipRoot(visual);

        FitToLength(visual.transform, targetLength);
        OrientVisual(visual.transform, pose);
        visual.transform.localPosition = Vector3.zero;

        root.transform.SetPositionAndRotation(position, rotation);
        return root;
    }

    static GameObject LoadBattleshipTemplate()
    {
        var catalog = LoadFleetCatalog();
        if (catalog != null && catalog.battleship != null)
            return catalog.battleship;

        var fromResources = Resources.Load<GameObject>("Fleet/Battleship");
        if (fromResources != null)
            return fromResources;

        fromResources = Resources.Load<GameObject>("Fleet/SF_Free-Fighter");
        if (fromResources != null)
            return fromResources;

#if UNITY_EDITOR
        return LoadBattleshipTemplateEditor();
#else
        return null;
#endif
    }

    static GameObject LoadUfoTemplate()
    {
        var catalog = LoadFleetCatalog();
        if (catalog != null && catalog.ufo != null)
            return catalog.ufo;

        var fromResources = Resources.Load<GameObject>("Fleet/UFO");
        if (fromResources != null)
            return fromResources;

#if UNITY_EDITOR
        return LoadUfoTemplateEditor();
#else
        return null;
#endif
    }

    static GameObject LoadFighterTemplate(int variant)
    {
        var catalog = LoadFleetCatalog();
        if (catalog != null)
        {
            if (variant % 3 == 0 && catalog.fighter != null)
                return catalog.fighter;
            if (variant % 3 == 1 && catalog.fighterAlt != null)
                return catalog.fighterAlt;
            if (catalog.fighter != null)
                return catalog.fighter;
        }

#if UNITY_EDITOR
        string[] names = { "aircraft-f", "aircraft-c", "aircraft-d" };
        return LoadGenericAircraftEditor(names[Mathf.Abs(variant) % names.Length]);
#else
        return null;
#endif
    }

    static GameObject LoadPlanetKillerTemplate()
    {
        var catalog = LoadFleetCatalog();
        if (catalog != null && catalog.planetKiller != null)
            return catalog.planetKiller;

#if UNITY_EDITOR
        return LoadGenericAircraftEditor("aircraft-k");
#else
        return null;
#endif
    }

    static GameObject LoadProbeTemplate()
    {
        var catalog = LoadFleetCatalog();
        if (catalog != null && catalog.probe != null)
            return catalog.probe;

#if UNITY_EDITOR
        return LoadGenericAircraftEditor("aircraft-a");
#else
        return null;
#endif
    }

    static GameObject LoadOrbitalCannonTemplate()
    {
        var catalog = LoadFleetCatalog();
        if (catalog != null && catalog.orbitalCannon != null)
            return catalog.orbitalCannon;

#if UNITY_EDITOR
        return LoadGenericAircraftEditor("aircraft-h");
#else
        return null;
#endif
    }

    static FleetVisualCatalog LoadFleetCatalog()
    {
        if (fleetCatalog == null)
            fleetCatalog = Resources.Load<FleetVisualCatalog>(FleetCatalogPath);
        return fleetCatalog;
    }

    static void PrepareShipRoot(GameObject root)
    {
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            Object.Destroy(col);

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            ps.gameObject.SetActive(false);

        foreach (var light in root.GetComponentsInChildren<Light>(true))
            light.enabled = false;

        foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void FitToLength(Transform root, float targetLength)
    {
        var bounds = ComputeBounds(root);
        if (bounds.size.sqrMagnitude < 1e-8f)
            return;

        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longest < 1e-6f)
            return;

        float scale = targetLength / longest;
        root.localScale *= scale;
    }

    static Bounds ComputeBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.position, Vector3.one * 0.01f);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

#if UNITY_EDITOR
    static GameObject LoadBattleshipTemplateEditor()
    {
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(
            "Assets/Resources/Fleet/Catalog.asset");
        if (catalog != null && catalog.battleship != null)
            return catalog.battleship;

        string[] names = { "SF_Free-Fighter", "SF_Fighter" };
        for (int n = 0; n < names.Length; n++)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(names[n] + " t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.Contains("/Resources/Fleet/"))
                    continue;
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                    return go;
            }
        }

        return null;
    }

    static GameObject LoadUfoTemplateEditor()
    {
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(
            "Assets/Resources/Fleet/Catalog.asset");
        if (catalog != null && catalog.ufo != null)
            return catalog.ufo;

        string[] paths =
        {
            "Assets/FlexUnit/UFO_Battleship/Built-In/Prefabs/UFO_Color1.prefab",
            "Assets/FlexUnit/UFO_Battleship/URP/Prefabs/UFO_Color1.prefab"
        };
        for (int i = 0; i < paths.Length; i++)
        {
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (go != null)
                return go;
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets("UFO_Color1 t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
                return go;
        }

        return null;
    }

    static GameObject LoadGenericAircraftEditor(string prefabName)
    {
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<FleetVisualCatalog>(
            "Assets/Resources/Fleet/Catalog.asset");
        if (catalog != null)
        {
            if (prefabName == "aircraft-f" && catalog.fighter != null)
                return catalog.fighter;
            if (prefabName == "aircraft-c" && catalog.fighterAlt != null)
                return catalog.fighterAlt;
            if (prefabName == "aircraft-k" && catalog.planetKiller != null)
                return catalog.planetKiller;
            if (prefabName == "aircraft-a" && catalog.probe != null)
                return catalog.probe;
            if (prefabName == "aircraft-h" && catalog.orbitalCannon != null)
                return catalog.orbitalCannon;
        }

        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Generic Aircraft Models/Prefabs/Aircrafts/{prefabName}.prefab");
    }
#endif
}
