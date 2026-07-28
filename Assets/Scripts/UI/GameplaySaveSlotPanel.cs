using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 ESC 暂停菜单用的三槽位保存面板。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplaySaveSlotPanel : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.20f, 0.13f, 0.08f, 1f);
    private static readonly Color TitleTextColor = new Color(0.98f, 0.93f, 0.82f, 1f);
    private static readonly Color SummaryTextColor = new Color(0.90f, 0.86f, 0.78f, 1f);
    private static readonly Color ButtonColor = new Color(0.55f, 0.38f, 0.22f, 1f);
    private static readonly Color ButtonTextColor = new Color(0.98f, 0.93f, 0.82f, 1f);
    private static readonly Color EmptySlotColor = new Color(0.72f, 0.66f, 0.58f, 1f);
    private static readonly Color RowColor = new Color(0.10f, 0.07f, 0.05f, 0.94f);
    private static readonly Color ActiveSlotColor = new Color(0.78f, 0.62f, 0.28f, 1f);
    private static readonly Color SavedSlotColor = new Color(0.22f, 0.42f, 0.24f, 0.96f);
    private static readonly Color StatusTextColor = new Color(0.90f, 0.86f, 0.78f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.78f);

    private readonly SlotRowUi[] slotRows = new SlotRowUi[SaveSystem.SlotCount];

    private GameObject root;
    private Text statusText;
    private int lastSavedSlotIndex = SaveSession.InvalidSlot;
    private Player player;
    private Inventory inventory;
    private Action<string> onSaved;
    private Action onClosed;

    public static GameplaySaveSlotPanel Ensure(Transform host)
    {
        if (host == null)
            return null;

        GameplaySaveSlotPanel existing = host.GetComponentInChildren<GameplaySaveSlotPanel>(true);
        if (existing != null)
            return existing;

        GameObject panelHost = new GameObject("GameplaySaveSlotPanel", typeof(RectTransform), typeof(GameplaySaveSlotPanel));
        panelHost.transform.SetParent(host, false);
        Stretch(panelHost.GetComponent<RectTransform>());
        return panelHost.GetComponent<GameplaySaveSlotPanel>();
    }

    public bool IsVisible => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            BuildUi();
    }

    public void Show(Player targetPlayer, Inventory targetInventory, Action<string> savedCallback, Action closedCallback)
    {
        if (root == null)
            BuildUi();

        player = targetPlayer;
        inventory = targetInventory;
        onSaved = savedCallback;
        onClosed = closedCallback;
        lastSavedSlotIndex = SaveSession.InvalidSlot;

        if (statusText != null)
            statusText.text = string.Empty;

        RefreshRows();
        transform.SetAsLastSibling();
        root.transform.SetAsLastSibling();
        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        onClosed?.Invoke();
        onClosed = null;
    }

    private void RefreshRows()
    {
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            SaveSlotSummary summary = SaveSystem.GetSlotSummary(i);
            SlotRowUi row = slotRows[i];
            if (row == null)
                continue;

            bool isActiveSlot = SaveSession.HasValidActiveSlot && i == SaveSession.ActiveSlot;
            row.titleText.text = isActiveSlot ? $"存档 {i + 1}（当前）" : $"存档 {i + 1}";

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

            SetButtonLabel(row.saveButton, summary.hasSave ? "覆盖保存" : "保存");
            row.saveButton.onClick.RemoveAllListeners();
            int slotIndex = i;
            row.saveButton.onClick.AddListener(() => SaveToSlot(slotIndex));

            Image rowImage = row.titleText.transform.parent.GetComponent<Image>();
            if (rowImage != null)
            {
                if (i == lastSavedSlotIndex)
                    rowImage.color = SavedSlotColor;
                else if (isActiveSlot)
                    rowImage.color = ActiveSlotColor;
                else
                    rowImage.color = RowColor;
            }
        }
    }

    private void SaveToSlot(int slotIndex)
    {
        if (player == null || inventory == null)
        {
            if (statusText != null)
                statusText.text = "保存失败：缺少玩家数据";
            return;
        }

        bool saved = SaveSystem.Save(slotIndex, player, inventory);
        if (saved)
        {
            lastSavedSlotIndex = slotIndex;
            string message = $"已保存到存档 {slotIndex + 1}";
            if (statusText != null)
                statusText.text = message;
            onSaved?.Invoke(message);
            RefreshRows();
            return;
        }

        if (statusText != null)
            statusText.text = "保存失败";
        onSaved?.Invoke("保存失败");
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

        Text titleText = CreateText(card.transform, "Title", "选择保存位置", 34, TextAnchor.MiddleCenter, FontStyle.Bold, TitleTextColor);
        PlaceTop(titleText.rectTransform, -24f, 48f);

        float firstRowY = 0.78f;
        const float rowStep = 0.22f;
        for (int i = 0; i < SaveSystem.SlotCount; i++)
            slotRows[i] = CreateSlotRow(card.transform, i, firstRowY - rowStep * i);

        Button backButton = CreateButton(card.transform, "BackButton", "返回", 0.06f);
        backButton.onClick.AddListener(Hide);

        statusText = CreateText(card.transform, "StatusText", string.Empty, 18, TextAnchor.MiddleCenter, FontStyle.Normal, StatusTextColor);
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 58f);
        statusRect.sizeDelta = new Vector2(-48f, 28f);

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

        Text titleText = CreateText(rowObject.transform, "Title", $"存档 {slotIndex + 1}", 24, TextAnchor.UpperLeft, FontStyle.Bold, TitleTextColor);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(18f, -10f);
        titleRect.sizeDelta = new Vector2(-150f, 24f);

        Text summaryText = CreateText(rowObject.transform, "Summary", "空存档", 20, TextAnchor.UpperLeft, FontStyle.Normal, EmptySlotColor);
        RectTransform summaryRect = summaryText.rectTransform;
        summaryRect.anchorMin = new Vector2(0f, 0f);
        summaryRect.anchorMax = new Vector2(1f, 1f);
        summaryRect.offsetMin = new Vector2(18f, 12f);
        summaryRect.offsetMax = new Vector2(-140f, -34f);
        summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        summaryText.verticalOverflow = VerticalWrapMode.Overflow;

        Button saveButton = CreateInlineButton(rowObject.transform, "SaveButton", "保存", new Vector2(-18f, 0f));

        return new SlotRowUi
        {
            titleText = titleText,
            summaryText = summaryText,
            saveButton = saveButton,
        };
    }

    private static Button CreateInlineButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(112f, 40f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject.transform, "Label", label, 18, TextAnchor.MiddleCenter, FontStyle.Bold, ButtonTextColor);
        Stretch(text.rectTransform);
        return button;
    }

    private static Button CreateButton(Transform parent, string name, string label, float anchorY)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, anchorY);
        buttonRect.anchorMax = new Vector2(0.5f, anchorY);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(220f, 48f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;

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
        public Button saveButton;
    }
}
