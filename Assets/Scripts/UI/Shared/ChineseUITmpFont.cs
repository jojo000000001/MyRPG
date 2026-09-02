using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// TextMeshPro 中文字体（动态图集，避免方块/乱码）。
/// </summary>
public static class ChineseUITmpFont
{
    private static TMP_FontAsset cachedFont;

    public static TMP_FontAsset Get()
    {
        if (cachedFont != null)
            return cachedFont;

        Font source = ChineseUIFont.Get();
        if (source == null)
            return null;

        cachedFont = TMP_FontAsset.CreateFontAsset(
            source,
            32,
            5,
            GlyphRenderMode.SDFAA,
            2048,
            2048,
            AtlasPopulationMode.Dynamic);

        if (cachedFont != null && cachedFont.material != null)
        {
            Shader shader = Shader.Find("TextMeshPro/Distance Field");
            if (shader != null)
                cachedFont.material.shader = shader;
        }

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
    }
}
