using UnityEngine;

/// <summary>
/// 카메라 뒤 간단한 별밭.
/// </summary>
public class StarfieldBackdrop : MonoBehaviour
{
    [SerializeField] int starCount = 220;
    [SerializeField] float radius = 80f;

    void Start()
    {
        var root = new GameObject("Stars").transform;
        root.SetParent(transform, false);

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = Color.white;

        for (int i = 0; i < starCount; i++)
        {
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "Star";
            star.transform.SetParent(root, false);
            star.transform.position = Random.onUnitSphere * radius;
            float s = Random.Range(0.03f, 0.12f);
            star.transform.localScale = Vector3.one * s;
            Destroy(star.GetComponent<Collider>());
            var rend = star.GetComponent<Renderer>();
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }
}
