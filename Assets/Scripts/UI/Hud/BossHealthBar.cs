using UnityEngine;

/// <summary>
/// 巨龙 Boss 头顶血条：鳞甲熔岩条，比哥布林更宽。
/// </summary>
[RequireComponent(typeof(Monster))]
public sealed class BossHealthBar : MonsterHealthBar
{
    private const string DragonStyleResource = "UI/EnemyHealthBar/DragonHealthBarStyle";

    private void Reset()
    {
        ApplyBossDefaults();
    }

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(styleResourcePath))
            styleResourcePath = DragonStyleResource;
        base.Awake();
    }

    private void ApplyBossDefaults()
    {
        styleResourcePath = DragonStyleResource;
        if (style == null || style.name.IndexOf("Dragon", System.StringComparison.OrdinalIgnoreCase) < 0)
            style = Resources.Load<EnemyHealthBarStyle>(DragonStyleResource);

        worldOffset = new Vector3(0f, 4.85f, 0f);
        barSize = new Vector2(4.4f, 0.92f);
        pixelsPerUnit = 100f;
        sortingOrder = 80;
        hideWhenFull = false;
        hideOnDeath = true;
        smoothSpeed = 10f;
    }
}
