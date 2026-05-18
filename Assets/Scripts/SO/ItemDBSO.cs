using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品数据库资产，集中保存项目中可用的 ItemSO 配置。
/// </summary>
[CreateAssetMenu]
public class ItemDBSO : ScriptableObject
{
    public List<ItemSO> itemList;
}

