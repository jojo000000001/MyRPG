using System;
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
    }

    private static readonly Color PanelColor = new Color(0.20f, 0.13f, 0.08f, 1f);
    private static readonly Color TitleTextColor = new Color(0.98f, 0.93f, 0.82f, 1f);
    private static readonly Color SummaryTextColor = new Color(0.90f, 0.86f, 0.78f, 1f);
    private static readonly Color ButtonColor = new Color(0.55f, 0.38f, 0.22f, 1f);
    private static readonly Color ButtonTextColor = new Color(0.98f, 0.93f, 0.82f, 1f);
    private static readonly Color EmptySlotColor = new Color(0.72f, 0.66f, 0.58f, 1f);
    private static readonly Color RowColor = new Color(0.10f, 0.07f, 0.05f, 0.94f);
    private static readonly Color DangerButtonColor = new Color(0.52f, 0.22f, 0.16f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.78f);

    private readonly SlotRowUi[] slotRows = new SlotRowUi[SaveSystem.SlotCount];

    private GameObject root;
    private Text titleText;
    private PanelMode currentMode;
    private string fallbackSceneName = "SampleScene";
    private Action onNavigateStarted;

    public static MainMenuSaveSlotPanel Ensure(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return null;

        MainMenuSaveSlotPanel existing = canvasRoot.GetComponentInChildren<MainMenuSaveSlotPanel>(true);
        if (existing != null)
            return existing;

        GameObject host = new GameObject("MainMenuSaveSlotPanel", typeof(RectTransform), typeof(MainMenuSaveSlotPanel));
        host.transform.SetParent(canvasRoot, false);
        Stretch(host.GetComponent<RectTransform>());
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

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        SetMainMenuCardVisible(true);
    }

    private void Show(PanelMode mode, string gameSceneName, Action navigateStarted)
    {
        if (root == null)
            BuildUi();

        currentMode = mode;
        fallbackSceneName = string.IsNullOrEmpty(gameSceneName) ? "SampleScene" : gameSceneName;
        onNavigateStarted = navigateStarted;

        if (titleText != null)
            titleText.text = mode == PanelMode.Load ? "读取存档" : "选择存档";

        ApplyVisualStyle();
        RefreshRows();
        SetMainMenuCardVisible(false);
        root.transform.SetAsLastSibling();
        root.SetActive(true);
    }

    private void SetMainMenuCardVisible(bool visible)
    {
        Transform menuCard = transform.Find("UIRoot/MenuCard");
        if (menuCard != null)
            menuCard.gameObject.SetActive(visible);
    }

    private void ApplyVisualStyle()
    {
        if (root == null)
            return;

        Image dim = root.transform.Find("Dim")?.GetComponent<Image>();
        if (dim != null)
            dim.color = DimColor;

        Transform card = root.transform.Find("SaveSlotCard");
        if (card != null)
        {
            Image cardImage = card.GetComponent<Image>();
            if (cardImage != null)
                cardImage.color = PanelColor;
        }

        if (titleText != null)
            titleText.color = TitleTextColor;

        for (int i = 0; i < slotRows.Length; i++)
        {
            SlotRowUi row = slotRows[i];
            if (row == null)
                continue;

            Image rowImage = row.titleText != null ? row.titleText.transform.parent.GetComponent<Image>() : null;
            if (rowImage != null)
                rowImage.color = RowColor;

            if (row.titleText != null)
                row.titleText.color = TitleTextColor;
        }
    }

    private void RefreshRows()
    {
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            SaveSlotSummary summary = SaveSystem.GetSlotSummary(i);
            SlotRowUi row = slotRows[i];
            if (row == null)
                continue;

            row.titleText.text = $"存档 {i + 1}";
            if (summary.hasSave)
            {
                row.summaryText.text =
                    $"Lv.{summary.level}  ·  {summary.sceneName}\n" +
                    $"HP {summary.currentHp}/{summary.maxHp}  ·  {summary.savedAtDisplay}";
                row.summaryText.color = SummaryTextColor;
            }
            else
            {
                row.summaryText.text = "空存档";
                row.summaryText.color = EmptySlotColor;
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

            SetButtonLabel(row.primaryButton, "读取");
            SetButtonLabel(row.secondaryButton, "删除");
            row.primaryButton.onClick.RemoveAllListeners();
            row.secondaryButton.onClick.RemoveAllListeners();
            row.primaryButton.onClick.AddListener(() => LoadSlot(summary.slotIndex));
            row.secondaryButton.onClick.AddListener(() => DeleteSlot(summary.slotIndex));
            return;
        }

        row.primaryButton.gameObject.SetActive(true);
        row.secondaryButton.gameObject.SetActive(summary.hasSave);

        SetButtonLabel(row.primaryButton, summary.hasSave ? "重新开始" : "开始游戏");
        SetButtonLabel(row.secondaryButton, "删除");
        row.primaryButton.onClick.RemoveAllListeners();
        row.secondaryButton.onClick.RemoveAllListeners();
        row.primaryButton.onClick.AddListener(() => StartNewGame(summary.slotIndex));
        if (summary.hasSave)
            row.secondaryButton.onClick.AddListener(() => DeleteSlot(summary.slotIndex));
    }

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
        root = CreateChild(transform, "SaveSlotOverlay", typeof(RectTransform)).gameObject;
        Stretch(root.GetComponent<RectTransform>());

        Image dim = CreateChild(root.transform, "Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Stretch(dim.rectTransform);
        dim.color = DimColor;
        dim.raycastTarget = true;

        GameObject card = CreateChild(root.transform, "SaveSlotCard", typeof(RectTransform), typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(560f, 560f);
        card.GetComponent<Image>().color = PanelColor;

        titleText = CreateText(card.transform, "Title", "读取存档", 34, TextAnchor.MiddleCenter, FontStyle.Bold, TitleTextColor);
        PlaceTop(titleText.rectTransform, -24f, 48f);

        float firstRowY = 0.78f;
        const float rowStep = 0.22f;
        for (int i = 0; i < SaveSystem.SlotCount; i++)
            slotRows[i] = CreateSlotRow(card.transform, i, firstRowY - rowStep * i);

        Button backButton = CreateButton(card.transform, "BackButton", "返回", 0.06f, ButtonColor);
        backButton.onClick.AddListener(Hide);

        root.SetActive(false);
    }

    private SlotRowUi CreateSlotRow(Transform parent, int slotIndex, float anchorY)
    {
        GameObject rowObject = CreateChild(parent, $"SlotRow_{slotIndex + 1}", typeof(RectTransform), typeof(Image));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, anchorY);
        rowRect.anchorMax = new Vector2(0.5f, anchorY);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(500f, 96f);
        rowObject.GetComponent<Image>().color = RowColor;

        Text titleTextLocal = CreateText(rowObject.transform, "Title", $"存档 {slotIndex + 1}", 24, TextAnchor.UpperLeft, FontStyle.Bold, TitleTextColor);
        RectTransform titleRect = titleTextLocal.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(18f, -10f);
        titleRect.sizeDelta = new Vector2(-180f, 24f);

        Text summaryText = CreateText(rowObject.transform, "Summary", "空存档", 20, TextAnchor.UpperLeft, FontStyle.Normal, EmptySlotColor);
        RectTransform summaryRect = summaryText.rectTransform;
        summaryRect.anchorMin = new Vector2(0f, 0f);
        summaryRect.anchorMax = new Vector2(1f, 1f);
        summaryRect.offsetMin = new Vector2(18f, 12f);
        summaryRect.offsetMax = new Vector2(-170f, -34f);
        summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        summaryText.verticalOverflow = VerticalWrapMode.Overflow;

        Button primaryButton = CreateInlineButton(rowObject.transform, "PrimaryButton", "读取", new Vector2(-150f, 0f), ButtonColor);
        Button secondaryButton = CreateInlineButton(rowObject.transform, "SecondaryButton", "删除", new Vector2(-18f, 0f), DangerButtonColor);

        return new SlotRowUi
        {
            titleText = titleTextLocal,
            summaryText = summaryText,
            primaryButton = primaryButton,
            secondaryButton = secondaryButton,
        };
    }

    private static Button CreateInlineButton(Transform parent, string name, string label, Vector2 anchoredPosition, Color color)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(112f, 40f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject.transform, "Label", label, 18, TextAnchor.MiddleCenter, FontStyle.Bold, ButtonTextColor);
        Stretch(text.rectTransform);
        return button;
    }

    private static Button CreateButton(Transform parent, string name, string label, float anchorY, Color color)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, anchorY);
        buttonRect.anchorMax = new Vector2(0.5f, anchorY);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(220f, 48f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject.transform, "Label", label, 22, TextAnchor.MiddleCenter, FontStyle.Bold, ButtonTextColor);
        Stretch(text.rectTransform);
        return button;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
            text.text = label;
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

        Shadow shadow = textObject.GetComponent<Shadow>();
        if (shadow == null)
            shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        return label;
    }

    private static GameObject CreateChild(Transform parent, string name, params Type[] components)
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

    private sealed class SlotRowUi
    {
        public Text titleText;
        public Text summaryText;
        public Button primaryButton;
        public Button secondaryButton;
    }
}
