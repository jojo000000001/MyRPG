using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 开始界面：继续游戏、开始游戏、设置、退出。
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuUI : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Buttons")]
    [SerializeField] private Button continueGameButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Sheikah Style")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite slotSprite;

    private MainMenuSettingsPanel settingsPanel;
    private MainMenuSaveSlotPanel saveSlotPanel;
    private bool isNavigating;

    private void Awake()
    {
        GameplayCursor.UnlockForUI();
        GameSettings.Apply();
        SheikahUiStyle.Register(panelSprite, slotSprite);
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
            cardRect.sizeDelta = new Vector2(520f, 560f);

        Image cardImage = menuCard.GetComponent<Image>();
        if (cardImage != null)
            cardImage.enabled = false;

        EnsureSheikahChrome(menuCard);

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
            SheikahUiStyle.SetButtonLabel(continueGameButton, "继续游戏");
        }

        HideLegacyButton(menuCard, "LoadSaveButton");

        PlaceMenuButton(menuCard, "ContinueGameButton", 0.68f);
        PlaceMenuButton(menuCard, "StartGameButton", 0.52f);
        PlaceMenuButton(menuCard, "SettingsButton", 0.36f);
        PlaceMenuButton(menuCard, "QuitButton", 0.20f);
        StyleMenuButtons(menuCard);
    }

    private static void EnsureSheikahChrome(Transform menuCard)
    {
        Transform background = menuCard.Find("Background");
        if (background == null)
        {
            Image fill = SheikahUiStyle.CreateChild(menuCard, "Background", typeof(Image)).GetComponent<Image>();
            SheikahUiStyle.Stretch(fill.rectTransform);
            fill.color = SheikahUiStyle.Fill;
            fill.raycastTarget = true;
            fill.transform.SetAsFirstSibling();
        }

        Transform frame = menuCard.Find("Frame");
        if (frame == null)
        {
            Image frameImage = SheikahUiStyle.CreateChild(menuCard, "Frame", typeof(Image)).GetComponent<Image>();
            SheikahUiStyle.Stretch(frameImage.rectTransform);
            SheikahUiStyle.ApplySliced(frameImage, SheikahUiStyle.PanelSprite, Color.white, 2.4f);
            frameImage.raycastTarget = false;
            frameImage.transform.SetSiblingIndex(1);
        }

        Transform title = menuCard.Find("Title");
        if (title == null)
            return;

        TextMeshProUGUI tmp = title.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.color = SheikahUiStyle.Orange;
            ChineseUITmpFont.Apply(tmp, tmp.fontSize > 1f ? tmp.fontSize : 40f, FontStyles.Bold);
            return;
        }

        Text legacy = title.GetComponent<Text>();
        if (legacy != null)
            legacy.color = SheikahUiStyle.Orange;
    }

    private static void StyleMenuButtons(Transform menuCard)
    {
        Sprite slot = SheikahUiStyle.SlotSprite;
        StyleButton(menuCard.Find("ContinueGameButton"), slot, SheikahUiStyle.Orange, SheikahUiStyle.Text);
        StyleButton(menuCard.Find("StartGameButton"), slot, SheikahUiStyle.Orange, SheikahUiStyle.Text);
        StyleButton(menuCard.Find("SettingsButton"), slot, SheikahUiStyle.Inactive, SheikahUiStyle.Text);
        StyleButton(menuCard.Find("QuitButton"), slot, SheikahUiStyle.Inactive, SheikahUiStyle.Text);
    }

    private static void StyleButton(Transform buttonTransform, Sprite slot, Color tint, Color labelColor)
    {
        if (buttonTransform == null)
            return;

        Image image = buttonTransform.GetComponent<Image>();
        if (image != null)
            SheikahUiStyle.ApplySliced(image, slot, tint, 5.5f);

        Button button = buttonTransform.GetComponent<Button>();
        if (button != null)
        {
            button.transition = Selectable.Transition.None;
            if (image != null)
                button.targetGraphic = image;
        }

        SheikahUiStyle.SetButtonLabelColor(button, labelColor);
    }

    private static void HideLegacyButton(Transform menuCard, string buttonName)
    {
        Transform buttonTransform = menuCard.Find(buttonName);
        if (buttonTransform != null)
            buttonTransform.gameObject.SetActive(false);
    }

    private static void PlaceMenuButton(Transform menuCard, string buttonName, float anchorY)
    {
        Transform buttonTransform = menuCard.Find(buttonName);
        if (buttonTransform == null)
            return;

        buttonTransform.gameObject.SetActive(true);

        RectTransform rect = buttonTransform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(340f, 56f);
    }

    private void OnEnable()
    {
        RefreshSaveButtonStates();

        if (continueGameButton != null)
            continueGameButton.onClick.AddListener(ContinueGame);

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OpenStartPanel);

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
            startGameButton.onClick.RemoveListener(OpenStartPanel);

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

    private void OpenStartPanel()
    {
        if (isNavigating)
            return;

        settingsPanel?.Hide();
        if (saveSlotPanel == null)
            saveSlotPanel = MainMenuSaveSlotPanel.Ensure(transform);

        saveSlotPanel.ShowStart(gameSceneName, OnNavigateStarted);
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

        if (continueGameButton == null)
            return;

        continueGameButton.interactable = hasAnySave;
        Image image = continueGameButton.targetGraphic as Image;
        if (hasAnySave)
        {
            if (image != null)
                image.color = SheikahUiStyle.Orange;
            SheikahUiStyle.SetButtonLabelColor(continueGameButton, SheikahUiStyle.Text);
        }
        else
        {
            if (image != null)
                image.color = SheikahUiStyle.Inactive;
            SheikahUiStyle.SetButtonLabelColor(continueGameButton, SheikahUiStyle.Muted);
        }
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

        if (settingsButton != null)
            settingsButton.interactable = interactable;

        if (quitButton != null)
            quitButton.interactable = interactable;
    }
}
