using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerHotbar : MonoBehaviour
{
    private const string RootName = "PlayerHotbarRoot";

    private static readonly KeyCode[] NumberKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6
    };

    private static readonly KeyCode[] KeypadKeys =
    {
        KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6
    };

    [Header("Target")]
    [SerializeField] private Player player;
    [SerializeField] private Inventory inventory;

    [Header("Prefab")]
    [SerializeField] private RectTransform hotbarRoot;

    [Header("Sprites")]
    [SerializeField] private Sprite slotSprite;

    [Header("Layout")]
    [SerializeField] private int slotCount = Inventory.DefaultHotbarSize;
    [SerializeField] private Vector2 slotSize = new Vector2(96f, 96f);
    [SerializeField] private float slotSpacing = 0f;
    [SerializeField] private float leftMargin = 0f;
    [SerializeField] private float bottomMargin = 20f;

    private readonly List<SlotView> slots = new List<SlotView>();
    private RectTransform rootRect;
    private int hoveredSlotIndex = -1;

    private static readonly Color KeyColor = new Color(0.96f, 0.90f, 0.74f, 1f);
    private static readonly Color HoverColor = new Color(1f, 0.92f, 0.68f, 1f);

    public int SlotCount => Mathf.Max(1, slotCount);

    private sealed class SlotView
    {
        public RectTransform Rect;
        public Image Background;
        public Image Icon;
        public TextMeshProUGUI CountText;
        public TextMeshProUGUI KeyText;
    }

    public void BindPlayer(Player target)
    {
        Unsubscribe();
        player = target;
        inventory = target != null ? target.GetComponent<Inventory>() : null;
        Subscribe();
        if (inventory != null)
            inventory.FillEmptyHotbarFromInventory();
        Refresh();
    }

    public bool TryAssignAtScreenPoint(Vector2 screenPoint, ItemSO item)
    {
        int slotIndex = FindSlotAtScreenPoint(screenPoint);
        if (slotIndex < 0 || item == null)
            return false;

        ResolveTargets();
        if (inventory == null)
            return false;

        inventory.SetHotbarItem(slotIndex, item);
        return true;
    }

    public void HandleSlotClick(int slotIndex)
    {
        if (GameplayPauseMenu.IsOpen || DialogueUI.IsOpen || ShopUI.IsOpen)
            return;

        UseSlot(slotIndex);
    }

    public void HandleSlotClear(int slotIndex)
    {
        if (GameplayPauseMenu.IsOpen || DialogueUI.IsOpen || ShopUI.IsOpen)
            return;

        ResolveTargets();
        if (inventory == null)
            return;

        inventory.ClearHotbarSlot(slotIndex);
    }

    public void HandleSlotPointerEnter(int slotIndex)
    {
        hoveredSlotIndex = slotIndex;
        Refresh();
    }

    public void HandleSlotPointerExit(int slotIndex)
    {
        if (hoveredSlotIndex == slotIndex)
            hoveredSlotIndex = -1;

        Refresh();
    }

    private void Awake()
    {
        ResolveTargets();
        EnsureUi();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        ResolveTargets();
        EnsureUi();
        if (inventory != null)
            inventory.FillEmptyHotbarFromInventory();
        Refresh();
    }

    private void Update()
    {
        if (GameplayPauseMenu.IsOpen || DialogueUI.IsOpen || ShopUI.IsOpen)
            return;

        int slotCount = GetActiveSlotCount();
        for (int i = 0; i < slotCount && i < NumberKeys.Length; i++)
        {
            if (Input.GetKeyDown(NumberKeys[i]) || Input.GetKeyDown(KeypadKeys[i]))
                UseSlot(i);
        }
    }

    private void EnsureUi()
    {
        if (BindExistingView())
            return;

        CreateDefaultView();
        BindExistingView();
    }

    private bool BindExistingView()
    {
        if (hotbarRoot == null)
        {
            Transform found = transform.Find(RootName);
            hotbarRoot = found as RectTransform;
        }

        if (hotbarRoot == null)
            return false;

        rootRect = hotbarRoot;
        Transform grid = hotbarRoot.Find("SlotGrid");
        if (grid == null)
            grid = hotbarRoot;

        slots.Clear();
        int count = Mathf.Max(1, slotCount);
        for (int i = 0; i < count; i++)
        {
            string slotName = "Slot_" + (i + 1).ToString("00");
            Transform slotTransform = grid.Find(slotName);
            if (slotTransform == null)
                slotTransform = hotbarRoot.Find(slotName);
            if (slotTransform == null)
                break;

            Image background = slotTransform.GetComponent<Image>();
            Transform iconTransform = slotTransform.Find("Icon");
            Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            TextMeshProUGUI countText = GetTmp(slotTransform, "Count");
            TextMeshProUGUI keyText = GetTmp(slotTransform, "Key");
            if (background == null || icon == null || countText == null || keyText == null)
                break;

            PlayerHotbarSlotUI events = slotTransform.GetComponent<PlayerHotbarSlotUI>();
            if (events == null)
                events = slotTransform.gameObject.AddComponent<PlayerHotbarSlotUI>();
            events.Configure(this, i);

            SlotView view = new SlotView();
            view.Rect = slotTransform as RectTransform;
            view.Background = background;
            view.Icon = icon;
            view.CountText = countText;
            view.KeyText = keyText;
            slots.Add(view);
        }

        return slots.Count > 0;
    }

    private void CreateDefaultView()
    {
        slots.Clear();
        int count = GetActiveSlotCount();
        float width = count * slotSize.x + Mathf.Max(0, count - 1) * slotSpacing;

        GameObject rootObject = CreateChild(gameObject, RootName, typeof(RectTransform));
        hotbarRoot = rootObject.GetComponent<RectTransform>();
        rootRect = hotbarRoot;
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.anchoredPosition = new Vector2(leftMargin, bottomMargin);
        rootRect.sizeDelta = new Vector2(width, slotSize.y);

        GameObject gridObject = CreateChild(rootObject, "SlotGrid", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        Stretch(gridRect);

        HorizontalLayoutGroup layout = gridObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = slotSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < count; i++)
            CreateSlot(gridObject, i);
    }

    private void CreateSlot(GameObject parent, int index)
    {
        GameObject slotObject = CreateChild(parent, "Slot_" + (index + 1).ToString("00"), typeof(RectTransform), typeof(Image), typeof(PlayerHotbarSlotUI));
        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.sizeDelta = slotSize;

        Image background = slotObject.GetComponent<Image>();
        background.sprite = slotSprite;
        background.type = slotSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = slotSprite != null ? Color.white : new Color(0.28f, 0.16f, 0.08f, 1f);
        background.raycastTarget = true;

        GameObject iconObject = CreateChild(slotObject, "Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(slotSize.x - 20f, slotSize.y - 20f);
        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;
        icon.canvasRenderer.cullTransparentMesh = false;

        TextMeshProUGUI count = CreateText(slotObject, "Count", string.Empty, 18f, TextAlignmentOptions.BottomRight);
        RectTransform countRect = count.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = new Vector2(6f, 4f);
        countRect.offsetMax = new Vector2(-7f, -4f);
        count.color = Color.white;
        count.fontStyle = FontStyles.Bold;

        TextMeshProUGUI key = CreateText(slotObject, "Key", (index + 1).ToString(), 16f, TextAlignmentOptions.TopLeft);
        RectTransform keyRect = key.rectTransform;
        keyRect.anchorMin = Vector2.zero;
        keyRect.anchorMax = Vector2.one;
        keyRect.offsetMin = new Vector2(8f, 4f);
        keyRect.offsetMax = new Vector2(-6f, -5f);
        key.color = KeyColor;
        key.fontStyle = FontStyles.Bold;

        PlayerHotbarSlotUI events = slotObject.GetComponent<PlayerHotbarSlotUI>();
        events.Configure(this, index);
    }

    private static TextMeshProUGUI GetTmp(Transform parent, string childName)
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private void HandleInventoryChanged()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (slots.Count == 0)
            EnsureUi();

        ResolveTargets();

        for (int i = 0; i < slots.Count; i++)
        {
            SlotView slot = slots[i];
            ItemSO item = inventory != null ? inventory.GetHotbarItem(i) : null;
            int amount = inventory != null ? inventory.CountOf(item) : 0;
            bool hasItem = item != null && amount > 0;

            slot.Icon.enabled = hasItem && item.icon != null;
            slot.Icon.sprite = hasItem ? item.icon : null;
            slot.Icon.color = Color.white;
            slot.Icon.canvasRenderer.cullTransparentMesh = false;
            slot.CountText.text = hasItem ? amount.ToString() : string.Empty;
            slot.Background.color = i == hoveredSlotIndex ? HoverColor : Color.white;
        }
    }

    private void UseSlot(int slotIndex)
    {
        ResolveTargets();
        if (inventory == null || player == null || player.IsDead)
            return;

        ItemSO item = inventory.GetHotbarItem(slotIndex);
        if (item == null)
            return;

        inventory.UseItem(item, player);
    }

    private int FindSlotAtScreenPoint(Vector2 screenPoint)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            RectTransform rect = slots[i].Rect;
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null))
                return i;
        }

        return -1;
    }

    private int GetActiveSlotCount()
    {
        if (inventory != null)
            return inventory.HotbarSize;

        return Mathf.Max(1, slotCount);
    }

    private void Subscribe()
    {
        if (inventory != null)
            inventory.Changed += HandleInventoryChanged;
    }

    private void Unsubscribe()
    {
        if (inventory != null)
            inventory.Changed -= HandleInventoryChanged;
    }

    private void ResolveTargets()
    {
        if (player == null)
            player = Player.Resolve();

        if (inventory == null && player != null)
            inventory = player.GetComponent<Inventory>();
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
        label.alignment = alignment;
        label.raycastTarget = false;
        ChineseUITmpFont.Apply(label, fontSize, FontStyles.Bold);
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void OnValidate()
    {
        slotCount = Mathf.Max(1, slotCount);
        slotSize.x = Mathf.Max(48f, slotSize.x);
        slotSize.y = Mathf.Max(48f, slotSize.y);
        slotSpacing = Mathf.Max(0f, slotSpacing);
        leftMargin = Mathf.Max(0f, leftMargin);
        bottomMargin = Mathf.Max(0f, bottomMargin);
    }
}
