using System;
using UnityEngine;

/// <summary>
/// 玩家屏幕 HUD 总控：统一解析、绑定并开关所有会出现的 HUD UI。
/// 世界空间 UI（怪物血条、伤害飘字）不归此类。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Canvas))]
public sealed class PlayerHUD : MonoBehaviour
{
    public static PlayerHUD Instance { get; private set; }

    [Header("Target")]
    [SerializeField] private Player player;

    [Header("Persistent HUD")]
    [SerializeField] private PlayerHealthBar healthBar;
    [SerializeField] private PlayerExperienceBar experienceBar;
    [SerializeField] private QuestTrackerUI questTracker;
    [SerializeField] private PlayerHotbar hotbar;
    [SerializeField] private PlayerRupeeHud rupeeHud;
    [SerializeField] private PlayerMouseHint mouseHint;

    [Header("Overlays")]
    [SerializeField] private LevelUpNotice levelUpNotice;
    [SerializeField] private InventoryUIRuntimeSpawner inventorySpawner;
    [SerializeField] private PlayerStatPanel statPanel;
    [SerializeField] private GameplayPauseMenu pauseMenu;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private ShopUI shopUI;
    [SerializeField] private ShopUIRuntimeSpawner shopSpawner;

    private Canvas canvas;

    public Player BoundPlayer => player;
    public PlayerHealthBar HealthBar => healthBar;
    public PlayerExperienceBar ExperienceBar => experienceBar;
    public QuestTrackerUI QuestTracker => questTracker;
    public PlayerHotbar Hotbar => hotbar;
    public PlayerRupeeHud RupeeHud => rupeeHud;
    public PlayerMouseHint MouseHint => mouseHint;
    public LevelUpNotice LevelUp => levelUpNotice;
    public InventoryUIRuntimeSpawner InventorySpawner => inventorySpawner;
    public PlayerStatPanel StatPanel => statPanel;
    public GameplayPauseMenu PauseMenu => pauseMenu;
    public DialogueUI Dialogue => dialogueUI;
    public ShopUI Shop => shopUI;

    public InventoryUI InventoryUi
    {
        get
        {
            if (inventorySpawner != null && inventorySpawner.RuntimeInstance != null)
                return inventorySpawner.RuntimeInstance;

            return GetComponentInChildren<InventoryUI>(true);
        }
    }

    public bool IsInventoryOpen => InventoryUi != null && InventoryUi.IsOpen;
    public bool IsPauseOpen => GameplayPauseMenu.IsOpen;
    public bool IsDialogueOpen => DialogueUI.IsOpen;
    public bool IsShopOpen => ShopUI.IsOpen;
    public bool IsVisible => canvas != null ? canvas.enabled && gameObject.activeInHierarchy : gameObject.activeInHierarchy;

    /// <summary>
    /// 返回场景里的 HUD 总控。Awake 尚未执行时会做一次场景查找。
    /// </summary>
    public static PlayerHUD Resolve()
    {
        if (Instance != null)
            return Instance;

        PlayerHUD found = FindObjectOfType<PlayerHUD>();
        if (found != null && Application.isPlaying)
            Instance = found;

        return found;
    }

    /// <summary>
    /// 优先使用场景里已有的 PlayerHUD；若只有同名 Canvas 则补上组件。
    /// </summary>
    public static PlayerHUD Ensure()
    {
        PlayerHUD hud = Resolve();
        if (hud != null)
            return hud;

        GameObject existing = GameObject.Find("PlayerHUD");
        if (existing != null)
        {
            hud = existing.GetComponent<PlayerHUD>();
            if (hud == null)
                hud = existing.AddComponent<PlayerHUD>();
            return hud;
        }

        Debug.LogWarning("PlayerHUD: 场景缺少 PlayerHUD，请把 Prefabs/UI/PlayerHUD 放进玩法场景。");
        return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PlayerHUD: duplicate instance, keeping the first one.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        canvas = GetComponent<Canvas>();
        CacheModules();
        EnsureOverlayHosts();
        BindPlayer(ResolvePlayer());
    }

    private void Start()
    {
        if (player == null)
            BindPlayer(ResolvePlayer());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void BindPlayer(Player target)
    {
        player = target;
        if (player == null)
            return;

        healthBar?.BindPlayer(player);
        experienceBar?.BindPlayer(player);
        levelUpNotice?.BindPlayer(player);
        statPanel?.BindPlayer(player);
        inventorySpawner?.BindPlayer(player);
        pauseMenu?.BindPlayer(player);
        hotbar?.BindPlayer(player);
        rupeeHud?.BindPlayer(player);
    }

    public void SetVisible(bool visible)
    {
        if (canvas != null)
            canvas.enabled = visible;
        else
            gameObject.SetActive(visible);
    }

    public void SetChromeVisible(bool visible)
    {
        SetChildActive("PlayerHealthBarRoot", visible);
        SetChildActive("PlayerExperienceBarRoot", visible);
        SetChildActive("PlayerHotbarRoot", visible);
        SetChildActive("PlayerRupeeRoot", visible);
        SetChildActive(PlayerMouseHint.RootName, visible);
        if (questTracker != null)
            questTracker.gameObject.SetActive(visible);
    }

    public void SetInventoryOpen(bool open)
    {
        InventoryUI inventoryUi = InventoryUi;
        if (inventoryUi != null)
            inventoryUi.SetOpen(open);
    }

    public void ShowQuest(QuestDefinition definition, int current, int required)
    {
        EnsureQuestTracker().ShowQuest(definition, current, required);
    }

    public void ShowQuestProgress(QuestDefinition definition, int current, int required, bool completed)
    {
        EnsureQuestTracker().ShowProgress(definition, current, required, completed);
    }

    public void HideQuest()
    {
        if (questTracker != null)
            questTracker.Hide();
    }

    public void ShowDialogue(string speaker, string[] lines, Action completed = null)
    {
        EnsureDialogue().Show(speaker, lines, completed);
    }

    public void HideDialogue()
    {
        if (dialogueUI != null)
            dialogueUI.Hide();
    }

    public QuestTrackerUI EnsureQuestTracker()
    {
        if (questTracker == null)
            questTracker = GetComponentInChildren<QuestTrackerUI>(true);

        if (questTracker == null)
            questTracker = CreateHost<QuestTrackerUI>("QuestTrackerUI");

        return questTracker;
    }

    public DialogueUI EnsureDialogue()
    {
        if (dialogueUI == null)
            dialogueUI = GetComponentInChildren<DialogueUI>(true);

        if (dialogueUI == null)
            dialogueUI = CreateHost<DialogueUI>("DialogueUI");

        return dialogueUI;
    }

    public ShopUI EnsureShop()
    {
        if (shopUI == null)
            shopUI = GetComponentInChildren<ShopUI>(true);

        if (shopUI == null)
        {
            if (shopSpawner == null)
                shopSpawner = GetComponent<ShopUIRuntimeSpawner>();
            if (shopSpawner != null)
                shopUI = shopSpawner.Spawn();
        }

        if (shopUI == null)
            Debug.LogWarning("PlayerHUD: 缺少商店预制体 Prefabs/UI/ShopUIRoot。", this);

        return shopUI;
    }

    private void CacheModules()
    {
        if (healthBar == null)
            healthBar = GetComponent<PlayerHealthBar>();
        if (experienceBar == null)
            experienceBar = GetComponent<PlayerExperienceBar>();
        if (levelUpNotice == null)
            levelUpNotice = GetComponent<LevelUpNotice>();
        if (inventorySpawner == null)
            inventorySpawner = GetComponent<InventoryUIRuntimeSpawner>();
        if (statPanel == null)
            statPanel = GetComponent<PlayerStatPanel>();
        if (hotbar == null)
            hotbar = GetComponent<PlayerHotbar>();
        if (hotbar == null)
            hotbar = gameObject.AddComponent<PlayerHotbar>();
        if (rupeeHud == null)
            rupeeHud = GetComponent<PlayerRupeeHud>();
        if (rupeeHud == null)
            rupeeHud = gameObject.AddComponent<PlayerRupeeHud>();
        if (mouseHint == null)
            mouseHint = GetComponent<PlayerMouseHint>();
        if (mouseHint == null)
            mouseHint = gameObject.AddComponent<PlayerMouseHint>();
        if (pauseMenu == null)
            pauseMenu = GetComponentInChildren<GameplayPauseMenu>(true);
        if (questTracker == null)
            questTracker = GetComponentInChildren<QuestTrackerUI>(true);
        if (dialogueUI == null)
            dialogueUI = GetComponentInChildren<DialogueUI>(true);
        if (shopSpawner == null)
            shopSpawner = GetComponent<ShopUIRuntimeSpawner>();
        if (shopUI == null)
            shopUI = GetComponentInChildren<ShopUI>(true);
    }

    private void EnsureOverlayHosts()
    {
        EnsureQuestTracker();
        EnsureDialogue();
        if (hotbar == null)
            hotbar = GetComponent<PlayerHotbar>();
        if (hotbar == null)
            hotbar = gameObject.AddComponent<PlayerHotbar>();
        if (rupeeHud == null)
            rupeeHud = GetComponent<PlayerRupeeHud>();
        if (rupeeHud == null)
            rupeeHud = gameObject.AddComponent<PlayerRupeeHud>();
        if (mouseHint == null)
            mouseHint = GetComponent<PlayerMouseHint>();
        if (mouseHint == null)
            mouseHint = gameObject.AddComponent<PlayerMouseHint>();
        mouseHint.EnsureUi();
    }

    private Player ResolvePlayer()
    {
        if (player == null)
            player = Player.Resolve();

        return player;
    }

    private T CreateHost<T>(string hostName) where T : Component
    {
        Transform existing = transform.Find(hostName);
        GameObject host = existing != null ? existing.gameObject : new GameObject(hostName, typeof(RectTransform));
        if (existing == null)
            host.transform.SetParent(transform, false);

        Stretch(host.GetComponent<RectTransform>());

        T module = host.GetComponent<T>();
        if (module == null)
            module = host.AddComponent<T>();

        return module;
    }

    private void SetChildActive(string childName, bool visible)
    {
        Transform child = transform.Find(childName);
        if (child != null)
            child.gameObject.SetActive(visible);
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
