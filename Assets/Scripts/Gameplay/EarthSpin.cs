using UnityEngine;

/// <summary>
/// 지구 자전. base × multiplier.
/// x1 ≈ 게임용 기본 속도 (실제보다 빠르게 표현).
/// </summary>
public class EarthSpin : MonoBehaviour
{
    [SerializeField] float baseDegreesPerSecond = 8f;
    [SerializeField] float multiplier = 1f;

    public float BaseDegreesPerSecond
    {
        get => baseDegreesPerSecond;
        set => baseDegreesPerSecond = value;
    }

    public float Multiplier
    {
        get => multiplier;
        set => multiplier = Mathf.Max(0f, value);
    }

    public void SetSpeed(float degPerSec)
    {
        baseDegreesPerSecond = degPerSec;
        multiplier = 1f;
    }

    public void SetMultiplier(float mul) => Multiplier = mul;

    void Update()
    {
        transform.Rotate(Vector3.up, baseDegreesPerSecond * multiplier * Time.deltaTime, Space.Self);
    }
}
