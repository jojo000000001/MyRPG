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

    public event Action Changed;

    public int Capacity => Mathf.Max(1, capacity);
    public IReadOnlyList<InventoryEntry> Entries => entries;

public bool AddItem(ItemSO item, int amount = 1)
    {
        EnsureSlotCapacity();

        if (item == null || amount <= 0)
            return false;

        InventoryEntry entry = FindEntry(item);
        if (entry != null)
        {
            entry.Add(amount);
            NotifyChanged();
            return true;
        }

        int slotIndex = FindFirstEmptySlot();
        if (slotIndex < 0)
            return false;

        entries[slotIndex] = new InventoryEntry(item, amount);
        NotifyChanged();
        return true;
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
        EnsureSlotCapacity();

        for (int i = 0; i < entries.Count; i++)
            entries[i] = null;

        NotifyChanged();
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
}
