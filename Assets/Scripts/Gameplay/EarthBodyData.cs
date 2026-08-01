using UnityEngine;

/// <summary>
/// 실제 지구 물리 상수 (표시용) + 게임 내 조절 상태.
/// </summary>
public class EarthBodyData : MonoBehaviour
{
    // —— 실제 지구 기준값 (표시) ——
    public const double MassKg = 5.972e24;
    public const double CoreTempKelvin = 5650.0;      // 내핵 대략
    public const double MeanRadiusKm = 6371.0;
    public const double MeanDensityKgM3 = 5514.0;
    public const double SurfaceGravityMs2 = 9.80665;
    public const double EscapeVelocityKmS = 11.186;
    public const double OrbitalPeriodDays = 365.256;

    // 실제 자전: 1 sidereal day = 86164.0905 s → 15.041°/h
    public const double SiderealDayHours = 23.9344696;

    [SerializeField] float rotationMultiplier = 1f;
    [SerializeField] bool rotationEnabled = true;

    public bool RotationEnabled
    {
        get => rotationEnabled;
        set
        {
            rotationEnabled = value;
            ApplyRotationToSpins();
        }
    }

    public float RotationMultiplier
    {
        get => rotationMultiplier;
        set
        {
            rotationMultiplier = value;
            ApplyRotationToSpins();
        }
    }

    public void ApplyRotationToSpins()
    {
        float mul = rotationEnabled ? rotationMultiplier : 0f;
        var spins = GetComponentsInChildren<EarthSpin>(true);
        for (int i = 0; i < spins.Length; i++)
            spins[i].SetMultiplier(mul);
    }

    void Start() => ApplyRotationToSpins();
}
