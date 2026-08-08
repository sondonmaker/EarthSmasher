using UnityEngine;

/// <summary>Planet Earth Free 프리팹을 게임용 EarthPlanet으로 구성.</summary>
public static class EarthPlanetFreeSetup
{
    const string PrefabResourcesPath = "PlanetEarthFree/EarthMedium";
    const string PrefabAssetPath = "Assets/Planet Earth Free/Prefabs/EarthMedium.prefab";

    public static bool TryCreate(out GameObject earthRoot, out Renderer crustRenderer, out Transform coreVisual)
    {
        earthRoot = null;
        crustRenderer = null;
        coreVisual = null;

        var prefab = LoadPrefab();
        if (prefab == null)
            return false;

        earthRoot = Object.Instantiate(prefab);
        earthRoot.name = "Earth";
        earthRoot.transform.position = Vector3.zero;
        earthRoot.transform.localScale = Vector3.one * 5f;

        RemoveSpinFree(earthRoot);

        Transform crustTransform = FindCrustTransform(earthRoot.transform);
        if (crustTransform == null)
        {
            Object.Destroy(earthRoot);
            earthRoot = null;
            return false;
        }

        var crustGo = crustTransform.gameObject;
        crustRenderer = crustGo.GetComponent<Renderer>();
        var meshFilter = crustGo.GetComponent<MeshFilter>();
        if (meshFilter != null)
            EarthMeshBuilder.Upgrade(meshFilter);

        if (earthRoot.GetComponent<SphereCollider>() == null)
            earthRoot.AddComponent<SphereCollider>();

        coreVisual = CreateCore(earthRoot.transform);

        var spin = earthRoot.GetComponent<EarthSpin>();
        if (spin == null)
            spin = earthRoot.AddComponent<EarthSpin>();
        spin.SetSpeed(7.5f);

        return crustRenderer != null;
    }

    static GameObject LoadPrefab()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcesPath);
        if (prefab != null)
            return prefab;

#if UNITY_EDITOR
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
#endif
        return prefab;
    }

    static Transform FindCrustTransform(Transform root)
    {
        var named = root.Find("Planet16128Tris");
        if (named != null)
            return named;

        named = root.Find("Planet3968Tris");
        if (named != null)
            return named;

        named = root.Find("Planet960Tris");
        if (named != null)
            return named;

        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] == null)
                continue;
            if (filters[i].name.Contains("Glow"))
                continue;
            return filters[i].transform;
        }

        return null;
    }

    static void RemoveSpinFree(GameObject root)
    {
        var spins = root.GetComponents<MonoBehaviour>();
        for (int i = 0; i < spins.Length; i++)
        {
            if (spins[i] == null)
                continue;
            if (spins[i].GetType().Name == "SpinFree")
                Object.Destroy(spins[i]);
        }
    }

    static Transform CreateCore(Transform parent)
    {
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(parent, false);
        core.transform.localScale = Vector3.one * 0.42f;
        Object.Destroy(core.GetComponent<Collider>());
        core.GetComponent<Renderer>().material = EarthTextureLoader.CreateCoreMaterial();
        core.SetActive(false);
        return core.transform;
    }
}
