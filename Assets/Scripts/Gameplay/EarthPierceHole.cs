using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피어싱 레이저 구멍: 셰이더 clip으로 반대쪽이 보이게.
/// (노란 용암 메시/림 없음)
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

        float r = Mathf.Max(radiusWorld, earth.Radius * 0.12f);
        holes.Add(new Hole
        {
            origin = center,
            axis = axis,
            radius = r
        });
        PushToShader();

        var deform = EarthCraterDeform.Ensure(earth);
        if (deform != null)
        {
            deform.DrillBore(entryWorld, 0.32f, 0.28f, 0.18f);
            deform.DrillBore(exitWorld, 0.32f, 0.28f, 0.18f);
        }
        // 검게 그을린 자국만 (노란 메시 없음)
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(entryWorld, 0.14f, 0.95f);
        EarthSurfaceScorch.Ensure(earth)?.BurnAt(exitWorld, 0.14f, 0.95f);

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
            if (n == "PierceLavaTunnel" || n == "PierceLavaRim" || n == "LavaPit" || n == "LavaBit")
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
