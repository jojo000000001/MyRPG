using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 ESC 暂停菜单用的三槽位保存面板。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplaySaveSlotPanel : MonoBehaviour
{
    private readonly SlotRowUi[] slotRows = new SlotRowUi[SaveSystem.SlotCount];

    private GameObject root;
    private TextMeshProUGUI statusText;
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
        SheikahUiStyle.Stretch(panelHost.GetComponent<RectTransform>());
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
                    $"Lv.{summary.level}  ·  {summary.rupees}卢比  ·  {summary.questLabel}\n" +
                    $"{summary.playTimeDisplay}  ·  {summary.savedAtDisplay}";
                row.summaryText.color = SheikahUiStyle.Muted;
            }
            else
            {
                row.summaryText.text = "空存档";
                row.summaryText.color = SheikahUiStyle.Muted;
            }

            SheikahUiStyle.SetButtonLabel(row.saveButton, summary.hasSave ? "覆盖保存" : "保存");
            row.saveButton.onClick.RemoveAllListeners();
            int slotIndex = i;
            row.saveButton.onClick.AddListener(() => SaveToSlot(slotIndex));

            if (row.rowImage != null)
            {
                if (i == lastSavedSlotIndex)
                    row.rowImage.color = SheikahUiStyle.Orange;
                else if (isActiveSlot)
                    row.rowImage.color = SheikahUiStyle.Button;
                else
                    row.rowImage.color = Color.white;
            }
        }
    }

    /// <summary>把当前进度写入指定槽，并刷新槽位摘要。</summary>
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
        Sprite slotSprite = SheikahUiStyle.SlotSprite;

        root = SheikahUiStyle.CreateChild(transform, "SaveSlotOverlay").gameObject;
        SheikahUiStyle.Stretch(root.GetComponent<RectTransform>());

        Image dim = SheikahUiStyle.CreateChild(root.transform, "Dim", typeof(Image)).GetComponent<Image>();
        SheikahUiStyle.Stretch(dim.rectTransform);
        dim.color = SheikahUiStyle.Dim;
        dim.raycastTarget = true;

        GameObject card = SheikahUiStyle.CreateCard(root.transform, "SaveSlotCard", new Vector2(640f, 640f), SheikahUiStyle.PanelSprite, 2.2f);

        TextMeshProUGUI titleText = SheikahUiStyle.CreateText(
            card.transform,
            "Title",
            "选择保存位置",
            34f,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            SheikahUiStyle.Orange);
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
        SheikahUiStyle.PlaceCentered(backButton.GetComponent<RectTransform>(), 0.08f, new Vector2(220f, 48f));
        backButton.onClick.AddListener(Hide);

        statusText = SheikahUiStyle.CreateText(
            card.transform,
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
        statusRect.anchoredPosition = new Vector2(0f, 58f);
        statusRect.sizeDelta = new Vector2(-48f, 28f);

        root.SetActive(false);
    }

    private static SlotRowUi CreateSlotRow(Transform parent, int slotIndex, float anchorY, Sprite slotSprite)
    {
        GameObject rowObject = SheikahUiStyle.CreateChild(parent, $"SlotRow_{slotIndex + 1}", typeof(Image));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        SheikahUiStyle.PlaceCentered(rowRect, anchorY, new Vector2(540f, 96f));
        Image rowImage = rowObject.GetComponent<Image>();
        SheikahUiStyle.ApplySliced(rowImage, slotSprite, Color.white, 4.6f);

        TextMeshProUGUI titleText = SheikahUiStyle.CreateText(
            rowObject.transform,
            "Title",
            $"存档 {slotIndex + 1}",
            22f,
            TextAlignmentOptions.TopLeft,
            FontStyles.Bold,
            SheikahUiStyle.Text);
        RectTransform titleRect = titleText.rectTransform;
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

        Button saveButton = SheikahUiStyle.CreateButton(
            rowObject.transform,
            "SaveButton",
            "保存",
            new Vector2(112f, 40f),
            SheikahUiStyle.Orange,
            SheikahUiStyle.Text,
            slotSprite,
            6f);
        RectTransform buttonRect = saveButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-18f, 0f);
        buttonRect.sizeDelta = new Vector2(112f, 40f);

        return new SlotRowUi
        {
            rowImage = rowImage,
            titleText = titleText,
            summaryText = summaryText,
            saveButton = saveButton,
        };
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
        public Image rowImage;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI summaryText;
        public Button saveButton;
    }
}
