using UnityEngine;

/// <summary>
/// 当前选中的存档槽，以及主菜单发出、等场景加载完再处理的读档/新游戏请求。
/// </summary>
public static class SaveSession
{
    /// <summary>无效槽位。尚未从主菜单选定存档时使用。</summary>
    public const int InvalidSlot = -1;

    /// <summary>当前正在游玩的槽位（0..SlotCount-1）。</summary>
    public static int ActiveSlot { get; private set; } = InvalidSlot;

    public static bool HasValidActiveSlot => ActiveSlot >= 0 && ActiveSlot < SaveSystem.SlotCount;

    /// <summary>场景加载完成后要套用的槽位。为空表示这次进场景不是读档。</summary>
    public static int? PendingLoadSlot { get; private set; }

    /// <summary>场景加载完成后按新游戏处理：清背包、送到出生点。</summary>
    public static bool PendingNewGame { get; private set; }

    /// <summary>读档时写入的累计时长，加上本局经过的时间就是当前游戏时长。</summary>
    public static float PlayTimeBaseSeconds { get; private set; }

    /// <summary>开始累计本局时长时的 unscaledTime。</summary>
    public static float PlayTimeAnchorUnscaled { get; private set; }

    /// <summary>当前累计游戏时长（秒）。</summary>
    public static float CurrentPlayTimeSeconds =>
        PlayTimeBaseSeconds + Mathf.Max(0f, Time.unscaledTime - PlayTimeAnchorUnscaled);

    /// <summary>
    /// 主菜单或 F9 决定读某个槽：记下槽位，等目标场景加载后再套用存档。
    /// </summary>
    public static void BeginLoad(int slotIndex)
    {
        ActiveSlot = slotIndex;
        PendingLoadSlot = slotIndex;
        PendingNewGame = false;
    }

    /// <summary>
    /// 在指定槽开始新游戏，并清掉该槽旧文件之外的待处理读档。
    /// </summary>
    public static void BeginNewGame(int slotIndex)
    {
        ActiveSlot = slotIndex;
        PendingLoadSlot = null;
        PendingNewGame = true;
        MarkPlayTime(0f);
    }

    /// <summary>
    /// 把累计时长锚到现在。读档传入存档里的秒数，新游戏传 0。
    /// </summary>
    public static void MarkPlayTime(float baseSeconds)
    {
        PlayTimeBaseSeconds = Mathf.Max(0f, baseSeconds);
        PlayTimeAnchorUnscaled = Time.unscaledTime;
    }

    /// <summary>
    /// 场景加载流程结束后清掉待处理标记，避免下次进场景再套一次。
    /// </summary>
    public static void ClearPending()
    {
        PendingLoadSlot = null;
        PendingNewGame = false;
    }
}
