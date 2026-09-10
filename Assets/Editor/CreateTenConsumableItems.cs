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
        new("consumable_moon_elixir.png", "Consumer_MoonElixir", 126, "月华药剂", "月下酿成的药剂，能恢复大量生命。", ItemPropertyType.HPValue, 75),
        new("consumable_rage_potion.png", "Consumer_RagePotion", 127, "狂怒药水", "猛烈的药水，能大幅增强攻击。", ItemPropertyType.AttackValue, 32),
        new("consumable_sage_tea.png", "Consumer_SageTea", 128, "鼠尾草茶", "草本茶饮，能消除精神疲劳。", ItemPropertyType.MentalValue, 40),
        new("consumable_spark_dust.png", "Consumer_SparkDust", 129, "火花尘", "噼啪作响的粉尘，能恢复精力。", ItemPropertyType.EnergyValue, 45),
        new("consumable_shadow_vial.png", "Consumer_ShadowVial", 130, "暗影瓶", "深色药剂，能提升逃脱速度。", ItemPropertyType.SpeedValue, 18),
        new("consumable_iron_root.png", "Consumer_IronRoot", 131, "铁根", "坚韧的根茎，能恢复生命并增强力量。", ItemPropertyType.HPValue, 40, ItemPropertyType.AttackValue, 10),
        new("consumable_pearl_soup.png", "Consumer_PearlSoup", 132, "珍珠汤", "温和的汤羹，能滋养身体与心灵。", ItemPropertyType.HPValue, 50, ItemPropertyType.MentalValue, 15),
        new("consumable_flame_orchid.png", "Consumer_FlameOrchid", 133, "焰兰", "炽热的花朵，能提升攻击并恢复精力。", ItemPropertyType.AttackValue, 22, ItemPropertyType.EnergyValue, 10),
        new("consumable_storm_charm.png", "Consumer_StormCharm", 134, "风暴护符", "蕴藏雷能的护符，能提升速度与力量。", ItemPropertyType.SpeedValue, 10, ItemPropertyType.AttackValue, 15),
        new("consumable_holy_water.png", "Consumer_HolyWater", 135, "圣水", "受祝福的清水，能治愈身体与心灵。", ItemPropertyType.HPValue, 60, ItemPropertyType.MentalValue, 20),
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
