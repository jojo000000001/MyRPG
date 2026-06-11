using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 为运行时 UI 提供可显示中文的系统字体（TMP 默认字体不含 CJK 字形）。
/// </summary>
public static class ChineseUIFont
{
    private static Font cachedFont;

    private static readonly string[] PreferredFamilies =
    {
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "SimHei",
        "PingFang SC",
        "Noto Sans CJK SC",
        "Arial Unicode MS",
    };

    public static Font Get()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = Font.CreateDynamicFontFromOSFont(PreferredFamilies, 32);
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return cachedFont;
    }

    public static void Apply(Text label, int fontSize, FontStyle style = FontStyle.Normal)
    {
        if (label == null)
            return;

        label.font = Get();
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.supportRichText = true;
    }
}
