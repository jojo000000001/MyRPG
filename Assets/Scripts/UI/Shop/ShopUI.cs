using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 塞尔达风格商店。界面来自预制体（结构和背包相同：面板 + 格子），运行时只负责开关和买卖。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ShopUI : MonoBehaviour
{
    private const string PanelName = "ShopPanel";
    private const string GridName = "SlotGrid";
    private const string DetailName = "DetailText";

    private static readonly Color Orange = new Color(1f, 0.55f, 0.14f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.93f, 0.86f, 1f);
    private static readonly Color RupeeColor = new Color(0.38f, 0.86f, 0.42f, 1f);
    private static readonly Color InactiveTab = new Color(0.16f, 0.20f, 0.24f, 0.95f);
    private static readonly Color RowSelectedTint = new Color(1f, 0.82f, 0.48f, 1f);

    public static ShopUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    private enum Mode
    {
        Buy,
        Sell
    }

    private struct Listing
    {
        public ItemSO Item;
        public int BuyPrice;
        public int SellPrice;
        public int OwnedAmount;
    }

    private sealed class SlotView
    {
        public Image Background;
        public Image Icon;
        public Image SelectedMark;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI PriceText;
    }

    [Header("Sprites")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite slotSprite;

    [Header("Layout")]
    [SerializeField] private int columns = 1;
    [SerializeField] private int rows = 6;
    [SerializeField] private Vector2 panelSize = new Vector2(1200f, 680f);
    [SerializeField] private Vector2 slotSize = new Vector2(528f, 76f);
    [SerializeField] private Vector2 slotSpacing = new Vector2(0f, 8f);

    private readonly List<Listing> listings = new List<Listing>();
    private readonly List<SlotView> slots = new List<SlotView>();
    private RectTransform rootRect;
    private GameObject panelObject;
    private TextMeshProUGUI rupeeText;
    private TextMeshProUGUI detailText;
    private TextMeshProUGUI quantityText;
    private TextMeshProUGUI actionButtonLabel;
    private TextMeshProUGUI previewPrice;
    private TextMeshProUGUI previewName;
    private TextMeshProUGUI previewType;
    private Image previewIcon;
    private Image buyTabImage;
    private Image sellTabImage;
    private TextMeshProUGUI buyTabLabel;
    private TextMeshProUGUI sellTabLabel;
    private Button actionButton;
    private bool buttonsBound;

    private Mode mode = Mode.Buy;
    private int selectedIndex;
    private int scrollOffset;
    private int quantity = 1;
    private int openedFrame = -1;
    private bool tradeUnlocked;
    private bool pausedTime;
    private float previousTimeScale = 1f;
    private bool lockedPlayer;
    private bool walletBound;
    private Player player;
    private Inventory inventory;

    private int VisibleSlotCount => Mathf.Max(1, columns * rows);

    public static ShopUI Ensure()
    {
        if (Instance != null)
            return Instance;

        PlayerHUD hud = PlayerHUD.Resolve();
        if (hud != null)
            return hud.EnsureShop();

        return FindObjectOfType<ShopUI>(true);
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void SetBuyMode()
    {
        SetMode(Mode.Buy);
    }

    public void SetSellMode()
    {
        SetMode(Mode.Sell);
    }

    public void DecreaseQuantity()
    {
        ChangeQuantity(-1);
    }

    public void IncreaseQuantity()
    {
        ChangeQuantity(1);
    }

    public void SelectVisibleSlot(int visibleIndex)
    {
        if (!IsOpen)
            return;

        int listingIndex = scrollOffset + visibleIndex;
        if (listingIndex < 0 || listingIndex >= listings.Count)
            return;

        selectedIndex = listingIndex;
        quantity = 1;
        Refresh();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BindExistingView();
        BindButtons();
        SetOpen(false);
    }

    private void OnEnable()
    {
        BindWallet(true);
    }

    private void OnDisable()
    {
        BindWallet(false);
        if (IsOpen)
            SetOpen(false);
    }

    private void OnDestroy()
    {
        BindWallet(false);
        if (Instance == this)
            Instance = null;

        if (IsOpen)
        {
            IsOpen = false;
            LockPlayer(false);
            SetPaused(false);
        }
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (GameplayPauseMenu.TryConsumeEscape())
        {
            Close();
            return;
        }

        UnlockTradeAfterOpenConfirmReleased();

        if (Input.GetKeyDown(KeyCode.Tab))
            SetMode(mode == Mode.Buy ? Mode.Sell : Mode.Buy);

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            MoveSelection(-columns);
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            MoveSelection(columns);
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            MoveSelection(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            MoveSelection(1);

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
            MoveSelection(scroll > 0f ? -columns : columns);

        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            ChangeQuantity(-1);
        else if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            ChangeQuantity(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            TryTrade();
    }

    private void SetOpen(bool value)
    {
        if (value)
        {
            if (panelObject == null)
            {
                Debug.LogError("ShopUI: 预制体缺少 ShopPanel。", this);
                return;
            }

            ResolveTargets();
            BindWallet(true);
            BindButtons();

            PlayerHUD hud = PlayerHUD.Resolve();
            InventoryUI inventoryUi = hud != null ? hud.InventoryUi : null;
            if (inventoryUi != null && inventoryUi.IsOpen)
                inventoryUi.SetOpen(false);

            mode = Mode.Buy;
            selectedIndex = 0;
            scrollOffset = 0;
            quantity = 1;
            openedFrame = Time.frameCount;
            tradeUnlocked = false;
            ClearUiSelection();
            IsOpen = true;
            SetPaused(true);
            LockPlayer(true);
            RebuildListings();
            Refresh();
            panelObject.SetActive(true);
            transform.SetAsLastSibling();
            GameplayCursor.UnlockForUI();
            return;
        }

        if (!IsOpen)
        {
            if (panelObject != null)
                panelObject.SetActive(false);
            LockPlayer(false);
            return;
        }

        IsOpen = false;
        if (panelObject != null)
            panelObject.SetActive(false);

        LockPlayer(false);
        SetPaused(false);
    }

    private void BindExistingView()
    {
        rootRect = GetComponent<RectTransform>();
        Transform panel = transform.Find(PanelName);
        if (panel == null)
            return;

        panelObject = panel.gameObject;
        rupeeText = FindNamed<TextMeshProUGUI>(panel, "Rupees");
        detailText = FindNamed<TextMeshProUGUI>(panel, DetailName);
        quantityText = FindNamed<TextMeshProUGUI>(panel, "Quantity");
        previewPrice = FindNamed<TextMeshProUGUI>(panel, "Price");
        previewName = FindNamed<TextMeshProUGUI>(panel, "PreviewName");
        previewType = FindNamed<TextMeshProUGUI>(panel, "PreviewType");
        previewIcon = FindNamed<Image>(panel, "PreviewIcon");
        actionButton = FindNamed<Button>(panel, "ActionButton");
        if (actionButton != null)
            actionButtonLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();

        Button buyTab = FindNamed<Button>(panel, "BuyTab");
        Button sellTab = FindNamed<Button>(panel, "SellTab");
        if (buyTab != null)
        {
            buyTabImage = buyTab.GetComponent<Image>();
            buyTabLabel = buyTab.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (sellTab != null)
        {
            sellTabImage = sellTab.GetComponent<Image>();
            sellTabLabel = sellTab.GetComponentInChildren<TextMeshProUGUI>();
        }

        Transform grid = panel.Find(GridName);
        if (grid == null)
        {
            Transform list = panel.Find("ItemList");
            if (list != null)
                grid = list.Find(GridName);
        }

        BindSlots(grid);
    }

    private void BindSlots(Transform grid)
    {
        slots.Clear();
        if (grid == null)
            return;

        int slotCount = VisibleSlotCount;
        for (int i = 0; i < slotCount; i++)
        {
            Transform slotTransform = grid.Find("Slot_" + (i + 1).ToString("00"));
            if (slotTransform == null)
                continue;

            ShopSlotUI events = slotTransform.GetComponent<ShopSlotUI>();
            if (events == null)
                events = slotTransform.gameObject.AddComponent<ShopSlotUI>();
            events.Configure(this, i);

            slots.Add(new SlotView
            {
                Background = slotTransform.GetComponent<Image>(),
                Icon = FindNamed<Image>(slotTransform, "Icon"),
                SelectedMark = FindNamed<Image>(slotTransform, "SelectedMark"),
                NameText = FindNamed<TextMeshProUGUI>(slotTransform, "Name"),
                PriceText = FindNamed<TextMeshProUGUI>(slotTransform, "Count"),
            });
        }
    }

    private void BindButtons()
    {
        if (buttonsBound || panelObject == null)
            return;

        buttonsBound = true;
        Transform panel = panelObject.transform;
        Button closeButton = FindNamed<Button>(panel, "CloseButton");
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        Button backButton = FindNamed<Button>(panel, "BackButton");
        if (backButton != null)
            backButton.onClick.AddListener(Close);

        Button buyTab = FindNamed<Button>(panel, "BuyTab");
        if (buyTab != null)
            buyTab.onClick.AddListener(SetBuyMode);

        Button sellTab = FindNamed<Button>(panel, "SellTab");
        if (sellTab != null)
            sellTab.onClick.AddListener(SetSellMode);

        Button qtyMinus = FindNamed<Button>(panel, "QtyMinus");
        if (qtyMinus != null)
            qtyMinus.onClick.AddListener(DecreaseQuantity);

        Button qtyPlus = FindNamed<Button>(panel, "QtyPlus");
        if (qtyPlus != null)
            qtyPlus.onClick.AddListener(IncreaseQuantity);

        if (actionButton != null)
            actionButton.onClick.AddListener(TryTrade);

        DisableButtonNavigation(panel);
    }

    private void SetMode(Mode next)
    {
        if (!IsOpen || mode == next)
            return;

        mode = next;
        selectedIndex = 0;
        scrollOffset = 0;
        quantity = 1;
        RebuildListings();
        Refresh();
    }

    private void MoveSelection(int delta)
    {
        if (listings.Count <= 0)
            return;

        selectedIndex = Mathf.Clamp(selectedIndex + delta, 0, listings.Count - 1);
        EnsureSelectionVisible();
        quantity = 1;
        Refresh();
    }

    private void EnsureSelectionVisible()
    {
        int visible = VisibleSlotCount;
        if (selectedIndex < scrollOffset)
            scrollOffset = selectedIndex;
        else if (selectedIndex >= scrollOffset + visible)
            scrollOffset = selectedIndex - visible + 1;

        int maxOffset = Mathf.Max(0, listings.Count - visible);
        scrollOffset = Mathf.Clamp(scrollOffset, 0, maxOffset);
        scrollOffset = (scrollOffset / columns) * columns;
        scrollOffset = Mathf.Clamp(scrollOffset, 0, maxOffset);
    }

    private void ChangeQuantity(int delta)
    {
        if (!IsOpen)
            return;

        quantity = Mathf.Clamp(quantity + delta, 1, GetMaxQuantity());
        RefreshPreview();
    }

    private int GetMaxQuantity()
    {
        if (!TryGetSelected(out Listing listing))
            return 1;

        if (mode == Mode.Sell)
            return Mathf.Max(1, listing.OwnedAmount);

        return 99;
    }

    private void TryTrade()
    {
        UnlockTradeAfterOpenConfirmReleased();
        if (!IsOpen || !tradeUnlocked)
            return;

        if (!TryGetSelected(out Listing listing) || listing.Item == null || player == null || inventory == null)
            return;

        int count = Mathf.Clamp(quantity, 1, GetMaxQuantity());
        if (mode == Mode.Buy)
            TryBuy(listing, count);
        else
            TrySell(listing, count);
    }

    private void TryBuy(Listing listing, int count)
    {
        int cost = listing.BuyPrice * count;
        if (!player.TrySpendRupees(cost))
        {
            SetStatus("卢比不足。");
            return;
        }

        if (!inventory.AddItem(listing.Item, count))
        {
            player.AddRupees(cost);
            SetStatus("背包已满。");
            return;
        }

        quantity = 1;
        RebuildListings();
        Refresh();
        SetStatus("买下了 " + listing.Item.name + " x" + count + "。");
    }

    private void TrySell(Listing listing, int count)
    {
        if (player.EquippedWeapon == listing.Item)
            player.UnequipWeapon();

        if (!inventory.RemoveItem(listing.Item, count))
        {
            SetStatus("没有可以出售的物品。");
            return;
        }

        player.AddRupees(listing.SellPrice * count);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, listings.Count - 2));
        quantity = 1;
        RebuildListings();
        if (selectedIndex >= listings.Count)
            selectedIndex = Mathf.Max(0, listings.Count - 1);
        Refresh();
        SetStatus("出售了 " + listing.Item.name + " x" + count + "。");
    }

    private void RebuildListings()
    {
        listings.Clear();
        ItemCatalog catalog = ItemCatalog.EnsureAvailable();

        if (mode == Mode.Buy)
        {
            for (int i = 0; i < KnightShopCatalog.Offers.Length; i++)
            {
                ShopOffer offer = KnightShopCatalog.Offers[i];
                ItemSO item = catalog != null ? catalog.GetItem(offer.ItemId) : null;
                if (item == null)
                    continue;

                listings.Add(new Listing
                {
                    Item = item,
                    BuyPrice = offer.BuyPrice,
                    SellPrice = offer.SellPrice,
                    OwnedAmount = inventory != null ? inventory.CountItem(item) : 0,
                });
            }

            return;
        }

        if (inventory == null)
            return;

        IReadOnlyList<InventoryEntry> entries = inventory.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null || entry.Item == null || entry.Amount <= 0)
                continue;

            listings.Add(new Listing
            {
                Item = entry.Item,
                BuyPrice = 0,
                SellPrice = KnightShopCatalog.GetSellPrice(entry.Item),
                OwnedAmount = entry.Amount,
            });
        }
    }

    private bool TryGetSelected(out Listing listing)
    {
        if (selectedIndex < 0 || selectedIndex >= listings.Count)
        {
            listing = default;
            return false;
        }

        listing = listings[selectedIndex];
        return true;
    }

    private void Refresh()
    {
        RefreshTabs();
        RefreshSlots();
        RefreshPreview();
        RefreshWallet();
    }

    private void RefreshTabs()
    {
        if (buyTabImage != null)
            buyTabImage.color = mode == Mode.Buy ? Orange : InactiveTab;
        if (sellTabImage != null)
            sellTabImage.color = mode == Mode.Sell ? Orange : InactiveTab;
        if (buyTabLabel != null)
            buyTabLabel.color = mode == Mode.Buy ? Color.black : TextColor;
        if (sellTabLabel != null)
            sellTabLabel.color = mode == Mode.Sell ? Color.black : TextColor;
        if (actionButtonLabel != null)
            actionButtonLabel.text = mode == Mode.Buy ? "购买" : "出售";
    }

    private void RefreshSlots()
    {
        EnsureSelectionVisible();
        for (int i = 0; i < slots.Count; i++)
        {
            SlotView slot = slots[i];
            int listingIndex = scrollOffset + i;
            bool active = listingIndex >= 0 && listingIndex < listings.Count;
            bool selected = active && listingIndex == selectedIndex;
            Listing listing = active ? listings[listingIndex] : default;

            if (slot.Background != null)
                slot.Background.color = selected ? RowSelectedTint : Color.white;

            if (slot.SelectedMark != null)
                slot.SelectedMark.enabled = selected;

            if (slot.Icon != null)
            {
                slot.Icon.sprite = active && listing.Item != null ? listing.Item.icon : null;
                slot.Icon.color = slot.Icon.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
                slot.Icon.enabled = true;
            }

            if (slot.NameText != null)
            {
                slot.NameText.text = active && listing.Item != null ? listing.Item.name : string.Empty;
                slot.NameText.color = selected ? Orange : TextColor;
                ChineseUITmpFont.Apply(slot.NameText, slot.NameText.fontSize, selected ? FontStyles.Bold : FontStyles.Normal);
            }

            if (slot.PriceText != null)
            {
                if (!active || listing.Item == null)
                    slot.PriceText.text = string.Empty;
                else
                    slot.PriceText.text = "◆ " + (mode == Mode.Sell
                        ? listing.OwnedAmount + " · " + listing.SellPrice
                        : listing.BuyPrice.ToString());
                slot.PriceText.color = RupeeColor;
                ChineseUITmpFont.Apply(slot.PriceText, slot.PriceText.fontSize, FontStyles.Bold);
            }
        }
    }

    private void RefreshPreview()
    {
        if (!TryGetSelected(out Listing listing) || listing.Item == null)
        {
            if (previewName != null)
                previewName.text = "没有商品";
            if (previewType != null)
                previewType.text = string.Empty;
            if (detailText != null)
                detailText.text = mode == Mode.Sell ? "背包是空的。" : string.Empty;
            if (previewPrice != null)
                previewPrice.text = string.Empty;
            if (previewIcon != null)
            {
                previewIcon.sprite = null;
                previewIcon.color = new Color(1f, 1f, 1f, 0.08f);
            }

            if (quantityText != null)
                quantityText.text = "1";
            if (actionButton != null)
                actionButton.interactable = false;
            return;
        }

        quantity = Mathf.Clamp(quantity, 1, GetMaxQuantity());
        if (previewName != null)
        {
            previewName.text = listing.Item.name;
            ChineseUITmpFont.Apply(previewName, previewName.fontSize, FontStyles.Bold);
        }

        if (previewType != null)
        {
            previewType.text = ItemSO.GetItemTypeDisplayName(listing.Item.itemType);
            ChineseUITmpFont.Apply(previewType, previewType.fontSize);
        }

        if (previewIcon != null)
        {
            previewIcon.sprite = listing.Item.icon;
            previewIcon.color = listing.Item.icon != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
        }

        if (detailText != null)
        {
            detailText.text = BuildDetailText(listing);
            ChineseUITmpFont.Apply(detailText, detailText.fontSize);
        }

        int unitPrice = mode == Mode.Buy ? listing.BuyPrice : listing.SellPrice;
        if (previewPrice != null)
        {
            previewPrice.text = "◆  " + (unitPrice * quantity);
            previewPrice.color = RupeeColor;
            ChineseUITmpFont.Apply(previewPrice, previewPrice.fontSize, FontStyles.Bold);
        }

        if (quantityText != null)
        {
            quantityText.text = quantity.ToString();
            ChineseUITmpFont.Apply(quantityText, quantityText.fontSize, FontStyles.Bold);
        }

        if (actionButton != null)
            actionButton.interactable = true;
    }

    private string BuildDetailText(Listing listing)
    {
        var builder = new System.Text.StringBuilder();
        AppendStat(builder, "攻击", listing.Item.GetPropertyValue(ItemPropertyType.AttackValue));
        AppendStat(builder, "盾牌耐久", listing.Item.GetPropertyValue(ItemPropertyType.ShieldDurability));
        AppendStat(builder, "生命", listing.Item.GetPropertyValue(ItemPropertyType.HPValue));
        AppendStat(builder, "生命上限", listing.Item.GetPropertyValue(ItemPropertyType.MaxHPValue));
        AppendStat(builder, "精力", listing.Item.GetPropertyValue(ItemPropertyType.EnergyValue));
        AppendStat(builder, "精神", listing.Item.GetPropertyValue(ItemPropertyType.MentalValue));
        AppendStat(builder, "速度", listing.Item.GetPropertyValue(ItemPropertyType.SpeedValue));
        if (!string.IsNullOrEmpty(listing.Item.description))
        {
            builder.AppendLine();
            builder.Append(listing.Item.description);
        }

        return builder.ToString();
    }

    private static void AppendStat(System.Text.StringBuilder builder, string label, int value)
    {
        if (value == 0)
            return;

        builder.AppendLine();
        builder.Append(label);
        builder.Append(value > 0 ? " +" : " ");
        builder.Append(value);
    }

    private void RefreshWallet()
    {
        if (rupeeText == null)
            return;

        int amount = player != null ? player.Rupees : 0;
        rupeeText.text = "◆  " + amount + " 卢比";
        rupeeText.color = RupeeColor;
        ChineseUITmpFont.Apply(rupeeText, rupeeText.fontSize, FontStyles.Bold);
    }

    private void SetStatus(string message)
    {
        if (detailText == null)
            return;

        if (string.IsNullOrEmpty(message))
            return;

        detailText.text = message;
        ChineseUITmpFont.Apply(detailText, detailText.fontSize);
    }

    private void BindWallet(bool bind)
    {
        ResolveTargets();
        if (player == null)
            return;

        if (bind)
        {
            if (walletBound)
                return;

            player.WalletChanged += OnWalletChanged;
            walletBound = true;
            return;
        }

        if (!walletBound)
            return;

        player.WalletChanged -= OnWalletChanged;
        walletBound = false;
    }

    private void OnWalletChanged()
    {
        if (IsOpen)
            RefreshWallet();
    }

    private void UnlockTradeAfterOpenConfirmReleased()
    {
        if (tradeUnlocked || !IsOpen)
            return;

        if (Time.frameCount <= openedFrame)
            return;

        if (IsConfirmHeld())
            return;

        tradeUnlocked = true;
        ClearUiSelection();
    }

    private static bool IsConfirmHeld()
    {
        return Input.GetKey(KeyCode.Space)
            || Input.GetKey(KeyCode.Return)
            || Input.GetKey(KeyCode.KeypadEnter);
    }

    private static void ClearUiSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private static void DisableButtonNavigation(Transform panel)
    {
        if (panel == null)
            return;

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Navigation navigation = buttons[i].navigation;
            navigation.mode = Navigation.Mode.None;
            buttons[i].navigation = navigation;
        }
    }

    private void ResolveTargets()
    {
        if (player == null)
            player = Player.Resolve();
        if (inventory == null && player != null)
            inventory = player.GetComponent<Inventory>();
    }

    private void SetPaused(bool pause)
    {
        if (pause)
        {
            if (pausedTime)
                return;

            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            pausedTime = true;
            GameplayCursor.UnlockForUI();
            return;
        }

        if (!pausedTime)
            return;

        pausedTime = false;
        if (!GameplayPauseMenu.IsOpen)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        PlayerHUD hud = PlayerHUD.Resolve();
        if (hud != null && hud.IsInventoryOpen)
            return;

        if (!GameplayPauseMenu.IsOpen && !DialogueUI.IsOpen)
            GameplayCursor.LockForGameplay();
    }

    private void LockPlayer(bool locked)
    {
        Player target = Player.Resolve();
        if (locked)
        {
            if (target != null && !lockedPlayer)
            {
                target.SetConversationLocked(true);
                lockedPlayer = true;
            }
        }
        else if (lockedPlayer)
        {
            target?.SetConversationLocked(false);
            lockedPlayer = false;
        }
    }

    private static T FindNamed<T>(Transform root, string name) where T : Component
    {
        if (root == null)
            return null;

        if (root.name == name)
        {
            T self = root.GetComponent<T>();
            if (self != null)
                return self;
        }

        Transform child = root.Find(name);
        if (child != null)
        {
            T component = child.GetComponent<T>();
            if (component != null)
                return component;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            T nested = FindNamed<T>(root.GetChild(i), name);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
