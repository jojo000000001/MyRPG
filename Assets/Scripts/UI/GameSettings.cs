using UnityEngine;

/// <summary>
/// 游戏设置（PlayerPrefs），当前仅主音量。
/// </summary>
public static class GameSettings
{
    private const string MasterVolumeKey = "Settings.MasterVolume";

    public const float DefaultMasterVolume = 1f;

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        set
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            Apply();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
    }
}
