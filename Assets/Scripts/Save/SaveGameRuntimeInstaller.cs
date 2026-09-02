using UnityEngine;

/// <summary>
/// 存档组件校验。SaveGameController 应配置在 Player.prefab 上。
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
