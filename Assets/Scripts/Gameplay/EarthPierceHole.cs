using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피어싱 레이저: 원통형으로 깔끔히 뚫림 + 가장자리 용암은 셰이더 처리.
/// </summary>
public class EarthPierceHole : MonoBehaviour
{
    public static EarthPierceHole Ensure(EarthPlanet earth)
    {
        if (earth == null)
            return null;
        var h = earth.GetComponent<EarthPierceHole>();
        if (h == null)
            h = earth.gameObject.AddComponent<EarthPierceHole>();
        h.earth = earth;
        h.BindMaterial();
        return h;
    }

    struct Hole
    {
        public Vector3 origin;
        public Vector3 axis;
        public float radius;
    }

    EarthPlanet earth;
    readonly List<Hole> holes = new List<Hole>();
    Material crustMat;
    const int MaxHoles = 4;

    void BindMaterial()
    {
        if (earth == null)
            earth = GetComponent<EarthPlanet>();
        var rend = GetComponent<Renderer>();
        if (rend != null)
            crustMat = rend.material;
    }

    public void AddPierce(Vector3 entryWorld, Vector3 exitWorld, float radiusWorld)
    {
        BindMaterial();
        CleanupOldYellowJunk();

        Vector3 center = earth.transform.position;
        Vector3 axis = (exitWorld - entryWorld).normalized;
        if (axis.sqrMagnitude < 1e-6f)
            axis = (entryWorld - center).normalized;

        while (holes.Count >= MaxHoles)
            holes.RemoveAt(0);

        // 레이저 굵기와 맞는 깔끔한 원통 구멍
        float r = Mathf.Clamp(radiusWorld, earth.Radius * 0.1f, earth.Radius * 0.28f);
        holes.Add(new Hole
        {
            origin = center,
            axis = axis,
            radius = r
        });
        PushToShader();

        // 표면 그을림만 (노란 메시 없음)
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(entryWorld, r / earth.Radius * 1.1f, 0.92f);
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(exitWorld, r / earth.Radius * 1.1f, 0.92f);

        var core = earth.transform.Find("Core");
        if (core != null)
            core.gameObject.SetActive(false);
    }

    void CleanupOldYellowJunk()
    {
        for (int i = earth.transform.childCount - 1; i >= 0; i--)
        {
            var ch = earth.transform.GetChild(i);
            string n = ch.name;
            if (n.StartsWith("PierceLava") || n == "LavaPit" || n == "LavaBit"
                || n == "PierceBeamCore" || n == "PierceBeamGlow" || n == "PierceBeamOuter")
                Object.Destroy(ch.gameObject);
        }
    }

    void PushToShader()
    {
        if (crustMat == null || !crustMat.HasProperty("_PierceCount"))
            BindMaterial();
        if (crustMat == null)
            return;

        crustMat.SetInt("_PierceCount", holes.Count);
        if (crustMat.HasProperty("_PierceEdge"))
            crustMat.SetFloat("_PierceEdge", earth.Radius * 0.08f);
        if (crustMat.HasProperty("_MoltenColor"))
            crustMat.SetColor("_MoltenColor", new Color(1f, 0.28f, 0.04f, 1f));

        for (int i = 0; i < MaxHoles; i++)
        {
            if (i < holes.Count)
            {
                crustMat.SetVector("_PierceOrigin" + i, holes[i].origin);
                crustMat.SetVector("_PierceAxis" + i, holes[i].axis);
                crustMat.SetFloat("_PierceRadius" + i, holes[i].radius);
            }
            else
            {
                crustMat.SetVector("_PierceOrigin" + i, Vector4.zero);
                crustMat.SetVector("_PierceAxis" + i, Vector4.zero);
                crustMat.SetFloat("_PierceRadius" + i, 0f);
            }
        }
    }

    void OnDestroy()
    {
        holes.Clear();
        if (crustMat != null && crustMat.HasProperty("_PierceCount"))
            crustMat.SetInt("_PierceCount", 0);
    }
}
