using UnityEngine;
using UnityEngine.SceneManagement;

static class SaveGameRuntimeInstaller
{
    private const string GameplaySceneName = "SampleScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForGameplayScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != GameplaySceneName)
            return;

        EnsureSaveGameController();
    }

    public static void EnsureSaveGameController()
    {
        if (Object.FindObjectOfType<SaveGameController>() != null)
            return;

        Player player = Player.ActiveInstance != null
            ? Player.ActiveInstance
            : Object.FindObjectOfType<Player>();
        if (player == null)
            return;

        ItemCatalog.EnsureAvailable();
        player.gameObject.AddComponent<SaveGameController>();
    }
}
