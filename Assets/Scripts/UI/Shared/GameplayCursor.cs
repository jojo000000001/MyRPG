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

    /// <summary>
    /// 背包、商店等弹窗打开时，镜头不应再跟鼠标转。
    /// </summary>
    public static bool BlocksWorldLook
    {
        get
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return true;
            if (GameplayPauseMenu.IsOpen || DialogueUI.IsOpen || ShopUI.IsOpen)
                return true;

            PlayerHUD hud = PlayerHUD.Instance;
            return hud != null && hud.IsInventoryOpen;
        }
    }
}
