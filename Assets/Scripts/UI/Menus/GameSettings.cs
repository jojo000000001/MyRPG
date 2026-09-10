using System;
using UnityEngine;

/// <summary>
/// 游戏设置（PlayerPrefs），当前仅主音量。
/// </summary>
public static class GameSettings
{
    private const string MasterVolumeKey = "Settings.MasterVolume";

    public const float DefaultMasterVolume = 1f;

    public static event Action Changed;

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        set
        {
            float clamped = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, clamped);
            Apply();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        Apply();
        Application.quitting -= Save;
        Application.quitting += Save;
    }

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
        Changed?.Invoke();
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
