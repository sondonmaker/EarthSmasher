using UnityEngine;

/// <summary>
/// 구버전 레이어 툴바. BlocksGameplayInput 플래그 호환용으로 유지.
/// UI는 EarthControlPanel 사용.
/// </summary>
public class EarthLayerToolbar : MonoBehaviour
{
    public static bool BlocksGameplayInput { get; set; }

    [SerializeField] EarthLayerController layers;

    public void Bind(EarthLayerController controller) => layers = controller;

    void OnGUI()
    {
        // 패널은 EarthControlPanel이 담당. 이 컴포넌트는 플래그 호환만.
    }
}
