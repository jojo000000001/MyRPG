using UnityEngine;

/// <summary>
/// 将消耗品的 propertyList 效果应用到玩家身上。
/// </summary>
public static class ConsumableEffectApplicator
{
    public const float DefaultAttackBuffDurationSeconds = 15f;
    public const float DefaultSpeedBuffDurationSeconds = 6f;
    public static void Apply(ItemSO item, Player player)
    {
        if (item == null || player == null || player.IsDead || item.propertyList == null)
            return;

        for (int i = 0; i < item.propertyList.Count; i++)
        {
            ItemProperty property = item.propertyList[i];
            if (property == null || property.Value == 0)
                continue;

            ApplyProperty(item, player, property);
        }
    }

    private static void ApplyProperty(ItemSO item, Player player, ItemProperty property)
    {
        switch (property.PropertyType)
        {
            case ItemPropertyType.HPValue:
                player.RestoreHp(property.Value);
                break;
            case ItemPropertyType.EnergyValue:
                player.RestoreEnergy(property.Value);
                break;
            case ItemPropertyType.MentalValue:
                player.RestoreMental(property.Value);
                break;
            case ItemPropertyType.SpeedValue:
                player.ApplySpeedBuff(property.Value, DefaultSpeedBuffDurationSeconds);
                break;
            case ItemPropertyType.AttackValue:
                if (item.itemType == ItemType.Consumable)
                    player.ApplyAttackBuff(property.Value, DefaultAttackBuffDurationSeconds);
                break;
        }
    }
}
