#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>BTM Rockets Missiles Bombs(#73141) → NuclearMissile catalog.</summary>
public static class NuclearMissilePackSetup
{
    const string CatalogAssetPath = "Assets/Resources/NuclearMissiles/Catalog.asset";
    const int MaxVariants = 8;

    // 길쭉한 로켓/미사일 위주 (폭탄형 RMB 제외)
    static readonly int[] PreferredRmbIndices = { 1, 3, 5, 8, 13, 21, 25, 29, 10, 17, 20, 32 };

    [MenuItem("EarthSmasher/Nuclear Missiles/Link Cartoon Missile Pack")]
    public static void LinkCartoonMissilePack()
    {
        FleetAssetBootstrap.LinkAllImportedAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<NuclearMissileCatalog>(CatalogAssetPath);
    }

    public static GameObject[] FindCartoonMissilePrefabs()
    {
        var picked = new List<GameObject>();

        for (int i = 0; i < PreferredRmbIndices.Length && picked.Count < MaxVariants; i++)
        {
            string prefabName = "RMB_" + PreferredRmbIndices[i].ToString("00");
            string path = "Assets/BTM_Assets/BTM_Rockets_Missiles_Bombs/Prefabs/Blue/" + prefabName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
                for (int g = 0; g < guids.Length; g++)
                {
                    string found = AssetDatabase.GUIDToAssetPath(guids[g]);
                    if (!found.Contains("BTM_Rockets_Missiles_Bombs"))
                        continue;
                    if (!found.Contains("/Blue/"))
                        continue;
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(found);
                    if (prefab != null)
                        break;
                }
            }

            if (prefab == null || !LooksLikeProjectile(prefab))
                continue;

            picked.Add(prefab);
        }

        if (picked.Count == 0)
            picked.AddRange(FallbackScan());

        if (picked.Count == 0)
            return Array.Empty<GameObject>();

        return picked.ToArray();
    }

    static IEnumerable<GameObject> FallbackScan()
    {
        var picked = new List<GameObject>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("RMB_ t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.Contains("BTM_Rockets_Missiles_Bombs") || !path.Contains("/Blue/"))
                continue;

            string name = Path.GetFileNameWithoutExtension(path);
            if (!seen.Add(name))
                continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !LooksLikeProjectile(prefab))
                continue;

            picked.Add(prefab);
            if (picked.Count >= MaxVariants)
                break;
        }

        return picked;
    }

    static bool LooksLikeProjectile(GameObject prefab)
    {
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return true;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 size = bounds.size;
        float longest = Mathf.Max(size.x, size.y, size.z);
        float shortest = Mathf.Min(size.x, size.y, size.z);
        if (shortest < 1e-4f)
            return true;
        return longest / shortest >= 1.25f;
    }

    public static void EnsureResourcesFolder()
    {
        EnsureFolder("Assets/Resources/NuclearMissiles");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
