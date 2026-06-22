using UnityEngine;

/// <summary>
/// Boss 头顶血条：更大、偏红，满血时隐藏。
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
        borderPixels = 4f;
        sortingOrder = 80;
        hideWhenFull = true;
        hideOnDeath = true;
        smoothSpeed = 10f;
        frameColor = new Color(0.05f, 0.03f, 0.02f, 0.92f);
        backgroundColor = new Color(0.22f, 0.05f, 0.04f, 0.9f);
        highHealthColor = new Color(0.92f, 0.18f, 0.12f, 0.98f);
        lowHealthColor = new Color(0.98f, 0.08f, 0.05f, 0.98f);
    }
}
