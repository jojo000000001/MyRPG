using UnityEngine;

/// <summary>
/// 打包后如果没有平台默认画质，Unity 会落到 Very Low。
/// SimpleNaturePack 的树在最后一档 LOD 之后会直接裁掉；Very Low 的 lodBias 是 0.3，
/// 远处树几乎全部消失。编辑器当前用的是 Ultra，所以只有打出来的包会中招。
/// </summary>
static class PlayQualityBootstrap
{
    private const int MinimumQualityLevel = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsurePlayableQuality()
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
            return;

        int current = QualitySettings.GetQualityLevel();
        int minimum = Mathf.Clamp(MinimumQualityLevel, 0, names.Length - 1);
        if (current < minimum)
            QualitySettings.SetQualityLevel(minimum, true);

        if (QualitySettings.lodBias < 1f)
            QualitySettings.lodBias = 1f;

        if (!Application.isEditor && QualitySettings.vSyncCount < 1)
            QualitySettings.vSyncCount = 1;
    }
}
