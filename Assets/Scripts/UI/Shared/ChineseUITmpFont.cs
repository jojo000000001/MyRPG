using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// TextMeshPro 中文字体。优先使用项目内生成的动态 SDF，避免系统字体创建失败导致方块。
/// </summary>
public static class ChineseUITmpFont
{
    private const string ResourcePath = "UIFonts/ChineseSDF";
    private static TMP_FontAsset cachedFont;
    private static bool fallbackInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Get();
    }

    public static TMP_FontAsset Get()
    {
        if (cachedFont != null)
        {
            InstallAsFallback(cachedFont);
            return cachedFont;
        }

        cachedFont = Resources.Load<TMP_FontAsset>(ResourcePath);
        if (cachedFont == null)
            cachedFont = CreateFromProjectOrOsFont();

        InstallAsFallback(cachedFont);
        return cachedFont;
    }

    public static void Apply(TextMeshProUGUI label, float fontSize, FontStyles style = FontStyles.Normal)
    {
        if (label == null)
            return;

        TMP_FontAsset font = Get();
        if (font != null)
            label.font = font;

        label.fontSize = fontSize;
        label.fontStyle = style;
        label.raycastTarget = false;

        if (font != null && !string.IsNullOrEmpty(label.text))
            font.TryAddCharacters(label.text);
    }

    private static TMP_FontAsset CreateFromProjectOrOsFont()
    {
        Font source = ChineseUIFont.Get();
        if (source == null)
            return null;

        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
            source,
            90,
            9,
            GlyphRenderMode.SDFAA,
            2048,
            2048,
            AtlasPopulationMode.Dynamic,
            true);

        if (font == null)
            return null;

        font.name = "ChineseUI-Dynamic";
        AssignShader(font);
        return font;
    }

    private static void AssignShader(TMP_FontAsset font)
    {
        if (font == null || font.material == null)
            return;

        Shader shader = Shader.Find("TextMeshPro/Distance Field");
        if (shader == null)
            shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
        if (shader != null)
            font.material.shader = shader;
    }

    private static void InstallAsFallback(TMP_FontAsset chinese)
    {
        if (fallbackInstalled || chinese == null)
            return;

        fallbackInstalled = true;
        AddFallback(TMP_Settings.fallbackFontAssets, chinese);

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
            AddFallback(defaultFont.fallbackFontAssetTable, chinese);
    }

    private static void AddFallback(System.Collections.Generic.List<TMP_FontAsset> list, TMP_FontAsset chinese)
    {
        if (list == null || chinese == null)
            return;

        if (!list.Contains(chinese))
            list.Add(chinese);
    }
}
