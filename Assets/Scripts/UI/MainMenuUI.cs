using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 开始界面：继续游戏、开始游戏、读取存档、设置、退出。
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuUI : MonoBehaviour
{
    private static readonly Color MutedButtonTextColor = new Color(0.55f, 0.5f, 0.45f, 0.85f);

    [Header("Flow")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Buttons")]
    [SerializeField] private Button continueGameButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button loadSaveButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    private MainMenuSettingsPanel settingsPanel;
    private MainMenuSaveSlotPanel saveSlotPanel;
    private bool isNavigating;

    private void Awake()
    {
        GameplayCursor.UnlockForUI();
        GameSettings.Apply();
        EnsureMainMenuLayout();
    }

    private void Start()
    {
        settingsPanel = MainMenuSettingsPanel.Ensure(transform);
        saveSlotPanel = MainMenuSaveSlotPanel.Ensure(transform);
        RefreshSaveButtonStates();
    }

    private void EnsureMainMenuLayout()
    {
        Transform menuCard = transform.Find("UIRoot/MenuCard");
        if (menuCard == null)
            return;

        RectTransform cardRect = menuCard as RectTransform;
        if (cardRect != null)
            cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, 580f);

        if (continueGameButton == null)
        {
            Transform existing = menuCard.Find("ContinueGameButton");
            if (existing != null)
                continueGameButton = existing.GetComponent<Button>();
        }

        if (continueGameButton == null && startGameButton != null)
        {
            GameObject continueObject = Instantiate(startGameButton.gameObject, menuCard);
            continueObject.name = "ContinueGameButton";
            continueGameButton = continueObject.GetComponent<Button>();
            continueGameButton.onClick.RemoveAllListeners();

            Text continueLabel = continueObject.GetComponentInChildren<Text>(true);
            if (continueLabel != null)
                continueLabel.text = "继续游戏";
        }

        PlaceMenuButton(menuCard, "ContinueGameButton", 0.76f);
        PlaceMenuButton(menuCard, "StartGameButton", 0.62f);
        PlaceMenuButton(menuCard, "LoadSaveButton", 0.48f);
        PlaceMenuButton(menuCard, "SettingsButton", 0.34f);
        PlaceMenuButton(menuCard, "QuitButton", 0.20f);
    }

    private static void PlaceMenuButton(Transform menuCard, string buttonName, float anchorY)
    {
        Transform buttonTransform = menuCard.Find(buttonName);
        if (buttonTransform == null)
            return;

        RectTransform rect = buttonTransform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(300f, 52f);
    }

    private void OnEnable()
    {
        if (continueGameButton != null)
            continueGameButton.onClick.AddListener(ContinueGame);

        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartNewGame);

        if (loadSaveButton != null)
            loadSaveButton.onClick.AddListener(OpenLoadSlots);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    private void OnDisable()
    {
        if (continueGameButton != null)
            continueGameButton.onClick.RemoveListener(ContinueGame);

        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(StartNewGame);

        if (loadSaveButton != null)
            loadSaveButton.onClick.RemoveListener(OpenLoadSlots);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);
    }

    private void ContinueGame()
    {
        if (isNavigating || !SaveSystem.TryGetMostRecentSlot(out int slotIndex))
            return;

        if (!SaveSystem.TryRead(slotIndex, out SaveData data))
            return;

        settingsPanel?.Hide();
        saveSlotPanel?.Hide();
        SaveSession.BeginLoad(slotIndex);
        OnNavigateStarted();

        GameplayCursor.LockForGameplay();
        string sceneName = string.IsNullOrEmpty(data.sceneName) ? gameSceneName : data.sceneName;
        GameSceneLoader.Load(sceneName);
    }

    private void StartNewGame()
    {
        if (isNavigating)
            return;

        settingsPanel?.Hide();
        saveSlotPanel?.Hide();

        int slotIndex = SaveSystem.FindPreferredNewGameSlot();
        SaveSession.BeginNewGame(slotIndex);
        SaveSystem.Delete(slotIndex);
        OnNavigateStarted();

        GameplayCursor.LockForGameplay();
        GameSceneLoader.Load(gameSceneName);
    }

    private void OpenLoadSlots()
    {
        if (isNavigating || !SaveSystem.HasAnySave())
            return;

        settingsPanel?.Hide();
        if (saveSlotPanel == null)
            saveSlotPanel = MainMenuSaveSlotPanel.Ensure(transform);

        saveSlotPanel.ShowLoad(gameSceneName, OnNavigateStarted);
    }

    private void OpenSettings()
    {
        saveSlotPanel?.Hide();
        if (settingsPanel == null)
            settingsPanel = MainMenuSettingsPanel.Ensure(transform);

        settingsPanel?.Show();
    }

    private void OnNavigateStarted()
    {
        isNavigating = true;
        SetButtonsInteractable(false);
        settingsPanel?.Hide();
    }

    private void RefreshSaveButtonStates()
    {
        bool hasAnySave = SaveSystem.HasAnySave();

        if (continueGameButton != null)
        {
            continueGameButton.interactable = hasAnySave;
            if (!hasAnySave)
                SetButtonLabelColor(continueGameButton, MutedButtonTextColor);
        }

        if (loadSaveButton != null)
            loadSaveButton.interactable = hasAnySave;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetButtonsInteractable(bool interactable)
    {
        bool hasAnySave = SaveSystem.HasAnySave();

        if (continueGameButton != null)
            continueGameButton.interactable = interactable && hasAnySave;

        if (startGameButton != null)
            startGameButton.interactable = interactable;

        if (loadSaveButton != null)
            loadSaveButton.interactable = interactable && hasAnySave;

        if (settingsButton != null)
            settingsButton.interactable = interactable;

        if (quitButton != null)
            quitButton.interactable = interactable;
    }

    private static void SetButtonLabelColor(Button button, Color color)
    {
        if (button == null)
            return;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
            label.color = color;
    }
}
