using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品分类，用来区分装备、消耗品等不同处理方式。
/// </summary>
public enum ItemType
{
    Weapon,
    Consumable
}

/// <summary>
/// 单个物品的数据配置，适合在 Inspector 中创建 ScriptableObject 资产。
/// </summary>
[CreateAssetMenu()]
public class ItemSO : ScriptableObject
{
    // 物品基础展示信息。
    public int id;
    public new string name;
    public ItemType itemType;
    public string description;

    // 可扩展属性列表，例如回血、加速、攻击力等数值效果。
    public List<ItemProperty> propertyList;
    public Sprite icon;
    public GameObject prefab;
}

/// <summary>
/// 一个物品属性条目，由属性类型和数值组成。
/// </summary>
[Serializable]
public class ItemProperty
{
    public ItemPropertyType PropertyType;
    public int Value;
}

public enum ItemPropertyType
{
    HPValue,
    EnergyValue,
    MentalValue,
    SpeedValue,
    AttackValue
}

