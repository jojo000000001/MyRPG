using UnityEngine;

/// <summary>
/// Boss 头顶血条：同一套 Kenney 条，尺寸更大，满血时隐藏。
/// </summary>
[RequireComponent(typeof(Monster))]
public sealed class BossHealthBar : MonsterHealthBar
{
    private void Reset()
    {
        ApplyBossDefaults();
    }

    protected override void Awake()
    {
        ApplyBossDefaults();
        base.Awake();
    }

    private void ApplyBossDefaults()
    {
        worldOffset = new Vector3(0f, 4.2f, 0f);
        barSize = new Vector2(2.8f, 0.24f);
        pixelsPerUnit = 100f;
        sortingOrder = 80;
        hideWhenFull = true;
        hideOnDeath = true;
        smoothSpeed = 10f;
    }
}
