using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateTenConsumableItems
{
    private const string IconFolder = "Assets/Art/Items/Consumables";
    private const string ItemFolder = "Assets/DataSO/GeneratedConsumables";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const int BackpackStartSlot = 10;

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
        new("consumable_moon_elixir.png", "Consumer_MoonElixir", 126, "Moon Elixir", "A lunar brew that restores plenty of HP.", ItemPropertyType.HPValue, 75),
        new("consumable_rage_potion.png", "Consumer_RagePotion", 127, "Rage Potion", "A fierce potion that empowers attacks.", ItemPropertyType.AttackValue, 32),
        new("consumable_sage_tea.png", "Consumer_SageTea", 128, "Sage Tea", "Herbal tea that clears mental fatigue.", ItemPropertyType.MentalValue, 40),
        new("consumable_spark_dust.png", "Consumer_SparkDust", 129, "Spark Dust", "Crackling dust that restores energy.", ItemPropertyType.EnergyValue, 45),
        new("consumable_shadow_vial.png", "Consumer_ShadowVial", 130, "Shadow Vial", "A dark brew that boosts escape speed.", ItemPropertyType.SpeedValue, 18),
        new("consumable_iron_root.png", "Consumer_IronRoot", 131, "Iron Root", "A tough root that restores HP and strength.", ItemPropertyType.HPValue, 40, ItemPropertyType.AttackValue, 10),
        new("consumable_pearl_soup.png", "Consumer_PearlSoup", 132, "Pearl Soup", "A soothing soup for body and mind.", ItemPropertyType.HPValue, 50, ItemPropertyType.MentalValue, 15),
        new("consumable_flame_orchid.png", "Consumer_FlameOrchid", 133, "Flame Orchid", "A fiery flower that boosts attack and energy.", ItemPropertyType.AttackValue, 22, ItemPropertyType.EnergyValue, 10),
        new("consumable_storm_charm.png", "Consumer_StormCharm", 134, "Storm Charm", "A charged charm for speed and power.", ItemPropertyType.SpeedValue, 10, ItemPropertyType.AttackValue, 15),
        new("consumable_holy_water.png", "Consumer_HolyWater", 135, "Holy Water", "Blessed water that heals body and spirit.", ItemPropertyType.HPValue, 60, ItemPropertyType.MentalValue, 20),
    };

    [MenuItem("Tools/Items/Create 10 Consumable ItemSOs And Add To Backpack")]
    public static void CreateAllAndAddToBackpack()
    {
        CreateAll();
        AddToPlayerBackpack();
    }

    [MenuItem("Tools/Items/Create 10 Consumable ItemSOs")]
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
        Debug.Log($"Created or updated {Definitions.Length} consumable ItemSO assets in {ItemFolder}.");
    }

    [MenuItem("Tools/Items/Add Latest 10 Consumables To Player Backpack")]
    public static void AddToPlayerBackpack()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError($"CreateTenConsumableItems: Missing player prefab at {PlayerPrefabPath}");
            return;
        }

        Inventory inventory = playerPrefab.GetComponent<Inventory>();
        if (inventory == null)
        {
            Debug.LogError("CreateTenConsumableItems: Player prefab has no Inventory component.");
            return;
        }

        SerializedObject serializedInventory = new SerializedObject(inventory);
        SerializedProperty entries = serializedInventory.FindProperty("entries");
        if (entries == null)
        {
            Debug.LogError("CreateTenConsumableItems: Could not find Inventory.entries.");
            return;
        }

        int requiredSize = BackpackStartSlot + Definitions.Length;
        while (entries.arraySize < requiredSize)
            entries.InsertArrayElementAtIndex(entries.arraySize);

        for (int i = 0; i < Definitions.Length; i++)
        {
            string assetPath = $"{ItemFolder}/{Definitions[i].AssetName}.asset";
            ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(assetPath);
            if (item == null)
            {
                Debug.LogError($"CreateTenConsumableItems: Missing ItemSO at {assetPath}");
                continue;
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(BackpackStartSlot + i);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("amount").intValue = 1;
        }

        serializedInventory.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(playerPrefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"Added {Definitions.Length} consumables to Player backpack slots {BackpackStartSlot}-{BackpackStartSlot + Definitions.Length - 1}.");
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
