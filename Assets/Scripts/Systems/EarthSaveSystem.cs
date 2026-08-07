using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 지구 파괴 상태(지형·그을음·관통구·인구·카메라 등)를 persistentDataPath에 저장/복원한다.
/// </summary>
public static class EarthSaveSystem
{
    public const int SaveVersion = 1;

    const string MetaFile = "earth_save.json";
    const string ScorchFile = "earth_scorch.png";
    const string MeshFile = "earth_mesh.bin";

    static string SaveDir => Application.persistentDataPath;
    static string MetaPath => Path.Combine(SaveDir, MetaFile);
    static string ScorchPath => Path.Combine(SaveDir, ScorchFile);
    static string MeshPath => Path.Combine(SaveDir, MeshFile);

    [Serializable]
    public class PierceHoleEntry
    {
        public float ax, ay, az, radius;
    }

    [Serializable]
    public class EarthSaveFile
    {
        public int version = SaveVersion;
        public float heat;
        public float nuclearScorch;
        public int impactCount;
        public long population;
        public float earthRotX, earthRotY, earthRotZ, earthRotW;
        public float camYaw, camPitch, camDistance;
        public string simDate;
        public float dayAccumulator;
        public float simSpeed;
        public int sciencePoints;
        public int meshVertexCount;
        public bool hasScorch;
        public bool hasMesh;
        public PierceHoleEntry[] pierceHoles;
    }

    public static bool HasSave() => File.Exists(MetaPath);

    public static void DeleteSave()
    {
        TryDelete(MetaPath);
        TryDelete(ScorchPath);
        TryDelete(MeshPath);
    }

    static void TryDelete(string path)
    {
        if (!File.Exists(path))
            return;
        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EarthSave] Delete failed: {path} — {e.Message}");
        }
    }

    public static bool TrySave()
    {
        var earth = UnityEngine.Object.FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;

        try
        {
            if (!Directory.Exists(SaveDir))
                Directory.CreateDirectory(SaveDir);

            var meta = new EarthSaveFile
            {
                version = SaveVersion,
                heat = earth.Heat,
                nuclearScorch = earth.NuclearScorch,
                impactCount = earth.ImpactCount,
                population = PopulationSystem.Instance != null
                    ? PopulationSystem.Instance.Population
                    : PopulationSystem.BaselinePopulation
            };

            var rot = earth.transform.rotation;
            meta.earthRotX = rot.x;
            meta.earthRotY = rot.y;
            meta.earthRotZ = rot.z;
            meta.earthRotW = rot.w;

            var cam = UnityEngine.Object.FindObjectOfType<OrbitCamera>();
            if (cam != null)
            {
                cam.GetOrbitState(out meta.camYaw, out meta.camPitch, out meta.camDistance);
            }

            var hud = WorldStatusHud.Instance;
            if (hud != null)
            {
                hud.CaptureSimState(out meta.simDate, out meta.dayAccumulator, out meta.simSpeed, out meta.sciencePoints);
            }

            var deform = earth.GetComponent<EarthCraterDeform>();
            if (deform != null && deform.TryExportVertices(out var verts))
            {
                meta.hasMesh = true;
                meta.meshVertexCount = verts.Length;
                WriteMesh(MeshPath, verts);
            }

            var scorch = earth.GetComponent<EarthSurfaceScorch>();
            if (scorch != null && scorch.TryExportPng(out var png))
            {
                meta.hasScorch = true;
                File.WriteAllBytes(ScorchPath, png);
            }

            var pierce = earth.GetComponent<EarthPierceHole>();
            if (pierce != null)
            {
                var list = new System.Collections.Generic.List<EarthPierceHole.HoleSnapshot>();
                pierce.ExportSnapshots(list);
                if (list.Count > 0)
                {
                    meta.pierceHoles = new PierceHoleEntry[list.Count];
                    for (int i = 0; i < list.Count; i++)
                    {
                        var h = list[i];
                        meta.pierceHoles[i] = new PierceHoleEntry
                        {
                            ax = h.ax,
                            ay = h.ay,
                            az = h.az,
                            radius = h.radius
                        };
                    }
                }
            }

            File.WriteAllText(MetaPath, JsonUtility.ToJson(meta, prettyPrint: false));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EarthSave] Save failed: {e.Message}");
            return false;
        }
    }

    public static bool TryLoad()
    {
        if (!File.Exists(MetaPath))
            return false;

        var earth = UnityEngine.Object.FindObjectOfType<EarthPlanet>();
        if (earth == null)
            return false;

        try
        {
            var meta = JsonUtility.FromJson<EarthSaveFile>(File.ReadAllText(MetaPath));
            if (meta == null || meta.version != SaveVersion)
                return false;

            earth.transform.rotation = new Quaternion(
                meta.earthRotX, meta.earthRotY, meta.earthRotZ, meta.earthRotW);

            if (meta.hasMesh && File.Exists(MeshPath))
            {
                var deform = earth.GetComponent<EarthCraterDeform>();
                if (deform == null)
                    deform = earth.gameObject.AddComponent<EarthCraterDeform>();

                if (ReadMesh(MeshPath, meta.meshVertexCount, out var verts))
                    deform.TryImportVertices(verts);
            }

            if (meta.hasScorch && File.Exists(ScorchPath))
            {
                var scorch = earth.GetComponent<EarthSurfaceScorch>();
                if (scorch == null)
                    scorch = earth.gameObject.AddComponent<EarthSurfaceScorch>();

                scorch.TryImportPng(File.ReadAllBytes(ScorchPath));
            }

            if (meta.pierceHoles != null && meta.pierceHoles.Length > 0)
            {
                var pierce = earth.GetComponent<EarthPierceHole>();
                if (pierce == null)
                    pierce = earth.gameObject.AddComponent<EarthPierceHole>();

                var snaps = new System.Collections.Generic.List<EarthPierceHole.HoleSnapshot>();
                for (int i = 0; i < meta.pierceHoles.Length; i++)
                {
                    var e = meta.pierceHoles[i];
                    snaps.Add(new EarthPierceHole.HoleSnapshot
                    {
                        ax = e.ax,
                        ay = e.ay,
                        az = e.az,
                        radius = e.radius
                    });
                }
                pierce.RestoreSnapshots(snaps);
            }

            earth.ApplySavedState(meta.heat, meta.nuclearScorch, meta.impactCount);

            var pop = PopulationSystem.Instance;
            if (pop != null)
                pop.SetPopulation(meta.population);

            var hud = WorldStatusHud.Instance;
            if (hud != null)
                hud.ApplySimState(meta.simDate, meta.dayAccumulator, WorldStatusHud.DefaultSimSpeed, meta.sciencePoints);

            var cam = UnityEngine.Object.FindObjectOfType<OrbitCamera>();
            if (cam != null)
                cam.SetOrbitState(meta.camYaw, meta.camPitch, meta.camDistance);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EarthSave] Load failed: {e.Message}");
            return false;
        }
    }

    static void WriteMesh(string path, Vector3[] verts)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write(verts.Length);
        for (int i = 0; i < verts.Length; i++)
        {
            bw.Write(verts[i].x);
            bw.Write(verts[i].y);
            bw.Write(verts[i].z);
        }
    }

    static bool ReadMesh(string path, int expectedCount, out Vector3[] verts)
    {
        verts = null;
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);
        int count = br.ReadInt32();
        if (count != expectedCount || count <= 0)
            return false;

        verts = new Vector3[count];
        for (int i = 0; i < count; i++)
            verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        return true;
    }
}
