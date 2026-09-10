using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 读档总入口：主菜单继续、槽位读取、游戏内 F9。
/// 为了让世界单位按存档重建，读档会先加载目标场景，等生成完成后再套用数据。
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

    /// <summary>
    /// 玩法场景加载完后，如果带了读档/新游戏标记，就挂一个临时物体在下一帧处理。
    /// </summary>
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

    /// <summary>
    /// 若当前还不是「刚载入目标场景、准备套用」的状态，就先切场景；
    /// 切完后会再次进入这里，等营地刷怪后再 Apply。
    /// </summary>
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

            string targetScene = string.IsNullOrEmpty(data.sceneName) ? GameplaySceneName : data.sceneName;
            string activeScene = SceneManager.GetActiveScene().name;
            bool applyingAfterSceneLoad = SaveSession.PendingLoadSlot == slotIndex && activeScene == targetScene;
            if (!applyingAfterSceneLoad)
            {
                SaveSession.BeginLoad(slotIndex);
                SceneManager.LoadScene(targetScene);
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
            if (data.camera == null || !data.camera.hasLook)
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

    public static void SnapOpeningCameraToKnight()
    {
        ThirdPersonCameraRig rig = Object.FindObjectOfType<ThirdPersonCameraRig>();
        if (rig != null)
            rig.SnapLookAtKnight();
    }

    private static Player ResolvePlayer()
    {
        return Player.Resolve();
    }

    /// <summary>
    /// 场景加载后的一次性跑者：处理新游戏出生，或把待读槽位套进场景。
    /// </summary>
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

        /// <summary>新游戏：清空背包、放到出生点、镜头对准犬骑士。</summary>
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

            SaveSession.MarkPlayTime(0f);
            SnapOpeningCameraToKnight();

            AreaBgmZone[] zones = Object.FindObjectsOfType<AreaBgmZone>();
            for (int i = 0; i < zones.Length; i++)
                zones[i].UpdatePlayerPresence();

            if (zones.Length == 0)
                BgmManager.RefreshGameplayMusic();
        }
    }
}
