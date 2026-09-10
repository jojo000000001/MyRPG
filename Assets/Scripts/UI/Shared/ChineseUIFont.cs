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
        "Noto Sans SC",
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "微软雅黑 UI",
        "微软雅黑",
        "SimHei",
        "黑体",
        "PingFang SC",
        "Noto Sans CJK SC",
        "Arial Unicode MS",
    };

    private static readonly string CommonCharacters =
        "等级经验生命攻击增伤暴击伤害吸血护甲魔抗角色属性还差升级商店购买出售离开卢比背包数量返回不足已满回满闪避左键右键跳跃加速";

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
