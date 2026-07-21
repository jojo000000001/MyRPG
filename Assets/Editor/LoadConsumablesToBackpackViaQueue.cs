using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LoadConsumablesToBackpackViaQueue
{
    private const string ItemFolder = "Assets/DataSO/GeneratedConsumables";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/Items/Load Generated Consumables To Backpack Via Circular Queue")]
    public static void LoadAllGeneratedConsumables()
    {
        string[] assetGuids = AssetDatabase.FindAssets("t:ItemSO", new[] { ItemFolder });
        var items = new List<ItemSO>(assetGuids.Length);

        for (int i = 0; i < assetGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
            if (item != null && item.itemType == ItemType.Consumable)
                items.Add(item);
        }

        items.Sort((a, b) => a.id.CompareTo(b.id));
        LoadItems(items);
    }

    public static void LoadItems(IReadOnlyList<ItemSO> items)
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError($"LoadConsumablesToBackpackViaQueue: Missing player prefab at {PlayerPrefabPath}");
            return;
        }

        Inventory inventory = playerPrefab.GetComponent<Inventory>();
        if (inventory == null)
        {
            Debug.LogError("LoadConsumablesToBackpackViaQueue: Player prefab has no Inventory component.");
            return;
        }

        SerializedObject serializedInventory = new SerializedObject(inventory);
        SerializedProperty capacity = serializedInventory.FindProperty("capacity");
        if (capacity != null && capacity.intValue < items.Count)
            capacity.intValue = items.Count;

        serializedInventory.ApplyModifiedPropertiesWithoutUndo();

        int added = inventory.EnqueueItemsViaCircularQueue(items, clearExisting: true);
        EditorUtility.SetDirty(playerPrefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"Loaded {added}/{items.Count} consumables into Player backpack via circular queue.");
    }
}
