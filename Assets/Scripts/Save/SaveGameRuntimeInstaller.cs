using UnityEngine;

/// <summary>
/// 读档前检查玩家身上是否有 SaveGameController。组件应配在 Player.prefab 上，这里不会运行时补挂。
/// </summary>
static class SaveGameRuntimeInstaller
{
    public static void EnsureSaveGameController()
    {
        if (Object.FindObjectOfType<SaveGameController>() != null)
            return;

        Player player = Player.Resolve();
        if (player == null)
            return;

        SaveGameController controller = player.GetComponent<SaveGameController>();
        if (controller != null)
            return;

        ItemCatalog.EnsureAvailable();
        Debug.LogError("SaveGameController missing on Player prefab. Run Tools/MyRPG/Setup Player Gameplay Components.", player);
    }
}
