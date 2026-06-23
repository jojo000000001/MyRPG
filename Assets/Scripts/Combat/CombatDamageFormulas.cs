using UnityEngine;

/// <summary>
/// 统一伤害公式：基础攻击 × 增伤 × [暴击倍率] × 100 / (100 + 防御)。
/// </summary>
public static class CombatDamageFormulas
{
    /// <summary>护甲/魔抗减伤倍率：100 / (100 + 防御)。</summary>
    public static float GetDefenseMultiplier(int defense)
    {
        defense = Mathf.Max(0, defense);
        return 100f / (100f + defense);
    }

    /// <summary>
    /// 计算最终伤害。暴击时额外乘以暴击倍率。
    /// </summary>
    public static int Calculate(
        int baseAttack,
        float damageBonusPercent,
        float critChance,
        float critDamageMultiplier,
        int targetDefense,
        out bool isCritical)
    {
        float bonusMultiplier = 1f + damageBonusPercent * 0.01f;
        isCritical = UnityEngine.Random.value < critChance;

        float value = Mathf.Max(0f, baseAttack) * bonusMultiplier;
        if (isCritical)
            value *= critDamageMultiplier;
        value *= GetDefenseMultiplier(targetDefense);

        return Mathf.Max(1, Mathf.RoundToInt(value));
    }

    /// <summary>根据伤害类型取目标对应防御（真实伤害视为 0 防御）。</summary>
    public static int GetTargetDefense(IDamageable target, DamageType damageType)
    {
        if (target == null || damageType == DamageType.TrueDamage)
            return 0;

        if (target is Player player)
            return player.GetDefense(damageType);

        if (target is Monster monster)
            return monster.GetDefense(damageType);

        return 0;
    }
}
