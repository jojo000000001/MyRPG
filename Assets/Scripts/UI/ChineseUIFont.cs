using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 为运行时 UI 提供可显示中文的系统字体（Legacy Text）。
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

    private static readonly string CommonCharacters =
        "等级经验生命攻击增伤暴击伤害吸血护甲魔抗角色属性还差升级";

    public static Font Get()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = Font.CreateDynamicFontFromOSFont(PreferredFamilies, 32);
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        cachedFont?.RequestCharactersInTexture(CommonCharacters, 32, FontStyle.Bold);
        cachedFont?.RequestCharactersInTexture(CommonCharacters, 32, FontStyle.Normal);

        return cachedFont;
    }

    public static void Apply(Text label, int fontSize, FontStyle style = FontStyle.Normal)
    {
        if (label == null)
            return;

        Font font = Get();
        label.font = font;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.supportRichText = true;
        font?.RequestCharactersInTexture(label.text, fontSize, style);
    }
}
