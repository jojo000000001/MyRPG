using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateBatchConsumableItems
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
        new("consumable_greater_hp_potion.png", "Consumer_GreaterHpPotion", 106, "高级生命药水", "能永久提升生命上限。", ItemPropertyType.MaxHPValue, 10),
        new("consumable_roast_meat.png", "Consumer_RoastMeat", 107, "烤肉串", "烤过的肉串，能恢复生命。", ItemPropertyType.HPValue, 80),
        new("consumable_healing_herb.png", "Consumer_HealingHerb", 108, "治愈草药", "新鲜草药，能温和地恢复生命。", ItemPropertyType.HPValue, 50),
        new("consumable_fire_bomb.png", "Consumer_FireBomb", 109, "火焰炸弹", "灌入火焰的投掷炸弹。", ItemPropertyType.AttackValue, 30),
        new("consumable_frost_potion.png", "Consumer_FrostPotion", 110, "寒霜药水", "冰凉药剂，能恢复精力。", ItemPropertyType.EnergyValue, 40),
        new("consumable_golden_apple.png", "Consumer_GoldenApple", 111, "金苹果", "珍稀果实，能永久提升生命上限。", ItemPropertyType.MaxHPValue, 20),
        new("consumable_strength_tonic.png", "Consumer_StrengthTonic", 112, "力量补剂", "能提升攻击力的补剂。", ItemPropertyType.AttackValue, 20),
        new("consumable_swiftness_elixir.png", "Consumer_SwiftnessElixir", 113, "迅捷药剂", "喝下后能提升移动速度。", ItemPropertyType.SpeedValue, 10),
        new("consumable_magic_scroll.png", "Consumer_MagicScroll", 114, "魔法卷轴", "秘法卷轴，能消除精神疲劳。", ItemPropertyType.MentalValue, 45),
        new("consumable_honey_cookie.png", "Consumer_HoneyCookie", 115, "蜂蜜饼干", "香甜饼干，能恢复生命和精力。", ItemPropertyType.HPValue, 45, ItemPropertyType.EnergyValue, 15),
    };

    [MenuItem("Tools/Items/Create 10 Batch Consumable ItemSOs")]
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
