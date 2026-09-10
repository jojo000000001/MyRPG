using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class InventoryEntry
{
    [SerializeField] private ItemSO item;
    [SerializeField] private int amount = 1;

    public ItemSO Item => item;
    public int Amount => amount;

    public InventoryEntry(ItemSO item, int amount)
    {
        this.item = item;
        this.amount = Mathf.Max(0, amount);
    }

    public void Add(int value)
    {
        amount = Mathf.Max(0, amount + value);
    }
}

[DisallowMultipleComponent]
public sealed class Inventory : MonoBehaviour
{
    public const int DefaultHotbarSize = 6;

    [SerializeField] private int capacity = 20;
    [SerializeField] private List<InventoryEntry> entries = new List<InventoryEntry>();
    [SerializeField] private int circularInsertCursor;
    [SerializeField] private int[] hotbarItemIds = new int[DefaultHotbarSize];

    public event Action Changed;

    public int Capacity => Mathf.Max(1, capacity);
    public int HotbarSize => Mathf.Max(1, hotbarItemIds != null ? hotbarItemIds.Length : DefaultHotbarSize);
    public IReadOnlyList<InventoryEntry> Entries => entries;
    public int OccupiedSlotCount => CountOccupiedSlots();
    public int OccupiedBackpackSlotCount => CountOccupiedBackpackSlots();

    public bool AddItem(ItemSO item, int amount = 1, bool insertAtFront = false)
    {
        EnsureSlotCapacity();

        if (item == null || amount <= 0)
            return false;

        bool added = insertAtFront
            ? TryInsertAtFront(item, amount)
            : TryAddSequential(item, amount);

        if (!added)
            return false;

        TryAssignToFirstEmptyHotbar(item);
        NotifyChanged();
        return true;
    }

    public int EnqueueItemsViaCircularQueue(IEnumerable<ItemSO> items, int amountEach = 1, bool clearExisting = false)
    {
        EnsureSlotCapacity();

        if (items == null)
            return 0;

        if (clearExisting)
            ClearInternal();

        var pendingItems = new CircularQueue<ItemSO>(Capacity);
        foreach (ItemSO item in items)
        {
            if (item == null)
                continue;

            if (!pendingItems.TryEnqueue(item))
                break;
        }

        int added = 0;
        while (pendingItems.TryDequeue(out ItemSO item))
        {
            if (AddItem(item, amountEach))
                added++;
        }

        return added;
    }

    public InventoryEntry GetEntryAt(int index)
    {
        EnsureSlotCapacity();
        return IsValidIndex(index) ? entries[index] : null;
    }

    public bool MoveItem(int fromIndex, int toIndex)
    {
        EnsureSlotCapacity();

        if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex) || fromIndex == toIndex)
            return false;

        InventoryEntry fromEntry = entries[fromIndex];
        if (fromEntry == null || fromEntry.Item == null || fromEntry.Amount <= 0)
            return false;

        InventoryEntry toEntry = entries[toIndex];
        if (toEntry != null && IsSameItem(toEntry.Item, fromEntry.Item))
        {
            toEntry.Add(fromEntry.Amount);
            entries[fromIndex] = null;
        }
        else
        {
            entries[fromIndex] = toEntry;
            entries[toIndex] = fromEntry;
        }

        NotifyChanged();
        return true;
    }

    public int CountItem(ItemSO item)
    {
        InventoryEntry entry = FindEntry(item);
        return entry != null && entry.Amount > 0 ? entry.Amount : 0;
    }

    public bool RemoveItem(ItemSO item, int amount = 1)
    {
        EnsureSlotCapacity();

        if (item == null || amount <= 0)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (!IsSameItem(entry != null ? entry.Item : null, item) || entry.Amount < amount)
                continue;

            entry.Add(-amount);
            if (entry.Amount <= 0)
            {
                ItemSO removedItem = entry.Item;
                entries[i] = null;
                ClearHotbarBindingsForItem(removedItem);
                CompactEntriesFrom(i);
            }

            NotifyChanged();
            return true;
        }

        return false;
    }

    public bool UseItemAt(int index, Player player)
    {
        EnsureSlotCapacity();

        if (!IsValidIndex(index))
            return false;

        InventoryEntry entry = entries[index];
        if (entry == null || entry.Item == null || entry.Amount <= 0)
            return false;

        if (IsWeaponItem(entry.Item))
        {
            if (player == null)
                return false;

            if (player.EquippedWeapon == entry.Item)
            {
                player.UnequipWeapon();
                return true;
            }

            return player.EquipWeapon(entry.Item);
        }

        if (IsShieldItem(entry.Item))
        {
            if (player == null)
                return false;

            if (player.EquippedShield == entry.Item)
            {
                player.UnequipShield();
                return true;
            }

            return player.EquipShield(entry.Item);
        }

        ApplyItem(entry.Item, player);
        ItemSO usedItem = entry.Item;
        entry.Add(-1);

        if (entry.Amount <= 0)
        {
            entries[index] = null;
            ClearHotbarBindingsForItem(usedItem);
            CompactEntriesFrom(index);
        }

        NotifyChanged();
        return true;
    }

    public void Clear()
    {
        ClearInternal();
        NotifyChanged();
    }

    /// <summary>
    /// 采集背包格子和快捷栏绑定。物品只存 id 与数量，图标从 ItemCatalog 还原。
    /// </summary>
    public InventorySaveData CaptureSaveData()
    {
        EnsureSlotCapacity();

        var slots = new InventorySlotSaveData[Capacity];
        for (int i = 0; i < Capacity; i++)
        {
            InventoryEntry entry = entries[i];
            if (IsValidEntry(entry))
            {
                slots[i] = new InventorySlotSaveData
                {
                    itemId = entry.Item.id,
                    amount = entry.Amount,
                };
            }
            else
            {
                slots[i] = new InventorySlotSaveData();
            }
        }

        return new InventorySaveData
        {
            capacity = Capacity,
            circularInsertCursor = circularInsertCursor,
            slots = slots,
            hotbarItemIds = CaptureHotbarItemIds(),
        };
    }

    /// <summary>
    /// 按存档格子还原背包。未知物品 id 会跳过并打警告。
    /// </summary>
    public void ApplySaveData(InventorySaveData data, ItemCatalog itemCatalog)
    {
        if (data == null)
            return;

        capacity = Mathf.Max(1, data.capacity);
        EnsureSlotCapacity();

        for (int i = 0; i < entries.Count; i++)
            entries[i] = null;

        if (data.slots != null && itemCatalog != null)
        {
            int count = Mathf.Min(data.slots.Length, Capacity);
            for (int i = 0; i < count; i++)
            {
                InventorySlotSaveData slot = data.slots[i];
                if (slot == null || slot.itemId <= 0 || slot.amount <= 0)
                    continue;

                ItemSO item = itemCatalog.GetItem(slot.itemId);
                if (item == null)
                {
                    Debug.LogWarning($"Inventory: Unknown item id {slot.itemId} in save slot {i}.");
                    continue;
                }

                entries[i] = new InventoryEntry(item, slot.amount);
            }
        }

        circularInsertCursor = Mathf.Clamp(data.circularInsertCursor, 0, Capacity - 1);
        ApplyHotbarItemIds(data.hotbarItemIds);
        NotifyChanged();
    }

    public int GetHotbarItemId(int slotIndex)
    {
        EnsureHotbarCapacity();
        return IsValidHotbarIndex(slotIndex) ? hotbarItemIds[slotIndex] : 0;
    }

    public ItemSO GetHotbarItem(int slotIndex)
    {
        int itemId = GetHotbarItemId(slotIndex);
        if (itemId <= 0)
            return null;

        ItemCatalog catalog = ItemCatalog.EnsureAvailable();
        return catalog != null ? catalog.GetItem(itemId) : null;
    }

    public bool IsOnHotbar(ItemSO item)
    {
        return item != null && IsOnHotbar(item.id);
    }

    public bool IsOnHotbar(int itemId)
    {
        return IndexOfHotbarItemId(itemId) >= 0;
    }

    public void SetHotbarItem(int slotIndex, ItemSO item)
    {
        EnsureHotbarCapacity();
        if (!IsValidHotbarIndex(slotIndex))
            return;

        int itemId = item != null && item.id > 0 ? item.id : 0;
        if (itemId > 0)
        {
            int existing = IndexOfHotbarItemId(itemId);
            if (existing >= 0 && existing != slotIndex)
                hotbarItemIds[existing] = 0;
        }

        hotbarItemIds[slotIndex] = itemId;
        NotifyChanged();
    }

    public void ClearHotbarSlot(int slotIndex)
    {
        SetHotbarItem(slotIndex, null);
    }

    private void ClearHotbarBindingsForItem(ItemSO item)
    {
        if (item == null || item.id <= 0)
            return;

        EnsureHotbarCapacity();
        for (int i = 0; i < hotbarItemIds.Length; i++)
        {
            if (hotbarItemIds[i] == item.id)
                hotbarItemIds[i] = 0;
        }
    }

    public void FillEmptyHotbarFromInventory()
    {
        EnsureSlotCapacity();
        EnsureHotbarCapacity();
        if (!IsHotbarCompletelyEmpty())
            return;

        FillHotbarFromInventory(0, null);
        NotifyChanged();
    }

    public int CountOf(ItemSO item)
    {
        EnsureSlotCapacity();
        if (item == null)
            return 0;

        int total = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (IsSameItem(entry != null ? entry.Item : null, item))
                total += Mathf.Max(0, entry.Amount);
        }

        return total;
    }

    public int CountOf(int itemId)
    {
        EnsureSlotCapacity();
        if (itemId <= 0)
            return 0;

        int total = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            ItemSO item = entry != null ? entry.Item : null;
            if (item == null || item.id != itemId || entry.Amount <= 0)
                continue;

            total += entry.Amount;
        }

        return total;
    }

    public int FindFirstIndex(ItemSO item)
    {
        EnsureSlotCapacity();
        if (item == null)
            return -1;

        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (IsSameItem(entry != null ? entry.Item : null, item) && entry.Amount > 0)
                return i;
        }

        return -1;
    }

    public bool UseItem(ItemSO item, Player player)
    {
        int index = FindFirstIndex(item);
        return index >= 0 && UseItemAt(index, player);
    }

    private void CompactEntriesFrom(int startIndex)
    {
        EnsureSlotCapacity();
        if (startIndex < 0 || startIndex >= entries.Count)
            return;

        int writeIndex = startIndex;
        for (int readIndex = startIndex; readIndex < entries.Count; readIndex++)
        {
            InventoryEntry entry = entries[readIndex];
            if (!IsValidEntry(entry))
                continue;

            if (writeIndex != readIndex)
            {
                entries[writeIndex] = entry;
                entries[readIndex] = null;
            }

            writeIndex++;
        }

        circularInsertCursor = writeIndex % Capacity;
    }

    private void ClearInternal()
    {
        EnsureSlotCapacity();

        for (int i = 0; i < entries.Count; i++)
            entries[i] = null;

        circularInsertCursor = 0;
    }

    private InventoryEntry FindEntry(ItemSO item)
    {
        EnsureSlotCapacity();

        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (IsSameItem(entry != null ? entry.Item : null, item))
                return entry;
        }

        return null;
    }

    private static void ApplyItem(ItemSO item, Player player)
    {
        ConsumableEffectApplicator.Apply(item, player);
    }

    private static bool IsWeaponItem(ItemSO item)
    {
        return item != null
            && item.itemType == ItemType.Weapon
            && item.GetPropertyValue(ItemPropertyType.AttackValue) > 0;
    }

    private static bool IsShieldItem(ItemSO item)
    {
        return item != null
            && item.itemType == ItemType.Shield
            && item.GetPropertyValue(ItemPropertyType.ShieldDurability) > 0;
    }

    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        EnsureSlotCapacity();
        EnsureHotbarCapacity();
        circularInsertCursor = Mathf.Clamp(circularInsertCursor, 0, Capacity - 1);

        for (int i = 0; i < entries.Count; i++)
        {
            if (!IsValidEntry(entries[i]))
                entries[i] = null;
        }
    }

    private void NotifyChanged()
    {
        if (Changed != null)
            Changed.Invoke();
    }

    private void Awake()
    {
        EnsureSlotCapacity();
    }

    private void EnsureSlotCapacity()
    {
        capacity = Mathf.Max(1, capacity);

        while (entries.Count < capacity)
            entries.Add(null);

        while (entries.Count > capacity)
            entries.RemoveAt(entries.Count - 1);

        circularInsertCursor = Mathf.Clamp(circularInsertCursor, 0, Capacity - 1);
        EnsureHotbarCapacity();
    }

    private void EnsureHotbarCapacity()
    {
        if (hotbarItemIds == null || hotbarItemIds.Length != DefaultHotbarSize)
        {
            int[] resized = new int[DefaultHotbarSize];
            if (hotbarItemIds != null)
            {
                int copyCount = Mathf.Min(hotbarItemIds.Length, DefaultHotbarSize);
                for (int i = 0; i < copyCount; i++)
                    resized[i] = hotbarItemIds[i];
            }

            hotbarItemIds = resized;
        }
    }

    private int[] CaptureHotbarItemIds()
    {
        EnsureHotbarCapacity();
        var copy = new int[HotbarSize];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = hotbarItemIds[i];
        return copy;
    }

    private void ApplyHotbarItemIds(int[] savedIds)
    {
        EnsureHotbarCapacity();
        for (int i = 0; i < hotbarItemIds.Length; i++)
            hotbarItemIds[i] = 0;

        if (savedIds == null)
            return;

        int count = Mathf.Min(savedIds.Length, hotbarItemIds.Length);
        for (int i = 0; i < count; i++)
            hotbarItemIds[i] = Mathf.Max(0, savedIds[i]);
    }

    private bool IsHotbarCompletelyEmpty()
    {
        EnsureHotbarCapacity();
        for (int i = 0; i < hotbarItemIds.Length; i++)
        {
            if (hotbarItemIds[i] > 0)
                return false;
        }

        return true;
    }

    private int IndexOfHotbarItemId(int itemId)
    {
        EnsureHotbarCapacity();
        if (itemId <= 0)
            return -1;

        for (int i = 0; i < hotbarItemIds.Length; i++)
        {
            if (hotbarItemIds[i] == itemId)
                return i;
        }

        return -1;
    }

    private int FillHotbarFromInventory(int writeIndex, ItemType? itemType)
    {
        for (int i = 0; i < entries.Count && writeIndex < hotbarItemIds.Length; i++)
        {
            InventoryEntry entry = entries[i];
            if (!IsValidEntry(entry) || entry.Item.id <= 0)
                continue;

            if (itemType.HasValue && entry.Item.itemType != itemType.Value)
                continue;

            if (IndexOfHotbarItemId(entry.Item.id) >= 0)
                continue;

            hotbarItemIds[writeIndex] = entry.Item.id;
            writeIndex++;
        }

        return writeIndex;
    }

    private bool IsValidHotbarIndex(int index)
    {
        EnsureHotbarCapacity();
        return index >= 0 && index < hotbarItemIds.Length;
    }

    private static bool IsSameItem(ItemSO left, ItemSO right)
    {
        if (left == null || right == null)
            return false;

        if (left == right)
            return true;

        return left.id > 0 && left.id == right.id;
    }

    private bool TryAddSequential(ItemSO item, int amount)
    {
        InventoryEntry entry = FindEntry(item);
        if (entry != null)
        {
            entry.Add(amount);
            return true;
        }

        int slotIndex = FindFirstEmptySlot();
        if (slotIndex < 0)
            return false;

        entries[slotIndex] = new InventoryEntry(item, amount);
        circularInsertCursor = (slotIndex + 1) % Capacity;
        return true;
    }

    private void TryAssignToFirstEmptyHotbar(ItemSO item)
    {
        if (item == null || item.id <= 0)
            return;

        EnsureHotbarCapacity();
        if (IsOnHotbar(item.id))
            return;

        int slotIndex = FindFirstEmptyHotbarSlot();
        if (slotIndex < 0)
            return;

        hotbarItemIds[slotIndex] = item.id;
    }

    private int FindFirstEmptyHotbarSlot()
    {
        EnsureHotbarCapacity();
        for (int i = 0; i < hotbarItemIds.Length; i++)
        {
            if (hotbarItemIds[i] <= 0)
                return i;
        }

        return -1;
    }

    private bool TryInsertAtFront(ItemSO item, int amount)
    {
        var orderedEntries = CollectOccupiedEntries();

        for (int i = 0; i < orderedEntries.Count; i++)
        {
            InventoryEntry entry = orderedEntries[i];
            if (!IsSameItem(entry.Item, item))
                continue;

            entry.Add(amount);
            if (i > 0)
            {
                orderedEntries.RemoveAt(i);
                orderedEntries.Insert(0, entry);
            }

            WriteOrderedEntries(orderedEntries);
            return true;
        }

        if (orderedEntries.Count >= Capacity)
            return false;

        orderedEntries.Insert(0, new InventoryEntry(item, amount));
        WriteOrderedEntries(orderedEntries);
        return true;
    }

    private List<InventoryEntry> CollectOccupiedEntries()
    {
        var orderedEntries = new List<InventoryEntry>(Capacity);
        for (int i = 0; i < Capacity; i++)
        {
            if (IsValidEntry(entries[i]))
                orderedEntries.Add(entries[i]);
        }

        return orderedEntries;
    }

    private void WriteOrderedEntries(List<InventoryEntry> orderedEntries)
    {
        for (int i = 0; i < Capacity; i++)
            entries[i] = i < orderedEntries.Count ? orderedEntries[i] : null;

        circularInsertCursor = orderedEntries.Count % Capacity;
    }

    private int FindFirstEmptySlot()
    {
        EnsureSlotCapacity();

        for (int i = 0; i < entries.Count; i++)
        {
            if (!IsValidEntry(entries[i]))
            {
                entries[i] = null;
                return i;
            }
        }

        return -1;
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < Capacity;
    }

    private static bool IsValidEntry(InventoryEntry entry)
    {
        return entry != null && entry.Item != null && entry.Amount > 0;
    }

    private int CountOccupiedSlots()
    {
        EnsureSlotCapacity();

        int occupied = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (IsValidEntry(entries[i]))
                occupied++;
        }

        return occupied;
    }

    private int CountOccupiedBackpackSlots()
    {
        EnsureSlotCapacity();

        int occupied = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (!IsValidEntry(entry) || IsOnHotbar(entry.Item))
                continue;

            occupied++;
        }

        return occupied;
    }
}
