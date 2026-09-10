using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 ESC 暂停菜单：继续、保存、设置、返回主菜单。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayPauseMenu : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Inventory inventory;

    private GameObject root;
    private GameObject menuCard;
    private TextMeshProUGUI statusText;
    private MainMenuSettingsPanel settingsPanel;
    private GameplaySaveSlotPanel saveSlotPanel;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private static int escapeConsumedFrame = -1;

    public static bool IsOpen { get; private set; }

    /// <summary>
    /// 同一帧内只处理一次 ESC，避免关闭背包/商店后再打开暂停菜单。
    /// </summary>
    public static bool TryConsumeEscape()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return false;

        int frame = Time.frameCount;
        if (escapeConsumedFrame == frame)
            return false;

        escapeConsumedFrame = frame;
        return true;
    }

    public static GameplayPauseMenu EnsureForHud(Transform hudRoot)
    {
        if (hudRoot == null)
            return null;

        GameplayPauseMenu existing = hudRoot.GetComponentInChildren<GameplayPauseMenu>(true);
        if (existing != null)
            return existing;

        GameObject host = new GameObject("GameplayPauseMenu", typeof(RectTransform), typeof(GameplayPauseMenu));
        host.transform.SetParent(hudRoot, false);
        SheikahUiStyle.Stretch(host.GetComponent<RectTransform>());
        return host.GetComponent<GameplayPauseMenu>();
    }

    private void Awake()
    {
        if (root == null)
            BuildUi();
    }

    private void Start()
    {
        ResolveReferences();
        settingsPanel = MainMenuSettingsPanel.Ensure(transform);
        saveSlotPanel = GameplaySaveSlotPanel.Ensure(transform);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(toggleKey))
            return;

        if (saveSlotPanel != null && saveSlotPanel.IsVisible)
        {
            CloseSaveSlots();
            return;
        }

        if (settingsPanel != null && settingsPanel.IsVisible)
        {
            settingsPanel.Hide();
            return;
        }

        if (DialogueUI.IsOpen)
            return;

        if (ShopUI.IsOpen)
        {
            ShopUI.Instance.Close();
            TryConsumeEscape();
            return;
        }

        if (TryCloseInventory())
        {
            TryConsumeEscape();
            return;
        }

        if (toggleKey == KeyCode.Escape && !TryConsumeEscape())
            return;

        if (DemoGameManager.Instance != null && !DemoGameManager.Instance.IsPlaying)
            return;

        if (isOpen)
            Resume();
        else
            Pause();
    }

    private void OnDestroy()
    {
        if (isOpen)
            ForceResume();
    }

    private void Pause()
    {
        if (isOpen)
            return;

        CloseInventoryIfOpen();
        ResolveReferences();

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isOpen = true;
        IsOpen = true;

        if (statusText != null)
            statusText.text = string.Empty;

        if (menuCard != null)
            menuCard.SetActive(true);

        transform.SetAsLastSibling();
        root.transform.SetAsLastSibling();
        root.SetActive(true);
        GameplayCursor.UnlockForUI();
    }

    private void Resume()
    {
        if (!isOpen)
            return;

        settingsPanel?.Hide();
        saveSlotPanel?.Hide();
        ForceResume();
    }

    private void ForceResume()
    {
        isOpen = false;
        IsOpen = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        if (root != null)
            root.SetActive(false);

        GameplayCursor.LockForGameplay();
    }

    private void OpenSaveSlots()
    {
        ResolveReferences();
        if (player == null || inventory == null)
        {
            ShowStatus("保存失败：缺少玩家数据");
            return;
        }

        if (saveSlotPanel == null)
            saveSlotPanel = GameplaySaveSlotPanel.Ensure(transform);

        if (root != null)
            root.SetActive(false);

        saveSlotPanel.transform.SetAsLastSibling();
        saveSlotPanel.Show(
            player,
            inventory,
            _ => { },
            CloseSaveSlots);
    }

    private void CloseSaveSlots()
    {
        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        if (menuCard != null)
            menuCard.SetActive(true);
    }

    private void OpenSettings()
    {
        if (settingsPanel == null)
            settingsPanel = MainMenuSettingsPanel.Ensure(transform);

        settingsPanel.HiddenCallback = RestorePauseOverlay;

        if (root != null)
            root.SetActive(false);

        settingsPanel.Show();
    }

    private void RestorePauseOverlay()
    {
        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        if (menuCard != null)
            menuCard.SetActive(true);
    }

    /// <summary>回主菜单前清掉待处理读档，避免主菜单再进场景时误套存档。</summary>
    private void ReturnToMainMenu()
    {
        ForceResume();
        SaveSession.ClearPending();
        GameplayCursor.UnlockForUI();
        GameSceneLoader.Load(MainMenuSceneName);
    }

    private void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void CloseInventoryIfOpen()
    {
        TryCloseInventory();
    }

    private bool TryCloseInventory()
    {
        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        InventoryUI inventoryUi = searchRoot.GetComponentInChildren<InventoryUI>(true);
        if (inventoryUi == null || !inventoryUi.IsOpen)
            return false;

        inventoryUi.SetOpen(false);
        return true;
    }

    public void BindPlayer(Player target)
    {
        if (target == null)
            return;

        player = target;
        inventory = target.GetComponent<Inventory>();
    }

    private void ResolveReferences()
    {
        if (player == null)
            player = Player.Resolve();

        if (inventory == null && player != null)
            inventory = player.GetComponent<Inventory>();
    }

    private void BuildUi()
    {
        Sprite slotSprite = SheikahUiStyle.SlotSprite;

        root = SheikahUiStyle.CreateChild(transform, "PauseOverlay").gameObject;
        SheikahUiStyle.Stretch(root.GetComponent<RectTransform>());

        Image dim = SheikahUiStyle.CreateChild(root.transform, "Dim", typeof(Image)).GetComponent<Image>();
        SheikahUiStyle.Stretch(dim.rectTransform);
        dim.color = SheikahUiStyle.Dim;
        dim.raycastTarget = true;

        menuCard = SheikahUiStyle.CreateCard(root.transform, "PauseMenuCard", new Vector2(520f, 560f), SheikahUiStyle.PanelSprite, 2.4f);

        TextMeshProUGUI title = SheikahUiStyle.CreateText(
            menuCard.transform,
            "Title",
            "暂停",
            40f,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            SheikahUiStyle.Orange);
        PlaceTop(title.rectTransform, -48f, 52f);

        CreateMenuButton(menuCard.transform, "ContinueButton", "继续游戏", 0.68f, Resume, slotSprite);
        CreateMenuButton(menuCard.transform, "SaveButton", "保存游戏", 0.52f, OpenSaveSlots, slotSprite);
        CreateMenuButton(menuCard.transform, "SettingsButton", "设置", 0.36f, OpenSettings, slotSprite);
        CreateMenuButton(menuCard.transform, "MainMenuButton", "返回主菜单", 0.20f, ReturnToMainMenu, slotSprite);

        statusText = SheikahUiStyle.CreateText(
            menuCard.transform,
            "StatusText",
            string.Empty,
            18f,
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            SheikahUiStyle.Muted);
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 18f);
        statusRect.sizeDelta = new Vector2(-48f, 28f);

        root.SetActive(false);
    }

    private static void CreateMenuButton(
        Transform parent,
        string name,
        string label,
        float anchorY,
        UnityEngine.Events.UnityAction action,
        Sprite slotSprite)
    {
        Button button = SheikahUiStyle.CreateButton(
            parent,
            name,
            label,
            new Vector2(340f, 56f),
            SheikahUiStyle.Orange,
            SheikahUiStyle.Text,
            slotSprite,
            5.5f);
        SheikahUiStyle.PlaceCentered(button.GetComponent<RectTransform>(), anchorY, new Vector2(340f, 56f));
        button.onClick.AddListener(action);
    }

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-96f, height);
    }
}
