using System.IO;
using UnityEditor;
using UnityEngine;

public static class FixMissingConsumableIcons
{
    private const string IconFolder = "Assets/Art/Items/Consumables";
    private const string ItemFolder = "Assets/DataSO/GeneratedConsumables";

    [MenuItem("Tools/Items/Fix Missing Generated Consumable Icons")]
    public static void FixAll()
    {
        string[] assetGuids = AssetDatabase.FindAssets("t:ItemSO", new[] { ItemFolder });
        int fixedCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(assetPath);
                if (item == null || item.icon != null)
                    continue;

                string iconPath = ResolveIconPath(assetPath);
                if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
                    continue;

                Sprite sprite = LoadSprite(iconPath);
                if (sprite == null)
                    continue;

                item.icon = sprite;
                EditorUtility.SetDirty(item);
                fixedCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Fixed {fixedCount} missing consumable icon(s) in {ItemFolder}.");
    }

    private static string ResolveIconPath(string assetPath)
    {
        string assetName = Path.GetFileNameWithoutExtension(assetPath);
        if (!assetName.StartsWith("Consumer_"))
            return null;

        string suffix = assetName.Substring("Consumer_".Length);
        string snake = ToSnakeCase(suffix);
        return $"{IconFolder}/consumable_{snake}.png";
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsUpper(c) && i > 0)
                builder.Append('_');

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
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

        return null;
    }
}
