using UnityEngine;

/// <summary>
/// 挂在玩家身上：F5 快存、F9 读当前槽。真正的读写走 SaveSystem / SaveLoadService。
/// </summary>
[DisallowMultipleComponent]
public sealed class SaveGameController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemCatalog itemCatalog;

    [Header("Input")]
    [SerializeField] private KeyCode saveKey = KeyCode.F5;
    [SerializeField] private KeyCode loadKey = KeyCode.F9;
    [SerializeField] private bool autoLoadOnStart;

    private void Awake()
    {
        ResolveReferences();
        if (itemCatalog != null)
            ItemCatalog.RegisterRuntimeInstance(itemCatalog);
    }

    private void Start()
    {
        // 主菜单已经安排了读档或新游戏，这里不要再自动读一次。
        if (SaveSession.PendingNewGame)
            return;

        if (SaveSession.PendingLoadSlot.HasValue)
            return;

        if (autoLoadOnStart && SaveSession.HasValidActiveSlot && SaveSystem.HasSave(SaveSession.ActiveSlot))
            SaveLoadService.LoadSlotAsync(SaveSession.ActiveSlot, this);
    }

    private void Update()
    {
        if (Input.GetKeyDown(saveKey))
            Save();

        if (Input.GetKeyDown(loadKey) && SaveSession.HasValidActiveSlot)
            SaveLoadService.LoadSlotAsync(SaveSession.ActiveSlot, this);
    }

    /// <summary>把当前进度写入正在使用的槽位。</summary>
    public void Save()
    {
        if (!SaveSession.HasValidActiveSlot)
        {
            Debug.LogWarning("SaveGameController: No active save slot.");
            return;
        }

        ResolveReferences();
        if (player == null || inventory == null)
        {
            Debug.LogWarning("SaveGameController: Missing player or inventory.");
            return;
        }

        bool saved = SaveSystem.Save(SaveSession.ActiveSlot, player, inventory);
        if (saved)
            Debug.Log($"SaveGameController: Quick saved to slot {SaveSession.ActiveSlot + 1}.");
    }

    /// <summary>读取当前槽。会重载场景后再套用存档。</summary>
    public void Load()
    {
        if (!SaveSession.HasValidActiveSlot)
            return;

        SaveLoadService.LoadSlotAsync(SaveSession.ActiveSlot, this);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = Player.Resolve();
        }

        if (inventory == null && player != null)
            inventory = player.GetComponent<Inventory>();

        if (itemCatalog == null)
            itemCatalog = ItemCatalog.EnsureAvailable();
    }
}
