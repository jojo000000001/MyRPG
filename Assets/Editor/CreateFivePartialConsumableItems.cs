using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateFivePartialConsumableItems
{
    private const string IconFolder = "Assets/Art/Items/Consumables";
    private const string ItemFolder = "Assets/DataSO/GeneratedConsumables";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const int BackpackStartSlot = 20;
    private const int BackpackCapacity = 25;

    private readonly struct ConsumableDef
    {
        public readonly string IconFile;
        public readonly string AssetName;
        public readonly int Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly ItemPropertyType PrimaryType;
        public readonly int PrimaryValue;
        public readonly ItemPropertyType SecondaryType;
        public readonly int SecondaryValue;

        public ConsumableDef(
            string iconFile,
            string assetName,
            int id,
            string displayName,
            string description,
            ItemPropertyType primaryType,
            int primaryValue,
            ItemPropertyType secondaryType = ItemPropertyType.HPValue,
            int secondaryValue = 0)
        {
            IconFile = iconFile;
            AssetName = assetName;
            Id = id;
            DisplayName = displayName;
            Description = description;
            PrimaryType = primaryType;
            PrimaryValue = primaryValue;
            SecondaryType = secondaryType;
            SecondaryValue = secondaryValue;
        }
    }

    private static readonly ConsumableDef[] Definitions =
    {
        new("consumable_ember_tonic.png", "Consumer_EmberTonic", 136, "Ember Tonic", "A heated tonic that sharpens attacks.", ItemPropertyType.AttackValue, 30),
        new("consumable_frost_berry.png", "Consumer_FrostBerry", 137, "Frost Berry", "Icy berries that restore energy.", ItemPropertyType.EnergyValue, 38),
        new("consumable_dream_leaf.png", "Consumer_DreamLeaf", 138, "Dream Leaf", "A mystical leaf that clears mental fog.", ItemPropertyType.MentalValue, 48),
        new("consumable_sunseed.png", "Consumer_Sunseed", 139, "Sunseed", "A radiant seed that restores HP.", ItemPropertyType.HPValue, 48),
        new("consumable_nightshade_brew.png", "Consumer_NightshadeBrew", 140, "Nightshade Brew", "A shadowy brew for mind and swiftness.", ItemPropertyType.MentalValue, 35, ItemPropertyType.SpeedValue, 8),
    };

    [MenuItem("Tools/Items/Create 5 Partial Consumables And Add To Backpack")]
    public static void CreateAllAndAddToBackpack()
    {
        CreateAll();
        AddToPlayerBackpack();
    }

    [MenuItem("Tools/Items/Create 5 Partial Consumable ItemSOs")]
    public static void CreateAll()
    {
        if (!AssetDatabase.IsValidFolder(ItemFolder))
            AssetDatabase.CreateFolder("Assets/DataSO", "GeneratedConsumables");

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < Definitions.Length; i++)
                ConfigureSpriteImport($"{IconFolder}/{Definitions[i].IconFile}");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < Definitions.Length; i++)
                CreateOrUpdateItem(Definitions[i]);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created or updated {Definitions.Length} partial consumable ItemSO assets.");
    }

    [MenuItem("Tools/Items/Add 5 Partial Consumables To Player Backpack")]
    public static void AddToPlayerBackpack()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError($"CreateFivePartialConsumableItems: Missing player prefab at {PlayerPrefabPath}");
            return;
        }

        Inventory inventory = playerPrefab.GetComponent<Inventory>();
        if (inventory == null)
        {
            Debug.LogError("CreateFivePartialConsumableItems: Player prefab has no Inventory component.");
            return;
        }

        SerializedObject serializedInventory = new SerializedObject(inventory);
        SerializedProperty capacity = serializedInventory.FindProperty("capacity");
        SerializedProperty entries = serializedInventory.FindProperty("entries");
        if (capacity == null || entries == null)
        {
            Debug.LogError("CreateFivePartialConsumableItems: Could not find Inventory fields.");
            return;
        }

        capacity.intValue = BackpackCapacity;

        int added = inventory.EnqueueItemsViaCircularQueue(
            LoadItemDefinitions(),
            amountEach: 1,
            clearExisting: false);

        serializedInventory.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(playerPrefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"Added {added} consumables to Player backpack via circular queue (slots {BackpackStartSlot}+).");
    }

    private static ItemSO[] LoadItemDefinitions()
    {
        ItemSO[] items = new ItemSO[Definitions.Length];
        for (int i = 0; i < Definitions.Length; i++)
        {
            string assetPath = $"{ItemFolder}/{Definitions[i].AssetName}.asset";
            items[i] = AssetDatabase.LoadAssetAtPath<ItemSO>(assetPath);
        }

        return items;
    }

    private static void CreateOrUpdateItem(ConsumableDef def)
    {
        string iconPath = $"{IconFolder}/{def.IconFile}";
        if (!File.Exists(iconPath))
        {
            Debug.LogError($"Missing icon: {iconPath}");
            return;
        }

        string assetPath = $"{ItemFolder}/{def.AssetName}.asset";
        ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(assetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemSO>();
            AssetDatabase.CreateAsset(item, assetPath);
        }

        item.id = def.Id;
        item.name = def.DisplayName;
        item.itemType = ItemType.Consumable;
        item.description = def.Description;
        item.icon = LoadSprite(iconPath);
        item.prefab = null;
        item.propertyList = BuildProperties(def);

        EditorUtility.SetDirty(item);
    }

    private static List<ItemProperty> BuildProperties(ConsumableDef def)
    {
        List<ItemProperty> properties = new List<ItemProperty>();
        if (def.PrimaryValue != 0)
        {
            properties.Add(new ItemProperty
            {
                PropertyType = def.PrimaryType,
                Value = def.PrimaryValue
            });
        }

        if (def.SecondaryValue != 0)
        {
            properties.Add(new ItemProperty
            {
                PropertyType = def.SecondaryType,
                Value = def.SecondaryValue
            });
        }

        return properties;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite loadedSprite)
                return loadedSprite;
        }

        Debug.LogWarning($"Sprite not found for {assetPath}");
        return null;
    }

    private static void ConfigureSpriteImport(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (Mathf.Abs(importer.spritePixelsPerUnit - 512f) > 0.01f)
        {
            importer.spritePixelsPerUnit = 512f;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }
}
