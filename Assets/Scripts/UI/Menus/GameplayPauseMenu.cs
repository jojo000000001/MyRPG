using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 ESC 暂停菜单：继续、保存、设置、返回主菜单。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayPauseMenu : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";

    private static readonly Color PanelColor = new Color(0.25f, 0.16f, 0.10f, 0.96f);
    private static readonly Color PrimaryTextColor = new Color(0.18f, 0.1f, 0.045f, 1f);
    private static readonly Color ButtonColor = new Color(0.55f, 0.38f, 0.22f, 1f);
    private static readonly Color ButtonTextColor = new Color(0.98f, 0.93f, 0.82f, 1f);
    private static readonly Color MutedTextColor = new Color(0.35f, 0.24f, 0.14f, 0.85f);

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Inventory inventory;

    private GameObject root;
    private GameObject menuCard;
    private Text statusText;
    private MainMenuSettingsPanel settingsPanel;
    private GameplaySaveSlotPanel saveSlotPanel;
    private float previousTimeScale = 1f;
    private bool isOpen;

    public static bool IsOpen { get; private set; }

    public static GameplayPauseMenu EnsureForHud(Transform hudRoot)
    {
        if (hudRoot == null)
            return null;

        GameplayPauseMenu existing = hudRoot.GetComponentInChildren<GameplayPauseMenu>(true);
        if (existing != null)
            return existing;

        GameObject host = new GameObject("GameplayPauseMenu", typeof(RectTransform), typeof(GameplayPauseMenu));
        host.transform.SetParent(hudRoot, false);
        Stretch(host.GetComponent<RectTransform>());
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

        if (settingsPanel != null && IsSettingsVisible())
        {
            settingsPanel.Hide();
            if (menuCard != null)
                menuCard.SetActive(true);
            return;
        }

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

        settingsPanel.HiddenCallback = () =>
        {
            if (menuCard != null)
                menuCard.SetActive(true);
        };

        if (menuCard != null)
            menuCard.SetActive(false);

        settingsPanel.Show();
    }

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
        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        InventoryUI inventoryUi = searchRoot.GetComponentInChildren<InventoryUI>(true);
        if (inventoryUi != null)
            inventoryUi.SetOpen(false);
    }

    private bool IsSettingsVisible()
    {
        if (settingsPanel == null)
            return false;

        Transform settingsTransform = transform.Find("MainMenuSettingsPanel");
        if (settingsTransform == null)
            return false;

        Transform overlay = settingsTransform.Find("SettingsOverlay");
        return overlay != null && overlay.gameObject.activeSelf;
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
        root = CreateChild(transform, "PauseOverlay", typeof(RectTransform)).gameObject;
        Stretch(root.GetComponent<RectTransform>());

        Image dim = CreateChild(root.transform, "Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        menuCard = CreateChild(root.transform, "PauseMenuCard", typeof(RectTransform), typeof(Image));
        RectTransform cardRect = menuCard.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(420f, 420f);
        menuCard.GetComponent<Image>().color = PanelColor;

        Text title = CreateText(menuCard.transform, "Title", "暂停", 34, TextAnchor.MiddleCenter, FontStyle.Bold, PrimaryTextColor);
        PlaceTop(title.rectTransform, -24f, 48f);

        CreateMenuButton(menuCard.transform, "ContinueButton", "继续游戏", 0.68f, Resume);
        CreateMenuButton(menuCard.transform, "SaveButton", "保存游戏", 0.52f, OpenSaveSlots);
        CreateMenuButton(menuCard.transform, "SettingsButton", "设置", 0.36f, OpenSettings);
        CreateMenuButton(menuCard.transform, "MainMenuButton", "返回主菜单", 0.20f, ReturnToMainMenu);

        statusText = CreateText(menuCard.transform, "StatusText", string.Empty, 18, TextAnchor.MiddleCenter, FontStyle.Normal, MutedTextColor);
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 14f);
        statusRect.sizeDelta = new Vector2(-48f, 28f);

        root.SetActive(false);
    }

    private void CreateMenuButton(Transform parent, string name, string label, float anchorY, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, name, label, anchorY);
        button.onClick.AddListener(action);
    }

    private static Button CreateButton(Transform parent, string name, string label, float anchorY)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, anchorY);
        buttonRect.anchorMax = new Vector2(0.5f, anchorY);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(280f, 48f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject.transform, "Label", label, 22, TextAnchor.MiddleCenter, FontStyle.Bold, ButtonTextColor);
        Stretch(text.rectTransform);
        return button;
    }

    private static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, FontStyle style, Color color)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(Text));
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        ChineseUIFont.Apply(label, fontSize, style);
        return label;
    }

    private static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void Stretch(RectTransform rect, float horizontalPadding = 0f, float verticalPadding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        rect.localScale = Vector3.one;
    }

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-48f, height);
    }
}
