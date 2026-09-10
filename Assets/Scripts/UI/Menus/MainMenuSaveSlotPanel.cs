using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单三存档槽位面板：读取、删除、开始新游戏。
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuSaveSlotPanel : MonoBehaviour
{
    public enum PanelMode
    {
        Load,
        NewGame,
        Start,
    }

    private readonly SlotRowUi[] slotRows = new SlotRowUi[SaveSystem.SlotCount];

    private GameObject root;
    private TextMeshProUGUI titleText;
    private PanelMode currentMode;
    private string fallbackSceneName = "SampleScene";
    private Action onNavigateStarted;

    public Action HiddenCallback;

    public static MainMenuSaveSlotPanel Ensure(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return null;

        MainMenuSaveSlotPanel existing = canvasRoot.GetComponentInChildren<MainMenuSaveSlotPanel>(true);
        if (existing != null)
            return existing;

        GameObject host = new GameObject("MainMenuSaveSlotPanel", typeof(RectTransform), typeof(MainMenuSaveSlotPanel));
        host.transform.SetParent(canvasRoot, false);
        SheikahUiStyle.Stretch(host.GetComponent<RectTransform>());
        return host.GetComponent<MainMenuSaveSlotPanel>();
    }

    private void Awake()
    {
        if (root == null)
            BuildUi();
    }

    public void ShowLoad(string gameSceneName, Action navigateStarted)
    {
        Show(PanelMode.Load, gameSceneName, navigateStarted);
    }

    public void ShowNewGame(string gameSceneName, Action navigateStarted)
    {
        Show(PanelMode.NewGame, gameSceneName, navigateStarted);
    }

    public void ShowStart(string gameSceneName, Action navigateStarted)
    {
        Show(PanelMode.Start, gameSceneName, navigateStarted);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        SetMainMenuCardVisible(true);

        Action callback = HiddenCallback;
        HiddenCallback = null;
        callback?.Invoke();
    }

    private void Show(PanelMode mode, string gameSceneName, Action navigateStarted)
    {
        if (root == null)
            BuildUi();

        currentMode = mode;
        fallbackSceneName = string.IsNullOrEmpty(gameSceneName) ? "SampleScene" : gameSceneName;
        onNavigateStarted = navigateStarted;

        if (titleText != null)
        {
            titleText.text = mode switch
            {
                PanelMode.Load => "读取存档",
                PanelMode.NewGame => "选择存档",
                _ => "开始游戏",
            };
        }

        RefreshRows();
        SetMainMenuCardVisible(false);
        root.transform.SetAsLastSibling();
        root.SetActive(true);
    }

    private void SetMainMenuCardVisible(bool visible)
    {
        Transform menuCard = FindMainMenuCard();
        if (menuCard != null)
            menuCard.gameObject.SetActive(visible);
    }

    private Transform FindMainMenuCard()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        return canvas.transform.Find("UIRoot/MenuCard");
    }

    private void RefreshRows()
    {
        SaveSystem.TryGetMostRecentSlot(out int mostRecentSlot);

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            SaveSlotSummary summary = SaveSystem.GetSlotSummary(i);
            SlotRowUi row = slotRows[i];
            if (row == null)
                continue;

            bool isRecent = summary.hasSave && i == mostRecentSlot;
            row.titleText.text = isRecent ? $"存档 {i + 1}（最近）" : $"存档 {i + 1}";
            if (summary.hasSave)
            {
                row.summaryText.text =
                    $"Lv.{summary.level}  ·  {summary.rupees}卢比  ·  {summary.questLabel}\n" +
                    $"{summary.playTimeDisplay}  ·  {summary.savedAtDisplay}";
                row.summaryText.color = SheikahUiStyle.Muted;
            }
            else
            {
                row.summaryText.text = "空存档";
                row.summaryText.color = SheikahUiStyle.Muted;
            }

            ConfigureRowButtons(row, summary);
        }
    }

    private void ConfigureRowButtons(SlotRowUi row, SaveSlotSummary summary)
    {
        if (currentMode == PanelMode.Load)
        {
            row.primaryButton.gameObject.SetActive(summary.hasSave);
            row.secondaryButton.gameObject.SetActive(summary.hasSave);

            if (!summary.hasSave)
                return;

            SetRowButton(row.primaryButton, "读取", SheikahUiStyle.Orange, SheikahUiStyle.Text);
            SetRowButton(row.secondaryButton, "删除", SheikahUiStyle.Danger, SheikahUiStyle.Text);
            row.primaryButton.onClick.RemoveAllListeners();
            row.secondaryButton.onClick.RemoveAllListeners();
            row.primaryButton.onClick.AddListener(() => LoadSlot(summary.slotIndex));
            row.secondaryButton.onClick.AddListener(() => DeleteSlot(summary.slotIndex));
            return;
        }

        if (currentMode == PanelMode.Start)
        {
            row.primaryButton.gameObject.SetActive(true);
            row.secondaryButton.gameObject.SetActive(summary.hasSave);

            SetRowButton(row.primaryButton, summary.hasSave ? "读取" : "开始游戏", SheikahUiStyle.Orange, SheikahUiStyle.Text);
            SetRowButton(row.secondaryButton, "新游戏", SheikahUiStyle.Inactive, SheikahUiStyle.Text);
            row.primaryButton.onClick.RemoveAllListeners();
            row.secondaryButton.onClick.RemoveAllListeners();
            row.primaryButton.onClick.AddListener(() =>
            {
                if (summary.hasSave)
                    LoadSlot(summary.slotIndex);
                else
                    StartNewGame(summary.slotIndex);
            });

            if (summary.hasSave)
                row.secondaryButton.onClick.AddListener(() => StartNewGame(summary.slotIndex));
            return;
        }

        row.primaryButton.gameObject.SetActive(true);
        row.secondaryButton.gameObject.SetActive(summary.hasSave);

        SetRowButton(row.primaryButton, summary.hasSave ? "重新开始" : "开始游戏", SheikahUiStyle.Orange, SheikahUiStyle.Text);
        SetRowButton(row.secondaryButton, "删除", SheikahUiStyle.Danger, SheikahUiStyle.Text);
        row.primaryButton.onClick.RemoveAllListeners();
        row.secondaryButton.onClick.RemoveAllListeners();
        row.primaryButton.onClick.AddListener(() => StartNewGame(summary.slotIndex));
        if (summary.hasSave)
            row.secondaryButton.onClick.AddListener(() => DeleteSlot(summary.slotIndex));
    }

    private static void SetRowButton(Button button, string label, Color tint, Color labelColor)
    {
        Image image = button != null ? button.targetGraphic as Image : null;
        if (image != null)
            SheikahUiStyle.ApplySliced(image, SheikahUiStyle.SlotSprite, tint, 6f);

        SheikahUiStyle.SetButtonLabel(button, label);
        SheikahUiStyle.SetButtonLabelColor(button, labelColor);
    }

    /// <summary>读取该槽并进入玩法场景，由 SaveLoadService 在场景加载后套用存档。</summary>
    private void LoadSlot(int slotIndex)
    {
        if (!SaveSystem.TryRead(slotIndex, out SaveData data))
            return;

        SaveSession.BeginLoad(slotIndex);
        onNavigateStarted?.Invoke();
        Hide();

        GameplayCursor.LockForGameplay();
        string sceneName = string.IsNullOrEmpty(data.sceneName) ? fallbackSceneName : data.sceneName;
        GameSceneLoader.Load(sceneName);
    }

    /// <summary>清空该槽并按新游戏进入场景。</summary>
    private void StartNewGame(int slotIndex)
    {
        SaveSession.BeginNewGame(slotIndex);
        SaveSystem.Delete(slotIndex);
        onNavigateStarted?.Invoke();
        Hide();

        GameplayCursor.LockForGameplay();
        GameSceneLoader.Load(fallbackSceneName);
    }

    private void DeleteSlot(int slotIndex)
    {
        SaveSystem.Delete(slotIndex);
        RefreshRows();
    }

    private void BuildUi()
    {
        Sprite panelSprite = SheikahUiStyle.PanelSprite;
        Sprite slotSprite = SheikahUiStyle.SlotSprite;

        root = SheikahUiStyle.CreateChild(transform, "SaveSlotOverlay").gameObject;
        SheikahUiStyle.Stretch(root.GetComponent<RectTransform>());

        Image dim = SheikahUiStyle.CreateChild(root.transform, "Dim", typeof(Image)).GetComponent<Image>();
        SheikahUiStyle.Stretch(dim.rectTransform);
        dim.color = SheikahUiStyle.Dim;
        dim.raycastTarget = true;

        GameObject card = SheikahUiStyle.CreateCard(root.transform, "SaveSlotCard", new Vector2(640f, 640f), panelSprite, 2.2f);

        titleText = SheikahUiStyle.CreateText(card.transform, "Title", "读取存档", 34f, TextAlignmentOptions.Center, FontStyles.Bold, SheikahUiStyle.Orange);
        PlaceTop(titleText.rectTransform, -40f, 48f);

        float firstRowY = 0.72f;
        const float rowStep = 0.20f;
        for (int i = 0; i < SaveSystem.SlotCount; i++)
            slotRows[i] = CreateSlotRow(card.transform, i, firstRowY - rowStep * i, slotSprite);

        Button backButton = SheikahUiStyle.CreateButton(
            card.transform,
            "BackButton",
            "返回",
            new Vector2(220f, 48f),
            SheikahUiStyle.Orange,
            SheikahUiStyle.Text,
            slotSprite,
            5.5f);
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.08f);
        backRect.anchorMax = new Vector2(0.5f, 0.08f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = Vector2.zero;
        backButton.onClick.AddListener(Hide);

        root.SetActive(false);
    }

    private SlotRowUi CreateSlotRow(Transform parent, int slotIndex, float anchorY, Sprite slotSprite)
    {
        GameObject rowObject = SheikahUiStyle.CreateChild(parent, $"SlotRow_{slotIndex + 1}", typeof(Image));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, anchorY);
        rowRect.anchorMax = new Vector2(0.5f, anchorY);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(540f, 96f);
        SheikahUiStyle.ApplySliced(rowObject.GetComponent<Image>(), slotSprite, Color.white, 4.6f);

        TextMeshProUGUI titleTextLocal = SheikahUiStyle.CreateText(
            rowObject.transform,
            "Title",
            $"存档 {slotIndex + 1}",
            22f,
            TextAlignmentOptions.TopLeft,
            FontStyles.Bold,
            SheikahUiStyle.Text);
        RectTransform titleRect = titleTextLocal.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(22f, -12f);
        titleRect.sizeDelta = new Vector2(-200f, 24f);

        TextMeshProUGUI summaryText = SheikahUiStyle.CreateText(
            rowObject.transform,
            "Summary",
            "空存档",
            18f,
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal,
            SheikahUiStyle.Muted);
        RectTransform summaryRect = summaryText.rectTransform;
        summaryRect.anchorMin = new Vector2(0f, 0f);
        summaryRect.anchorMax = new Vector2(1f, 1f);
        summaryRect.offsetMin = new Vector2(22f, 12f);
        summaryRect.offsetMax = new Vector2(-188f, -36f);

        Button primaryButton = CreateInlineButton(rowObject.transform, "PrimaryButton", "读取", new Vector2(-150f, 0f), slotSprite, SheikahUiStyle.Orange, SheikahUiStyle.Text);
        Button secondaryButton = CreateInlineButton(rowObject.transform, "SecondaryButton", "删除", new Vector2(-18f, 0f), slotSprite, SheikahUiStyle.Danger, SheikahUiStyle.Text);

        return new SlotRowUi
        {
            titleText = titleTextLocal,
            summaryText = summaryText,
            primaryButton = primaryButton,
            secondaryButton = secondaryButton,
        };
    }

    private static Button CreateInlineButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        Sprite slotSprite,
        Color tint,
        Color labelColor)
    {
        Button button = SheikahUiStyle.CreateButton(parent, name, label, new Vector2(112f, 40f), tint, labelColor, slotSprite, 6f);
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(112f, 40f);
        return button;
    }

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-96f, height);
    }

    private sealed class SlotRowUi
    {
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI summaryText;
        public Button primaryButton;
        public Button secondaryButton;
    }
}
