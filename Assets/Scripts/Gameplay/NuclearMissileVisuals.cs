using UnityEngine;

/// <summary>Cartoon Low Poly Pack(73141) 등 핵미사일 3D 비주얼 — 랜덤 변형.</summary>
public static class NuclearMissileVisuals
{
    const string CatalogPath = "NuclearMissiles/Catalog";

    static NuclearMissileCatalog catalog;
    static GameObject[] cachedVariants;

    /// <summary>루트에 랜덤 미사일 메쉬 자식 추가. 없으면 null.</summary>
    public static GameObject AttachRandomVisual(Transform root, bool tiny, float earthRadius)
    {
        var template = PickRandomVariant();
        if (template == null)
            return null;

        var visual = Object.Instantiate(template, root);
        visual.name = "MissileVisual";
        PrepareVisual(visual);

        float targetLength = tiny ? earthRadius * 0.018f : 0.72f;
        FitToLength(visual.transform, targetLength);
        OrientVisualAlongForward(visual.transform);
        visual.transform.localPosition = Vector3.zero;

        return visual;
    }

    public static bool UsesCartoonVisual(Transform root)
    {
        return root != null && root.Find("MissileVisual") != null;
    }

    public static float ExhaustLocalZ(Transform root, bool tiny)
    {
        var visual = root.Find("MissileVisual");
        if (visual == null)
            return tiny ? -0.55f : -0.55f;

        var bounds = ComputeLocalBounds(visual);
        return bounds.min.z - bounds.extents.z * 0.12f;
    }

    static void OrientVisualAlongForward(Transform visual)
    {
        var bounds = ComputeLocalBounds(visual);
        Vector3 size = bounds.size;
        if (size.sqrMagnitude < 1e-8f)
        {
            visual.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            return;
        }

        Vector3 longAxis = LongestAxis(size);
        visual.localRotation = Quaternion.FromToRotation(longAxis, Vector3.forward);
    }

    static Vector3 LongestAxis(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z)
            return Vector3.right;
        if (size.y >= size.x && size.y >= size.z)
            return Vector3.up;
        return Vector3.forward;
    }

    static GameObject PickRandomVariant()
    {
        EnsureCache();
        if (cachedVariants == null || cachedVariants.Length == 0)
            return null;
        return cachedVariants[Random.Range(0, cachedVariants.Length)];
    }

    static void EnsureCache()
    {
        if (cachedVariants != null && cachedVariants.Length > 0)
            return;

        catalog = Resources.Load<NuclearMissileCatalog>(CatalogPath);
        if (catalog != null && catalog.variants != null && catalog.variants.Length > 0)
        {
            cachedVariants = catalog.variants;
            return;
        }

        cachedVariants = Resources.LoadAll<GameObject>("NuclearMissiles");
        if (cachedVariants != null && cachedVariants.Length > 0)
            return;

#if UNITY_EDITOR
        cachedVariants = LoadVariantsEditor();
#endif
    }

    static void PrepareVisual(GameObject root)
    {
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            Object.Destroy(col);

        foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void FitToLength(Transform root, float targetLength)
    {
        var bounds = ComputeLocalBounds(root);
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longest < 1e-6f)
            return;

        float scale = targetLength / longest;
        root.localScale *= scale;
    }

    static Bounds ComputeLocalBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one * 0.01f);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 center = root.InverseTransformPoint(bounds.center);
        Vector3 size = bounds.size / Mathf.Max(root.lossyScale.x, 1e-6f);
        return new Bounds(center, size);
    }

#if UNITY_EDITOR
    static GameObject[] LoadVariantsEditor()
    {
        var cat = UnityEditor.AssetDatabase.LoadAssetAtPath<NuclearMissileCatalog>(
            "Assets/Resources/NuclearMissiles/Catalog.asset");
        if (cat != null && cat.variants != null && cat.variants.Length > 0)
            return cat.variants;

        int[] preferred = { 1, 3, 5, 8, 13, 21, 25, 29 };
        var picked = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < preferred.Length; i++)
        {
            string name = "RMB_" + preferred[i].ToString("00");
            string path = "Assets/BTM_Assets/BTM_Rockets_Missiles_Bombs/Prefabs/Blue/" + name + ".prefab";
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                picked.Add(prefab);
        }

        if (picked.Count > 0)
            return picked.ToArray();

        picked.Clear();
        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        string[] guids = UnityEditor.AssetDatabase.FindAssets("RMB_ t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.Contains("BTM_Rockets_Missiles_Bombs") || !path.Contains("/Blue/"))
                continue;

            string lowerName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!seen.Add(lowerName))
                continue;

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            picked.Add(prefab);
            if (picked.Count >= 8)
                break;
        }

        return picked.Count == 0 ? null : picked.ToArray();
    }
#endif
}
