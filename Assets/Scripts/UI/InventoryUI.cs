using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class InventoryUI : MonoBehaviour
{
    private const string PanelName = "InventoryPanel";
    private const string GridName = "SlotGrid";
    private const string DetailName = "DetailText";
    private const string DragIconName = "DragIcon";

    [Header("Target")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private Player player;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [SerializeField] private KeyCode alternateToggleKey = KeyCode.B;
    [SerializeField] private bool openOnStart;

    [Header("Sprites")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite slotSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite closeIconSprite;

    [Header("Layout")]
    [SerializeField] private int columns = 5;
    [SerializeField] private int rows = 4;
    [SerializeField] private Vector2 panelSize = new Vector2(520f, 560f);
    [SerializeField] private Vector2 slotSize = new Vector2(76f, 76f);
    [SerializeField] private Vector2 slotSpacing = new Vector2(10f, 10f);
    [SerializeField] private float longPressSeconds = 0.35f;

    private readonly List<SlotView> slots = new List<SlotView>();
    private RectTransform rootRect;
    private GameObject panelObject;
    private RectTransform panelRect;
    private RectTransform gridRect;
    private RectTransform dragIconRect;
    private Image dragIcon;
    private TextMeshProUGUI detailText;
    private bool isOpen;
    private bool leftPressActive;
    private bool isDragging;
    private float leftPressStartedAt;
    private int selectedSlotIndex = -1;
    private int pressedSlotIndex = -1;
    private int hoveredSlotIndex = -1;
    private int dragSourceIndex = -1;

    private sealed class SlotView
    {
        public Image Background;
        public Image Icon;
        public TextMeshProUGUI CountText;
    }

    private void Awake()
    {
        ResolveTargets();
        BuildOrBindView();
        SetOpen(openOnStart);
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.Changed += Refresh;

        if (player != null)
            player.EquipmentChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.Changed -= Refresh;

        if (player != null)
            player.EquipmentChanged -= Refresh;

        if (isOpen)
            GameplayCursor.LockForGameplay();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(alternateToggleKey))
            SetOpen(!isOpen);

        if (!isOpen)
            return;

        if (leftPressActive && !isDragging && Time.unscaledTime - leftPressStartedAt >= longPressSeconds)
            BeginDrag();

        if (isDragging)
            UpdateDragIconPosition();

        if (leftPressActive && !Input.GetMouseButton(0))
            EndLeftPress(hoveredSlotIndex);
    }

    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    public void SetOpen(bool value)
    {
        isOpen = value;

        if (panelObject == null)
            ResolveCanonicalPanel(removeDuplicates: false);

        ApplyPanelActiveState();

        if (!isOpen)
            ClearDrag();

        if (isOpen)
            Refresh();

        ApplyCursorState();
    }

    private void ApplyPanelActiveState()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name != PanelName)
                continue;

            bool visible = isOpen && panelObject != null && child.gameObject == panelObject;
            child.gameObject.SetActive(visible);
        }
    }

    private void ApplyCursorState()
    {
        if (isOpen)
            GameplayCursor.UnlockForUI();
        else
            GameplayCursor.LockForGameplay();
    }

    public void Initialize(Inventory targetInventory, Player targetPlayer)
    {
        if (inventory != null)
            inventory.Changed -= Refresh;

        if (player != null)
            player.EquipmentChanged -= Refresh;

        inventory = targetInventory;
        player = targetPlayer;
        ResolveTargets();

        if (isActiveAndEnabled && inventory != null)
            inventory.Changed += Refresh;

        if (isActiveAndEnabled && player != null)
            player.EquipmentChanged += Refresh;

        Refresh();
    }

    public void Refresh()
    {
        if (slots.Count == 0)
            BindSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            SlotView slot = slots[i];
            InventoryEntry entry = inventory != null ? inventory.GetEntryAt(i) : null;
            ItemSO item = entry != null ? entry.Item : null;

            bool hasItem = item != null && entry.Amount > 0;
            bool hiddenByDrag = isDragging && i == dragSourceIndex;
            bool equipped = hasItem && IsEquipped(item);
            slot.Icon.enabled = hasItem && !hiddenByDrag;
            slot.Icon.sprite = hasItem ? item.icon : null;
            slot.CountText.text = BuildSlotMarker(entry, equipped, hiddenByDrag);

            if (slot.Background != null)
            {
                if (i == selectedSlotIndex)
                    slot.Background.color = new Color(1f, 0.92f, 0.68f, 1f);
                else if (equipped)
                    slot.Background.color = new Color(0.76f, 1f, 0.72f, 1f);
                else if (i == hoveredSlotIndex && isDragging)
                    slot.Background.color = new Color(0.92f, 1f, 0.72f, 1f);
                else
                    slot.Background.color = Color.white;
            }
        }

        UpdateDetail(selectedSlotIndex);
    }

    public void HandleSlotPointerDown(int index, PointerEventData eventData)
    {
        if (!isOpen || eventData == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            selectedSlotIndex = index;
            pressedSlotIndex = index;
            leftPressStartedAt = Time.unscaledTime;
            leftPressActive = true;
            UpdateDetail(index);
            Refresh();
        }
    }

    public void HandleSlotPointerUp(int index, PointerEventData eventData)
    {
        if (!isOpen || eventData == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            UseSlot(index);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
            EndLeftPress(hoveredSlotIndex >= 0 ? hoveredSlotIndex : index);
    }

    public void HandleSlotPointerEnter(int index)
    {
        hoveredSlotIndex = index;
        if (isDragging)
            Refresh();
    }

    public void HandleSlotPointerExit(int index)
    {
        if (hoveredSlotIndex == index)
            hoveredSlotIndex = -1;

        if (isDragging)
            Refresh();
    }

    private void BuildOrBindView()
    {
        rootRect = GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        if (!BindExistingView())
        {
            CreateDefaultView();
            BindExistingView();
        }

        ResolveCanonicalPanel(removeDuplicates: true);
        ConfigureCloseButton();
        BindSlots();
    }

    private void ResolveCanonicalPanel(bool removeDuplicates)
    {
        GameObject canonical = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name != PanelName)
                continue;

            if (child.Find(GridName) == null || child.Find(DetailName) == null)
                continue;

            if (canonical == null)
            {
                canonical = child.gameObject;
                continue;
            }

            if (!removeDuplicates)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        if (canonical != null)
            panelObject = canonical;
    }

    private bool BindExistingView()
    {
        Transform panel = transform.Find(PanelName);
        if (panel == null)
            return false;

        panelObject = panel.gameObject;
        panelRect = panel as RectTransform;
        gridRect = panel.Find(GridName) as RectTransform;
        detailText = GetComponentInChild<TextMeshProUGUI>(panel, DetailName);

        Transform drag = transform.Find(DragIconName);
        dragIconRect = drag as RectTransform;
        dragIcon = drag != null ? drag.GetComponent<Image>() : null;

        return panelRect != null && gridRect != null && detailText != null && dragIconRect != null && dragIcon != null;
    }

    private void CreateDefaultView()
    {
        panelObject = CreateChild(gameObject, PanelName, typeof(RectTransform), typeof(Image));
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-36f, 0f);
        panelRect.sizeDelta = panelSize;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.color = panelSprite != null ? Color.white : new Color(0.25f, 0.16f, 0.1f, 0.96f);

        TextMeshProUGUI title = CreateText(panelObject, "Title", "Inventory", 34f, TextAlignmentOptions.Left);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(28f, -24f);
        titleRect.sizeDelta = new Vector2(-92f, 48f);
        title.color = new Color(0.18f, 0.1f, 0.045f, 1f);

        CreateCloseButton(panelObject);
        CreateGrid(panelObject);

        detailText = CreateText(panelObject, DetailName, string.Empty, 19f, TextAlignmentOptions.Left);
        RectTransform detailRect = detailText.rectTransform;
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 0f);
        detailRect.pivot = new Vector2(0.5f, 0f);
        detailRect.anchoredPosition = new Vector2(28f, 24f);
        detailRect.sizeDelta = new Vector2(-56f, 86f);
        detailText.color = new Color(0.18f, 0.1f, 0.045f, 1f);
        detailText.enableWordWrapping = true;

        CreateDragIcon();
    }

    private void CreateCloseButton(GameObject parent)
    {
        GameObject buttonObject = CreateChild(parent, "CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-24f, -22f);
        buttonRect.sizeDelta = new Vector2(48f, 48f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = buttonSprite;
        buttonImage.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        buttonImage.color = buttonSprite != null ? Color.white : new Color(0.54f, 0.32f, 0.16f, 1f);

        GameObject iconObject = CreateChild(buttonObject, "Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(20f, 20f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = closeIconSprite;
        icon.color = closeIconSprite != null ? Color.white : new Color(0.2f, 0.1f, 0.05f, 1f);
        icon.raycastTarget = false;
    }

    private void CreateGrid(GameObject parent)
    {
        GameObject gridObject = CreateChild(parent, GridName, typeof(RectTransform), typeof(GridLayoutGroup));
        gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 1f);
        gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchoredPosition = new Vector2(0f, -92f);
        gridRect.sizeDelta = new Vector2(columns * slotSize.x + (columns - 1) * slotSpacing.x, rows * slotSize.y + (rows - 1) * slotSpacing.y);

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = slotSize;
        grid.spacing = slotSpacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.UpperCenter;

        int slotCount = Mathf.Max(1, columns * rows);
        for (int i = 0; i < slotCount; i++)
            CreateSlot(gridObject, i);
    }

    private void CreateSlot(GameObject parent, int index)
    {
        GameObject slotObject = CreateChild(parent, "Slot_" + (index + 1).ToString("00"), typeof(RectTransform), typeof(Image));
        Image slotImage = slotObject.GetComponent<Image>();
        slotImage.sprite = slotSprite;
        slotImage.type = slotSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        slotImage.color = slotSprite != null ? Color.white : new Color(0.78f, 0.61f, 0.42f, 1f);

        GameObject iconObject = CreateChild(slotObject, "Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(slotSize.x - 18f, slotSize.y - 18f);

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI countText = CreateText(slotObject, "Count", string.Empty, 18f, TextAlignmentOptions.BottomRight);
        RectTransform countRect = countText.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = new Vector2(5f, 4f);
        countRect.offsetMax = new Vector2(-7f, -4f);
        countText.color = Color.white;
        countText.fontStyle = FontStyles.Bold;
        countText.raycastTarget = false;
    }

    private void CreateDragIcon()
    {
        GameObject iconObject = CreateChild(gameObject, DragIconName, typeof(RectTransform), typeof(Image));
        dragIconRect = iconObject.GetComponent<RectTransform>();
        dragIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        dragIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        dragIconRect.pivot = new Vector2(0.5f, 0.5f);
        dragIconRect.sizeDelta = new Vector2(slotSize.x - 10f, slotSize.y - 10f);

        dragIcon = iconObject.GetComponent<Image>();
        dragIcon.preserveAspect = true;
        dragIcon.raycastTarget = false;
        iconObject.SetActive(false);
    }

    private void ConfigureCloseButton()
    {
        if (panelRect == null)
            return;

        Transform close = panelRect.Find("CloseButton");
        Button button = close != null ? close.GetComponent<Button>() : null;
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(delegate { SetOpen(false); });
    }

    private void BindSlots()
    {
        slots.Clear();
        if (gridRect == null)
            return;

        int slotCount = Mathf.Max(1, columns * rows);
        for (int i = 0; i < slotCount; i++)
        {
            Transform slotTransform = gridRect.Find("Slot_" + (i + 1).ToString("00"));
            if (slotTransform == null)
                continue;

            Image background = slotTransform.GetComponent<Image>();
            Image icon = GetComponentInChild<Image>(slotTransform, "Icon");
            TextMeshProUGUI count = GetComponentInChild<TextMeshProUGUI>(slotTransform, "Count");
            InventorySlotUI events = slotTransform.GetComponent<InventorySlotUI>();
            if (events == null)
                events = slotTransform.gameObject.AddComponent<InventorySlotUI>();

            events.Configure(this, i);

            SlotView slot = new SlotView();
            slot.Background = background;
            slot.Icon = icon;
            slot.CountText = count;
            slots.Add(slot);
        }
    }

    private void UseSlot(int index)
    {
        if (inventory == null)
            return;

        ResolveTargets();
        inventory.UseItemAt(index, player);
        selectedSlotIndex = index;
        Refresh();
    }

    private void EndLeftPress(int targetSlotIndex)
    {
        if (!leftPressActive && !isDragging)
            return;

        if (isDragging)
            DropDraggedItem(targetSlotIndex);
        else if (pressedSlotIndex >= 0)
            SelectSlot(pressedSlotIndex);

        leftPressActive = false;
        pressedSlotIndex = -1;
    }

    private void SelectSlot(int index)
    {
        selectedSlotIndex = index;
        UpdateDetail(index);
        Refresh();
    }

    private void BeginDrag()
    {
        if (inventory == null || pressedSlotIndex < 0)
            return;

        InventoryEntry entry = inventory.GetEntryAt(pressedSlotIndex);
        ItemSO item = entry != null ? entry.Item : null;
        if (item == null || entry.Amount <= 0 || item.icon == null)
            return;

        isDragging = true;
        dragSourceIndex = pressedSlotIndex;
        selectedSlotIndex = pressedSlotIndex;

        if (dragIcon != null)
        {
            dragIcon.sprite = item.icon;
            dragIcon.enabled = true;
            dragIcon.gameObject.SetActive(true);
        }

        UpdateDragIconPosition();
        Refresh();
    }

    private void DropDraggedItem(int targetSlotIndex)
    {
        if (inventory != null && dragSourceIndex >= 0 && targetSlotIndex >= 0)
        {
            inventory.MoveItem(dragSourceIndex, targetSlotIndex);
            selectedSlotIndex = targetSlotIndex;
        }

        ClearDrag();
        Refresh();
    }

    private void ClearDrag()
    {
        isDragging = false;
        leftPressActive = false;
        dragSourceIndex = -1;
        pressedSlotIndex = -1;

        if (dragIcon != null)
        {
            dragIcon.enabled = false;
            dragIcon.sprite = null;
            dragIcon.gameObject.SetActive(false);
        }
    }

    private void UpdateDragIconPosition()
    {
        if (dragIconRect == null || rootRect == null)
            return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, Input.mousePosition, null, out localPoint);
        dragIconRect.anchoredPosition = localPoint;
    }

    private void UpdateDetail(int index)
    {
        if (detailText == null)
            return;

        InventoryEntry entry = inventory != null && index >= 0 ? inventory.GetEntryAt(index) : null;
        detailText.text = BuildDetailText(entry);
    }

    private void ResolveTargets()
    {
        if (player == null)
            player = Object.FindObjectOfType<Player>();

        if (inventory == null && player != null)
            inventory = player.GetComponent<Inventory>();
    }

    private string BuildDetailText(InventoryEntry entry)
    {
        ItemSO item = entry != null ? entry.Item : null;
        if (item == null || entry.Amount <= 0)
            return "Empty";

        StringBuilder builder = new StringBuilder();
        builder.Append(item.name);
        builder.Append("  x");
        builder.Append(entry.Amount);

        if (IsEquipped(item))
            builder.Append("  Equipped");

        builder.AppendLine();
        builder.Append(item.itemType == ItemType.Weapon ? "Weapon" : "Consumable");

        AppendPropertyLine(builder, item, ItemPropertyType.AttackValue, "Attack");
        AppendPropertyLine(builder, item, ItemPropertyType.HPValue, "HP");
        AppendPropertyLine(builder, item, ItemPropertyType.EnergyValue, "Energy");
        AppendPropertyLine(builder, item, ItemPropertyType.MentalValue, "Mental");
        AppendPropertyLine(builder, item, ItemPropertyType.SpeedValue, "Speed");

        if (!string.IsNullOrEmpty(item.description))
        {
            builder.AppendLine();
            builder.Append(item.description);
        }

        return builder.ToString();
    }

    private bool IsEquipped(ItemSO item)
    {
        return player != null && player.EquippedWeapon == item;
    }

    private static string BuildSlotMarker(InventoryEntry entry, bool equipped, bool hiddenByDrag)
    {
        if (hiddenByDrag || entry == null || entry.Item == null || entry.Amount <= 0)
            return string.Empty;

        if (equipped)
            return "E";

        return entry.Amount > 1 ? entry.Amount.ToString() : string.Empty;
    }

    private static void AppendPropertyLine(StringBuilder builder, ItemSO item, ItemPropertyType propertyType, string label)
    {
        int value = item != null ? item.GetPropertyValue(propertyType) : 0;
        if (value == 0)
            return;

        builder.AppendLine();
        builder.Append(label);
        builder.Append(value > 0 ? " +" : " ");
        builder.Append(value);
    }

    private static GameObject CreateChild(GameObject parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static TextMeshProUGUI CreateText(GameObject parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static T GetComponentInChild<T>(Transform parent, string childName) where T : Component
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        return child != null ? child.GetComponent<T>() : null;
    }

    private void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        panelSize.x = Mathf.Max(320f, panelSize.x);
        panelSize.y = Mathf.Max(320f, panelSize.y);
        slotSize.x = Mathf.Max(48f, slotSize.x);
        slotSize.y = Mathf.Max(48f, slotSize.y);
        longPressSeconds = Mathf.Max(0.05f, longPressSeconds);
    }
}
