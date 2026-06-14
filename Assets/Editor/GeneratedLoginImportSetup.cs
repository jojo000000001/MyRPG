using UnityEditor;
using UnityEngine;

/// <summary>
/// 配置 GeneratedLogin 目录下 PNG 的 Sprite 导入设置。
/// </summary>
public static class GeneratedLoginImportSetup
{
    private const string Root = "Assets/Art/UI/GeneratedLogin/";

    [MenuItem("Tools/MyRPG/Setup Generated Login Art")]
    public static void SetupAllFromMenu()
    {
        int count = SetupAll();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GeneratedLoginImportSetup] Configured {count} texture(s).");
    }

    public static int SetupAll()
    {
        if (!AssetDatabase.IsValidFolder(Root.TrimEnd('/')))
            return 0;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') });
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
        string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsToUnits = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;

        if (fileName.Contains("panel"))
            importer.spriteBorder = new Vector4(48f, 48f, 48f, 48f);
        else if (fileName.Contains("input") || fileName.Contains("field"))
            importer.spriteBorder = new Vector4(24f, 16f, 24f, 16f);
        else if (fileName.Contains("button"))
            importer.spriteBorder = new Vector4(32f, 20f, 32f, 20f);
        else
            importer.spriteBorder = Vector4.zero;
    }
}

public sealed class GeneratedLoginImportPostprocessor : AssetPostprocessor
{
    private const string Root = "Assets/Art/UI/GeneratedLogin/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root) || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            return;

        GeneratedLoginImportSetup.ConfigureImporter((TextureImporter)assetImporter, assetPath);
    }
}
