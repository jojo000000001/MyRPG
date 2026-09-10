using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 从项目内中文字体生成 TMP 动态字体，并挂到默认 LiberationSans 的回退列表。
/// </summary>
public static class ChineseTmpFontBuilder
{
    public const string SourceFontPath = "Assets/Fonts/NotoSansSC-Regular.otf";
    public const string FontAssetPath = "Assets/Resources/UIFonts/ChineseSDF.asset";
    public const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    public const string DefaultSdfPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/MyRPG/Rebuild Chinese TMP Font")]
    public static void Rebuild()
    {
        string message = RebuildInternal();
        Debug.Log("ChineseTmpFontBuilder: " + message);
        EditorUtility.DisplayDialog("Chinese TMP Font", message, "OK");
    }

    public static string RebuildInternal()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null)
            return "Missing source font at " + SourceFontPath;

        ConfigureSourceFontImporter();

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            source,
            90,
            9,
            GlyphRenderMode.SDFAA,
            2048,
            2048,
            AtlasPopulationMode.Dynamic,
            true);

        if (fontAsset == null)
            return "TMP_FontAsset.CreateFontAsset returned null.";

        fontAsset.name = "ChineseSDF";
        fontAsset.TryAddCharacters("背包铁剑炎龙剑林地短剑消耗品武器已装备生命上限精力精神速度空等级经验盾牌耐久轻木鸢盾精钢闪避左键右键跳跃加速");

        string folder = Path.GetDirectoryName(FontAssetPath)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
        {
            EnsureFolder(folder);
        }

        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(FontAssetPath);

        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        if (fontAsset.material != null)
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        Texture2D[] atlases = fontAsset.atlasTextures;
        if (atlases != null)
        {
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] != null)
                    AssetDatabase.AddObjectToAsset(atlases[i], fontAsset);
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssignFallbacks(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Created " + FontAssetPath + " chars=" + fontAsset.characterTable.Count;
    }

    private static void ConfigureSourceFontImporter()
    {
        TrueTypeFontImporter importer = AssetImporter.GetAtPath(SourceFontPath) as TrueTypeFontImporter;
        if (importer == null)
            return;

        importer.fontTextureCase = FontTextureCase.Dynamic;
        importer.includeFontData = true;
        importer.fontNames = new[] { "Noto Sans SC" };
        importer.SaveAndReimport();
    }

    private static void AssignFallbacks(TMP_FontAsset chinese)
    {
        AddFallbackToSettings(chinese);
        AddFallbackToFont(DefaultSdfPath, chinese);
    }

    private static void AddFallbackToSettings(TMP_FontAsset chinese)
    {
        Object settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
        if (settings == null)
            return;

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty fallbacks = so.FindProperty("m_fallbackFontAssets");
        if (fallbacks == null || !fallbacks.isArray)
            return;

        for (int i = 0; i < fallbacks.arraySize; i++)
        {
            if (fallbacks.GetArrayElementAtIndex(i).objectReferenceValue == chinese)
                return;
        }

        fallbacks.arraySize += 1;
        fallbacks.GetArrayElementAtIndex(fallbacks.arraySize - 1).objectReferenceValue = chinese;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static void AddFallbackToFont(string fontPath, TMP_FontAsset chinese)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (font == null)
            return;

        SerializedObject so = new SerializedObject(font);
        SerializedProperty table = so.FindProperty("m_FallbackFontAssetTable");
        if (table == null || !table.isArray)
            table = so.FindProperty("fallbackFontAssets");
        if (table == null || !table.isArray)
            return;

        for (int i = 0; i < table.arraySize; i++)
        {
            if (table.GetArrayElementAtIndex(i).objectReferenceValue == chinese)
                return;
        }

        table.arraySize += 1;
        table.GetArrayElementAtIndex(table.arraySize - 1).objectReferenceValue = chinese;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(font);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
