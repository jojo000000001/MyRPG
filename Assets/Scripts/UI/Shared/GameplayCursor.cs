using UnityEngine;

/// <summary>
/// 游戏中锁定并隐藏鼠标；打开 UI（背包等）时显示。
/// </summary>
public static class GameplayCursor
{
    public static void LockForGameplay()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void UnlockForUI()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
