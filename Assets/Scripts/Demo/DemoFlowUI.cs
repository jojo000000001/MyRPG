using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Demo 胜负界面。预制体提供完整 Overlay，缺引用时才在运行时补建。
/// </summary>
[DisallowMultipleComponent]
public sealed class DemoFlowUI : MonoBehaviour
{
    private const int OverlaySortingOrder = 500;
    private const string MainMenuSceneName = "MainMenuScene";
    private const float DefeatCardHeight = 440f;
    private const float VictoryCardHeight = 560f;

    [SerializeField] private RectTransform rootRect;
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private GameObject endScreenRoot;
    [SerializeField] private TextMeshProUGUI endTitleText;
    [SerializeField] private TextMeshProUGUI endBodyText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button loadSaveButton;
    [SerializeField] private Button mainMenuButton;

    private Action restartCallback;
    private MainMenuSaveSlotPanel saveSlotPanel;
    private bool loadingSave;
    private bool listenersBound;

    private void Awake()
    {
        EnsureUi();
        BindListeners();

        if (endScreenRoot != null)
            endScreenRoot.SetActive(false);
    }

    public void ShowDefeat(Action onRestart)
    {
        ShowEndScreen("你阵亡了", "巨龙将继续入侵这片土地……", "再试一次", onRestart, victory: false);
    }

    public void ShowVictory(Action onRestart)
    {
        ShowEndScreen("胜利！", "巨龙已被击败，这片土地重归平静。", "再玩一次", onRestart, victory: true);
    }

    private void ShowEndScreen(string title, string body, string buttonLabel, Action onRestart, bool victory)
    {
        EnsureUi();
        BindListeners();

        restartCallback = onRestart;
        loadingSave = false;

        if (endScreenRoot != null)
            endScreenRoot.SetActive(true);

        if (endTitleText != null)
            endTitleText.text = title;

        if (endBodyText != null)
            endBodyText.text = body;

        SheikahUiStyle.SetButtonLabel(restartButton, buttonLabel);

        ApplyLayout(victory);
        GameplayCursor.UnlockForUI();
    }

    private void ApplyLayout(bool victory)
    {
        if (cardRect != null)
            cardRect.sizeDelta = new Vector2(520f, victory ? VictoryCardHeight : DefeatCardHeight);

        if (endBodyText != null)
        {
            RectTransform bodyRect = endBodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, victory ? 0.62f : 0.58f);
            bodyRect.anchorMax = new Vector2(1f, victory ? 0.62f : 0.58f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = new Vector2(-80f, 80f);
        }

        if (restartButton != null)
        {
            SheikahUiStyle.PlaceCentered(
                restartButton.GetComponent<RectTransform>(),
                victory ? 0.48f : 0.34f,
                new Vector2(340f, 56f));
        }

        if (loadSaveButton != null)
            loadSaveButton.gameObject.SetActive(victory);

        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(true);
            SheikahUiStyle.SetButtonLabel(mainMenuButton, "返回主菜单");
            SheikahUiStyle.PlaceCentered(
                mainMenuButton.GetComponent<RectTransform>(),
                victory ? 0.16f : 0.14f,
                new Vector2(340f, 56f));
        }

        if (victory && loadSaveButton != null)
            SheikahUiStyle.PlaceCentered(loadSaveButton.GetComponent<RectTransform>(), 0.32f, new Vector2(340f, 56f));
    }

    private bool HasPrefabUi()
    {
        return endScreenRoot != null
            && cardRect != null
            && endTitleText != null
            && endBodyText != null
            && restartButton != null
            && loadSaveButton != null
            && mainMenuButton != null;
    }

    private void EnsureUi()
    {
        BindFromHierarchy();
        if (HasPrefabUi())
            return;

        if (rootRect != null)
            return;

        GameObject overlay = new GameObject("DemoFlowOverlay", typeof(RectTransform));
        overlay.transform.SetParent(transform, false);

        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        overlay.AddComponent<GraphicRaycaster>();

        GameObject root = new GameObject("DemoFlowRoot", typeof(RectTransform));
        root.transform.SetParent(overlay.transform, false);
        rootRect = root.GetComponent<RectTransform>();
        SheikahUiStyle.Stretch(rootRect);

        CreateEndScreen(root.transform);
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        if (restartButton != null)
            restartButton.onClick.AddListener(HandleRestartClicked);
        if (loadSaveButton != null)
            loadSaveButton.onClick.AddListener(HandleLoadSaveClicked);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(HandleReturnToMenuClicked);

        listenersBound = restartButton != null;
    }

    private void BindFromHierarchy()
    {
        if (rootRect == null)
        {
            Transform found = transform.Find("DemoFlowRoot");
            if (found != null)
                rootRect = found as RectTransform;
        }

        if (endScreenRoot == null)
        {
            Transform found = rootRect != null ? rootRect.Find("EndScreen") : transform.Find("DemoFlowRoot/EndScreen");
            if (found != null)
                endScreenRoot = found.gameObject;
        }

        Transform card = null;
        if (cardRect == null && endScreenRoot != null)
        {
            card = endScreenRoot.transform.Find("Card");
            if (card != null)
                cardRect = card as RectTransform;
        }

        if (card == null && cardRect != null)
            card = cardRect;

        if (card == null)
            return;

        if (endTitleText == null)
        {
            Transform title = card.Find("Title");
            if (title != null)
                endTitleText = title.GetComponent<TextMeshProUGUI>();
        }

        if (endBodyText == null)
        {
            Transform body = card.Find("Body");
            if (body != null)
                endBodyText = body.GetComponent<TextMeshProUGUI>();
        }

        if (restartButton == null)
        {
            Transform button = card.Find("RestartButton");
            if (button != null)
                restartButton = button.GetComponent<Button>();
        }

        if (loadSaveButton == null)
        {
            Transform button = card.Find("LoadSaveButton");
            if (button != null)
                loadSaveButton = button.GetComponent<Button>();
        }

        if (mainMenuButton == null)
        {
            Transform button = card.Find("MainMenuButton");
            if (button != null)
                mainMenuButton = button.GetComponent<Button>();
        }
    }

    private void CreateEndScreen(Transform parent)
    {
        Sprite slotSprite = SheikahUiStyle.SlotSprite;

        endScreenRoot = SheikahUiStyle.CreateChild(parent, "EndScreen").gameObject;
        SheikahUiStyle.Stretch(endScreenRoot.GetComponent<RectTransform>());

        Image backdrop = SheikahUiStyle.CreateChild(endScreenRoot.transform, "Dim", typeof(Image)).GetComponent<Image>();
        SheikahUiStyle.Stretch(backdrop.rectTransform);
        backdrop.color = SheikahUiStyle.Dim;
        backdrop.raycastTarget = true;

        GameObject card = SheikahUiStyle.CreateCard(endScreenRoot.transform, "Card", new Vector2(520f, DefeatCardHeight), SheikahUiStyle.PanelSprite, 2.4f);
        cardRect = card.GetComponent<RectTransform>();

        endTitleText = SheikahUiStyle.CreateText(
            card.transform,
            "Title",
            string.Empty,
            40f,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            SheikahUiStyle.Orange);
        RectTransform titleRect = endTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -48f);
        titleRect.sizeDelta = new Vector2(-96f, 52f);

        endBodyText = SheikahUiStyle.CreateText(
            card.transform,
            "Body",
            string.Empty,
            22f,
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            SheikahUiStyle.Text);
        RectTransform bodyRect = endBodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0.58f);
        bodyRect.anchorMax = new Vector2(1f, 0.58f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.sizeDelta = new Vector2(-80f, 80f);

        restartButton = CreateMenuButton(card.transform, "RestartButton", "再试一次", 0.34f, slotSprite);
        loadSaveButton = CreateMenuButton(card.transform, "LoadSaveButton", "读取存档", 0.32f, slotSprite);
        loadSaveButton.gameObject.SetActive(false);
        mainMenuButton = CreateMenuButton(card.transform, "MainMenuButton", "返回主菜单", 0.14f, slotSprite);

        endScreenRoot.SetActive(false);
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, float anchorY, Sprite slotSprite)
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
        return button;
    }

    private Transform ResolveCanvasRoot()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            return canvas.transform;

        if (rootRect != null && rootRect.parent != null)
            return rootRect.parent;

        return transform;
    }

    private void HandleRestartClicked()
    {
        restartCallback?.Invoke();
    }

    private void HandleLoadSaveClicked()
    {
        EnsureUi();

        if (endScreenRoot != null)
            endScreenRoot.SetActive(false);

        loadingSave = false;
        if (saveSlotPanel == null)
            saveSlotPanel = MainMenuSaveSlotPanel.Ensure(ResolveCanvasRoot());

        if (saveSlotPanel == null)
        {
            RestoreVictoryScreen();
            return;
        }

        saveSlotPanel.HiddenCallback = OnLoadPanelHidden;
        saveSlotPanel.transform.SetAsLastSibling();
        saveSlotPanel.ShowLoad(SceneManager.GetActiveScene().name, OnLoadNavigateStarted);
    }

    private void OnLoadNavigateStarted()
    {
        loadingSave = true;
        Time.timeScale = 1f;
    }

    private void OnLoadPanelHidden()
    {
        if (loadingSave)
            return;

        RestoreVictoryScreen();
    }

    private void RestoreVictoryScreen()
    {
        if (endScreenRoot != null)
            endScreenRoot.SetActive(true);

        GameplayCursor.UnlockForUI();
    }

    /// <summary>胜负界面返回主菜单。清掉待处理读档，避免下一局误套旧进度。</summary>
    private void HandleReturnToMenuClicked()
    {
        Time.timeScale = 1f;
        SaveSession.ClearPending();
        GameplayCursor.UnlockForUI();
        GameSceneLoader.Load(MainMenuSceneName);
    }
}
