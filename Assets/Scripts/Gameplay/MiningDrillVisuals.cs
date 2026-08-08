using System.Collections.Generic;
using UnityEngine;

/// <summary>Soviet 드릴 — 표면에 수직(+Y=밖), 거대 스케일.</summary>
public static class MiningDrillVisuals
{
    const string CatalogPath = "Fleet/Catalog";
    const string ResourcesPrefabPath = "Fleet/SovietDrill";
    const string EditorPrefabPath =
        "Assets/EvSeStudio/3D_Art/Tools/Drills_Vol_01/Drill_01/Prefabs/SM_Drill_01.prefab";
    const string EditorResourcesPrefabPath = "Assets/Resources/Fleet/SovietDrill.prefab";

    public struct RigVisual
    {
        public Transform root;
        public Transform[] spinParts;
    }

    public static RigVisual TryBuild(Transform parent, float earthRadius)
    {
        var template = LoadTemplate();
        if (template == null)
            return default;

        var visual = Object.Instantiate(template, parent, false);
        visual.name = "SovietDrill";
        PrepareInstance(visual);

        FitGiantDrill(visual.transform, earthRadius);

        return new RigVisual
        {
            root = visual.transform,
            spinParts = CollectSpinParts(visual.transform)
        };
    }

    public static GameObject LoadTemplate()
    {
        var catalog = Resources.Load<FleetVisualCatalog>(CatalogPath);
        if (catalog != null && catalog.miningDrill != null)
            return catalog.miningDrill;

        var fromResources = Resources.Load<GameObject>(ResourcesPrefabPath);
        if (fromResources != null)
            return fromResources;

#if UNITY_EDITOR
        var editorResources = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(EditorResourcesPrefabPath);
        if (editorResources != null)
            return editorResources;

        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefabPath);
#else
        return null;
#endif
    }

    static void PrepareInstance(GameObject root)
    {
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            Object.Destroy(rb);
        }

        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            Object.Destroy(col);

        FixMaterials(root);
    }

    static void FixMaterials(GameObject root)
    {
        foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
        {
            rend.enabled = true;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var src = rend.sharedMaterial;
            if (src == null || src.shader == null || !src.shader.isSupported
                || src.shader.name.Contains("Universal") || src.shader.name.Contains("HDRP"))
            {
                Texture albedo = ExtractAlbedo(src);
                Color tint = src != null && src.HasProperty("_BaseColor")
                    ? src.GetColor("_BaseColor")
                    : new Color(0.72f, 0.24f, 0.12f);

                rend.material = albedo != null
                    ? RuntimeMaterial.TexturedOpaque(albedo, tint)
                    : RuntimeMaterial.Opaque(tint, 0.08f);
            }
        }
    }

    static Texture ExtractAlbedo(Material src)
    {
        if (src == null)
            return null;
        if (src.HasProperty("_BaseMap"))
        {
            var tex = src.GetTexture("_BaseMap");
            if (tex != null)
                return tex;
        }
        if (src.HasProperty("_MainTex"))
            return src.GetTexture("_MainTex");
        return src.mainTexture;
    }

    static Transform[] CollectSpinParts(Transform root)
    {
        var parts = new List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root)
                continue;
            string n = t.name;
            if (n.Contains("Drillbit") || n.Contains("Spindel"))
                parts.Add(t);
        }

        if (parts.Count == 0)
            parts.Add(root);
        return parts.ToArray();
    }

    static void FitGiantDrill(Transform visual, float earthRadius)
    {
        // 1) 단위 크기로 정규화
        var world = ComputeWorldBounds(visual);
        float longest = Mathf.Max(world.size.x, Mathf.Max(world.size.y, world.size.z));
        if (longest < 1e-4f)
            longest = 0.15f;
        visual.localScale = Vector3.one / longest;

        OrientBitDownBodyUp(visual);

        // 2) 거대 드릴 — 몸통 높이 = 지구 반경의 45%
        var local = ComputeLocalBounds(visual);
        float bodyLen = Mathf.Max(local.size.y, local.size.magnitude * 0.35f);
        if (bodyLen < 1e-4f)
            bodyLen = 1f;

        float targetBody = earthRadius * 0.45f;
        visual.localScale *= targetBody / bodyLen;

        OrientBitDownBodyUp(visual);
    }

    /// <summary>비트가 -Y(지구 안), 몸통이 +Y(밖). 비트 끝이 y=0.</summary>
    static void OrientBitDownBodyUp(Transform visual)
    {
        var b = ComputeLocalBounds(visual);
        Vector3 size = b.size;

        Vector3 axis = Vector3.up;
        if (size.z >= size.x && size.z >= size.y)
            axis = Vector3.forward;
        else if (size.x >= size.y && size.x >= size.z)
            axis = Vector3.right;

        float minAlong = axis.y != 0f ? b.min.y : axis.x != 0f ? b.min.x : b.min.z;
        float maxAlong = axis.y != 0f ? b.max.y : axis.x != 0f ? b.max.x : b.max.z;
        Vector3 bitEnd = axis * minAlong;
        if (Mathf.Abs(maxAlong) > Mathf.Abs(minAlong))
            bitEnd = axis * maxAlong;

        Vector3 drillDir = (bitEnd - b.center).normalized;
        if (drillDir.sqrMagnitude < 1e-6f)
            drillDir = -axis;

        visual.localRotation = Quaternion.FromToRotation(drillDir, Vector3.down);
        b = ComputeLocalBounds(visual);
        visual.localPosition = new Vector3(0f, -b.min.y, 0f);
    }

    static Bounds ComputeWorldBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.position, Vector3.one * 0.15f);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static Bounds ComputeLocalBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one * 0.15f);

        bool has = false;
        Bounds local = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            var rend = renderers[i];
            if (rend == null)
                continue;

            var wb = rend.bounds;
            Vector3 c = root.InverseTransformPoint(wb.center);
            var lb = new Bounds(c, wb.size);
            if (!has)
            {
                local = lb;
                has = true;
            }
            else
            {
                local.Encapsulate(lb.min);
                local.Encapsulate(lb.max);
            }
        }

        return has ? local : new Bounds(Vector3.zero, Vector3.one * 0.15f);
    }
}
