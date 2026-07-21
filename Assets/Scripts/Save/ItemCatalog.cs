using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Game/Item Catalog")]
public sealed class ItemCatalog : ScriptableObject
{
    private static ItemCatalog cachedInstance;

    [SerializeField] private List<ItemSO> items = new List<ItemSO>();

    private Dictionary<int, ItemSO> lookup;

    public static ItemCatalog Instance => EnsureAvailable();

    public static ItemCatalog EnsureAvailable()
    {
        if (cachedInstance != null)
            return cachedInstance;

        cachedInstance = Resources.Load<ItemCatalog>("ItemCatalog");
        if (cachedInstance != null)
        {
            cachedInstance.BuildLookup();
            return cachedInstance;
        }

        ItemSO[] allItems = Resources.FindObjectsOfTypeAll<ItemSO>();
        if (allItems == null || allItems.Length == 0)
            return null;

        ItemCatalog runtimeCatalog = CreateInstance<ItemCatalog>();
        runtimeCatalog.name = "RuntimeItemCatalog";
        runtimeCatalog.SetItems(allItems);
        cachedInstance = runtimeCatalog;
        Debug.Log($"ItemCatalog: Built runtime catalog with {allItems.Length} items.");
        return cachedInstance;
    }

    public IReadOnlyList<ItemSO> Items => items;

    public ItemSO GetItem(int itemId)
    {
        if (itemId <= 0)
            return null;

        BuildLookup();
        return lookup.TryGetValue(itemId, out ItemSO item) ? item : null;
    }

    public void BuildLookup()
    {
        if (lookup != null)
            return;

        lookup = new Dictionary<int, ItemSO>();
        for (int i = 0; i < items.Count; i++)
        {
            ItemSO item = items[i];
            if (item == null || item.id <= 0)
                continue;

            if (lookup.ContainsKey(item.id))
            {
                Debug.LogWarning($"ItemCatalog: Duplicate item id {item.id} on {item.name}");
                continue;
            }

            lookup[item.id] = item;
        }
    }

    public void SetItems(IReadOnlyList<ItemSO> sourceItems)
    {
        items = sourceItems != null ? new List<ItemSO>(sourceItems) : new List<ItemSO>();
        lookup = null;
        BuildLookup();
    }

    private void OnEnable()
    {
        BuildLookup();
    }

    public static void RegisterRuntimeInstance(ItemCatalog catalog)
    {
        cachedInstance = catalog;
        if (cachedInstance != null)
            cachedInstance.BuildLookup();
    }
}
