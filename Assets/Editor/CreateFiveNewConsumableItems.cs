using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateFiveNewConsumableItems
{
    private const string IconFolder = "Assets/Art/Items/Consumables";
    private const string ItemFolder = "Assets/DataSO/GeneratedConsumables";

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
        new("consumable_poison_vial.png", "Consumer_PoisonVial", 116, "Poison Vial", "A toxic brew that clears mental fog.", ItemPropertyType.MentalValue, 35),
        new("consumable_life_crystal.png", "Consumer_LifeCrystal", 117, "Life Crystal", "A radiant crystal that restores HP.", ItemPropertyType.HPValue, 90),
        new("consumable_energy_bar.png", "Consumer_EnergyBar", 118, "Energy Bar", "A quick snack that restores energy.", ItemPropertyType.EnergyValue, 35),
        new("consumable_smoke_bomb.png", "Consumer_SmokeBomb", 119, "Smoke Bomb", "A smoke bomb that boosts escape speed.", ItemPropertyType.SpeedValue, 30),
        new("consumable_thunder_stone.png", "Consumer_ThunderStone", 120, "Thunder Stone", "A charged stone that empowers attacks.", ItemPropertyType.AttackValue, 35),
    };

    [MenuItem("Tools/Items/Create 5 New Consumable ItemSOs")]
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
