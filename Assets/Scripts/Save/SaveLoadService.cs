using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central save-load entry point for menu continue, slot load, and in-game reload.
/// </summary>
public static class SaveLoadService
{
    private const string GameplaySceneName = "SampleScene";

    private static bool isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoaded()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != GameplaySceneName)
            return;

        if (!SaveSession.PendingLoadSlot.HasValue && !SaveSession.PendingNewGame)
            return;

        var runner = new GameObject(nameof(SaveLoadService));
        runner.AddComponent<SceneLoadRunner>();
    }

    public static void LoadSlotAsync(int slotIndex, MonoBehaviour host)
    {
        if (host == null)
            return;

        host.StartCoroutine(LoadSlot(slotIndex));
    }

    public static IEnumerator LoadSlot(int slotIndex)
    {
        if (isLoading)
            yield break;

        isLoading = true;

        try
        {
            if (!SaveSystem.TryRead(slotIndex, out SaveData data))
            {
                Debug.LogWarning($"SaveLoadService: Failed to read slot {slotIndex + 1}.");
                yield break;
            }

            string activeScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(data.sceneName) && data.sceneName != activeScene)
            {
                SaveSession.BeginLoad(slotIndex);
                SceneManager.LoadScene(data.sceneName);
                yield break;
            }

            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            SaveGameRuntimeInstaller.EnsureSaveGameController();

            Player player = ResolvePlayer();
            Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
            ItemCatalog itemCatalog = ItemCatalog.EnsureAvailable();

            if (player == null || inventory == null || itemCatalog == null)
            {
                Debug.LogWarning("SaveLoadService: Missing player, inventory, or item catalog.");
                yield break;
            }

            if (!SaveSystem.Apply(data, player, inventory, itemCatalog))
            {
                Debug.LogWarning($"SaveLoadService: Apply failed for slot {slotIndex + 1}.");
                yield break;
            }

            SavePositionApplier.Schedule(player, data);
            SnapCameraToPlayer();

            Debug.Log(
                $"SaveLoadService: Loaded slot {slotIndex + 1} at ({data.posX:F2}, {data.posY:F2}, {data.posZ:F2}). " +
                $"Player now at {player.transform.position}.");
        }
        finally
        {
            isLoading = false;
        }
    }

    public static void SnapCameraToPlayer()
    {
        ThirdPersonCameraRig rig = Object.FindObjectOfType<ThirdPersonCameraRig>();
        if (rig != null)
            rig.SnapToTarget();
    }

    private static Player ResolvePlayer()
    {
        return Player.Resolve();
    }

    private sealed class SceneLoadRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            if (SaveSession.PendingNewGame)
            {
                yield return ApplyNewGame();
                SaveSession.ClearPending();
                Destroy(gameObject);
                yield break;
            }

            if (!SaveSession.PendingLoadSlot.HasValue)
            {
                Destroy(gameObject);
                yield break;
            }

            int slotIndex = SaveSession.PendingLoadSlot.Value;
            yield return LoadSlot(slotIndex);

            if (SaveSession.PendingLoadSlot == slotIndex)
                SaveSession.ClearPending();

            Destroy(gameObject);
        }

        private static IEnumerator ApplyNewGame()
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            SaveGameRuntimeInstaller.EnsureSaveGameController();

            Player player = ResolvePlayer();
            Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
            GameSpawnPoint spawnPoint = GameSpawnPoint.FindInScene();

            if (player != null && inventory != null)
            {
                inventory.Clear();

                if (spawnPoint != null)
                    spawnPoint.ApplyTo(player);
            }

            SnapCameraToPlayer();

            AreaBgmZone[] zones = Object.FindObjectsOfType<AreaBgmZone>();
            for (int i = 0; i < zones.Length; i++)
                zones[i].UpdatePlayerPresence();

            if (zones.Length == 0)
                BgmManager.RefreshGameplayMusic();
        }
    }
}
