using System.Collections;
using UnityEngine;

/// <summary>
/// 부트스트랩 이후 저장본을 불러오고, 종료/백그라운드 시 자동 저장한다.
/// </summary>
public class EarthSaveRunner : MonoBehaviour
{
    bool ready;

    IEnumerator Start()
    {
        // MeteorImpactBootstrap·카메라 연결을 기다린 뒤 복원
        yield return null;
        yield return null;

        if (EarthSaveSystem.TryLoad())
        {
            Debug.Log("[EarthSave] Last session restored.");
            var earth = FindObjectOfType<EarthPlanet>();
            if (earth != null)
                PopulationDestructionSync.EnforceCap(earth, force: true);
        }

        WorldStatusHud.Instance?.ApplyDefaultSimSpeed();

        ready = true;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && ready)
            EarthSaveSystem.TrySave();
    }

    void OnApplicationQuit()
    {
        if (ready)
            EarthSaveSystem.TrySave();
    }

    void OnDestroy()
    {
        if (ready)
            EarthSaveSystem.TrySave();
    }
}
