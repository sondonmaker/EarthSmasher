using UnityEngine;

/// <summary>카메라를 향하는 밈 빌보드.</summary>
public class MemeBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
