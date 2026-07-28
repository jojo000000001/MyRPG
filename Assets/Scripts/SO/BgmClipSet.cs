using UnityEngine;

/// <summary>
/// BGM 音频资源配置。
/// </summary>
[CreateAssetMenu(fileName = "BgmClipSet", menuName = "MyRPG/Bgm Clip Set")]
public sealed class BgmClipSet : ScriptableObject
{
    [Header("Menu")]
    public AudioClip title;

    [Header("Exploration")]
    public AudioClip town;
    public AudioClip forest;

    [Header("Combat")]
    public AudioClip battle1;
    public AudioClip battle2;
}
