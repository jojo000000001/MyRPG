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
    [SerializeField] private int capacity = 20;
    [SerializeField] private List<InventoryEntry> entries = new List<InventoryEntry>();
    [SerializeField] private int circularInsertCursor;

    public event Action Changed;

    public int Capacity => Mathf.Max(1, capacity);
    public IReadOnlyList<InventoryEntry> Entries => entries;
    public int OccupiedSlotCount => CountOccupiedSlots();

    public bool AddItem(ItemSO item, int amount = 1, bool insertAtFront = false)
    {
        EnsureSlotCapacity();

        if (item == null || amount <= 0)
            return false;

        if (insertAtFront)
            return TryInsertAtFront(item, amount);

        InventoryEntry entry = FindEntry(item);
        if (entry != null)
        {
            entry.Add(amount);
            NotifyChanged();
            return true;
        }

        int slotIndex = FindNextEmptySlotCircular();
        if (slotIndex < 0)
            return false;

        entries[slotIndex] = new InventoryEntry(item, amount);
        circularInsertCursor = (slotIndex + 1) % Capacity;
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
        if (toEntry != null && toEntry.Item == fromEntry.Item)
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

        ApplyItem(entry.Item, player);
        entry.Add(-1);

        if (entry.Amount <= 0)
            entries[index] = null;

        NotifyChanged();
        return true;
    }

    public void Clear()
    {
        ClearInternal();
        NotifyChanged();
    }

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
        };
    }

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
        NotifyChanged();
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
            if (entry != null && entry.Item == item)
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

    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        EnsureSlotCapacity();
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
    }

    private bool TryInsertAtFront(ItemSO item, int amount)
    {
        var orderedEntries = CollectOccupiedEntries();

        for (int i = 0; i < orderedEntries.Count; i++)
        {
            InventoryEntry entry = orderedEntries[i];
            if (entry.Item != item)
                continue;

            entry.Add(amount);
            if (i > 0)
            {
                orderedEntries.RemoveAt(i);
                orderedEntries.Insert(0, entry);
            }

            WriteOrderedEntries(orderedEntries);
            NotifyChanged();
            return true;
        }

        if (orderedEntries.Count >= Capacity)
            return false;

        orderedEntries.Insert(0, new InventoryEntry(item, amount));
        WriteOrderedEntries(orderedEntries);
        NotifyChanged();
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

    private int FindNextEmptySlotCircular()
    {
        EnsureSlotCapacity();

        for (int offset = 0; offset < entries.Count; offset++)
        {
            int index = (circularInsertCursor + offset) % entries.Count;
            if (!IsValidEntry(entries[index]))
            {
                entries[index] = null;
                return index;
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
}
