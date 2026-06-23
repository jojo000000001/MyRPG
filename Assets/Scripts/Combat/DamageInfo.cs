using UnityEngine;

/// <summary>
/// 伤害类型：物理伤害吃护甲，魔法伤害吃魔抗，真实伤害不参与防御减免。
/// </summary>
public enum DamageType
{
    Physical,
    Magic,
    TrueDamage
}

/// <summary>
/// 一次伤害事件携带的数据：伤害值、类型、来源、命中点和击退方向等都从这里传递。
/// </summary>
public struct DamageInfo
{
    // 实际伤害数值，由受击目标决定是否接受或减免。
    public int amount;

    // 伤害类型，用于区分护甲、魔抗或真实伤害。
    public DamageType damageType;

    // 伤害来源，通常是发起攻击的角色根物体。
    public GameObject source;

    // 命中点，可用于播放受击特效或计算方向反馈。
    public Vector3 point;

    // 从攻击者指向受击者的方向，可用于击退、朝向或特效旋转。
    public Vector3 direction;

    // 是否暴击，用于飘字或特效区分。
    public bool isCritical;

    public DamageInfo(int amount, GameObject source, Vector3 point, Vector3 direction)
        : this(amount, source, point, direction, DamageType.Physical, false)
    {
    }

    public DamageInfo(int amount, GameObject source, Vector3 point, Vector3 direction, DamageType damageType)
        : this(amount, source, point, direction, damageType, false)
    {
    }

    public DamageInfo(int amount, GameObject source, Vector3 point, Vector3 direction, DamageType damageType, bool isCritical)
    {
        this.amount = amount;
        this.damageType = damageType;
        this.source = source;
        this.point = point;
        this.direction = direction;
        this.isCritical = isCritical;
    }
}

