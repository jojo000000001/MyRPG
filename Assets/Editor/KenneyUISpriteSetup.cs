using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Kenney RPG UI 扩展包 PNG 批量配置为 UGUI 可用的 Sprite。
/// </summary>
public static class KenneyUISpriteSetup
{
    private const string PngRoot = "Assets/Art/UI/kenney_ui-pack-rpg-expansion/PNG/";

    [MenuItem("Tools/MyRPG/Setup Kenney UI Sprites")]
    public static void SetupAllFromMenu()
    {
        int count = SetupAll();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[KenneyUISpriteSetup] Configured {count} texture(s) under {PngRoot}");
    }

    public static int SetupAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/UI/kenney_ui-pack-rpg-expansion/PNG"))
            return 0;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI/kenney_ui-pack-rpg-expansion/PNG" });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (ConfigureAtPath(path))
                count++;
        }

        return count;
    }

    public static bool ConfigureAtPath(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        ConfigureImporter(importer, assetPath);
        importer.SaveAndReimport();
        return true;
    }

    public static void ConfigureImporter(TextureImporter importer, string assetPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(assetPath);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsToUnits = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spriteBorder = ResolveSpriteBorder(fileName);
    }

    /// <summary>
    /// Kenney UI 包常用 9-slice 边距（按素材命名推断）。
    /// </summary>
    private static Vector4 ResolveSpriteBorder(string fileName)
    {
        string name = fileName.ToLowerInvariant();

        if (name.StartsWith("panelinset"))
            return new Vector4(9f, 9f, 9f, 9f);

        if (name.StartsWith("panel"))
            return new Vector4(10f, 10f, 10f, 10f);

        if (name.StartsWith("buttonlong"))
            return new Vector4(12f, 12f, 12f, 12f);

        if (name.StartsWith("buttonsquare"))
            return new Vector4(10f, 10f, 10f, 10f);

        if (name.Contains("horizontalmid") && name.StartsWith("bar"))
            return new Vector4(9f, 0f, 9f, 0f);

        if (name.Contains("verticalmid") && name.StartsWith("bar"))
            return new Vector4(0f, 9f, 0f, 9f);

        return Vector4.zero;
    }
}

/// <summary>
/// 新导入或修改 Kenney PNG 时自动应用 Sprite 设置。
/// </summary>
public sealed class KenneyUISpritePostprocessor : AssetPostprocessor
{
    private const string PngRoot = "Assets/Art/UI/kenney_ui-pack-rpg-expansion/PNG/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(PngRoot) || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            return;

        KenneyUISpriteSetup.ConfigureImporter((TextureImporter)assetImporter, assetPath);
    }
}
