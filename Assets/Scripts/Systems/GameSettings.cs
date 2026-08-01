using UnityEngine;

/// <summary>
/// Tool 메뉴용 사운드/그래픽 설정 (PlayerPrefs 저장).
/// </summary>
public static class GameSettings
{
    const string MusicKey = "settings.music";
    const string EffectKey = "settings.effect";
    const string QualityKey = "settings.quality";

    static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
            ApplyAudio();
        }
    }

    public static float EffectVolume
    {
        get => PlayerPrefs.GetFloat(EffectKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(EffectKey, Mathf.Clamp01(value));
            ApplyAudio();
        }
    }

    public static int QualityIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 1), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        set
        {
            int i = Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            PlayerPrefs.SetInt(QualityKey, i);
            QualitySettings.SetQualityLevel(i, true);
        }
    }

    public static string QualityLabel
    {
        get
        {
            int i = QualityIndex;
            if (QualitySettings.names != null && i < QualitySettings.names.Length)
                return QualitySettings.names[i];
            if (i < QualityNames.Length) return QualityNames[i];
            return "Medium";
        }
    }

    public static void CycleQuality()
    {
        int count = QualitySettings.names != null ? QualitySettings.names.Length : QualityNames.Length;
        if (count <= 0) return;
        QualityIndex = (QualityIndex + 1) % count;
        PlayerPrefs.Save();
    }

    public static void ApplyAudio()
    {
        // 마스터는 음악/효과 평균에 가깝게 (효과음 시스템 전 임시)
        AudioListener.volume = Mathf.Clamp01(MusicVolume * 0.5f + EffectVolume * 0.5f);
    }

    public static void Load()
    {
        QualitySettings.SetQualityLevel(QualityIndex, true);
        ApplyAudio();
    }
}
