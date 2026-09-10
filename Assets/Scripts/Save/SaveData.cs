using System;

/// <summary>
/// 一份完整存档。JsonUtility 按字段名读写，缺字段时用这里的默认值。
/// </summary>
[Serializable]
public sealed class SaveData
{
    /// <summary>存档格式版本。当前为 2，读取时接受 1..CurrentVersion。</summary>
    public int version = 1;
    /// <summary>保存时所在场景名，读档时用来决定加载哪张场景。</summary>
    public string sceneName;
    /// <summary>保存时刻（UTC，ISO-8601）。</summary>
    public string savedAtUtc;
    /// <summary>累计游戏时长（秒）。</summary>
    public float playTimeSeconds;
    /// <summary>玩家世界坐标。</summary>
    public float posX;
    public float posY;
    public float posZ;
    /// <summary>玩家水平朝向（度）。</summary>
    public float rotY;
    public PlayerSaveData player = new PlayerSaveData();
    public InventorySaveData inventory = new InventorySaveData();
    public QuestSaveData quest = new QuestSaveData();
    public WorldSaveData world = new WorldSaveData();
    public CameraSaveData camera = new CameraSaveData();
}

/// <summary>
/// 玩家属性、装备与钱包。armor / 盾牌耐久用 -1 表示旧存档未写入，读档时保留场景默认值。
/// </summary>
[Serializable]
public sealed class PlayerSaveData
{
    public int level = 1;
    public int experience;
    public int maxHp = 100;
    public int currentHp = 100;
    public int maxEnergy = 100;
    public int currentEnergy = 100;
    public int maxMental = 100;
    public int currentMental = 100;
    public int attackPower = 10;
    /// <summary>护甲。-1 表示旧存档没有这项，不要覆盖。</summary>
    public int armor = -1;
    /// <summary>当前盾牌耐久。-1 表示旧存档没有这项，装备后再按上限补满。</summary>
    public int currentShieldDurability = -1;
    public int equippedWeaponId;
    public int equippedShieldId;
    public int rupees = 80;
}

/// <summary>
/// 背包格子与快捷栏绑定。物品本身仍只存一份，快捷栏用物品 id 引用。
/// </summary>
[Serializable]
public sealed class InventorySaveData
{
    public int capacity = 20;
    /// <summary>旧版循环插入游标，读档时仍还原，新获得物品已改为顺序填空位。</summary>
    public int circularInsertCursor;
    public InventorySlotSaveData[] slots = Array.Empty<InventorySlotSaveData>();
    /// <summary>快捷栏每个格子绑定的物品 id，0 表示空。</summary>
    public int[] hotbarItemIds = Array.Empty<int>();
}

/// <summary>
/// 单个背包格：物品 id 与数量。空格 id/数量为 0。
/// </summary>
[Serializable]
public sealed class InventorySlotSaveData
{
    public int itemId;
    public int amount;
}

/// <summary>
/// 三条主线任务的状态与进度，以及犬骑士是否已经打过招呼。
/// 状态值对应 <see cref="QuestManager.Status"/>：0 未接取 / 1 进行中 / 2 已完成。
/// </summary>
[Serializable]
public sealed class QuestSaveData
{
    public int huntGoblinStatus;
    public int huntGoblinProgress;
    public int huntAllStatus;
    public int huntAllProgress;
    /// <summary>清剿林地任务的目标数量，读档时不能按当前存活数重算。</summary>
    public int huntAllRequiredCount;
    public int slayDragonStatus;
    public int slayDragonProgress;
    public bool swordHintShown;
    public int startingWeaponId;
    public int forestGoblinKillCount;
    public bool knightGreeted;

    /// <summary>
    /// 存档槽位上显示的一句任务摘要。
    /// </summary>
    public string GetSummaryLabel()
    {
        if (slayDragonStatus == (int)QuestManager.Status.Completed)
            return "巨龙已击败";
        if (slayDragonStatus == (int)QuestManager.Status.Active)
            return "讨伐巨龙";
        if (huntAllStatus == (int)QuestManager.Status.Completed)
            return "林地已清剿";
        if (huntAllStatus == (int)QuestManager.Status.Active)
            return "清剿林地哥布林";
        if (huntGoblinStatus == (int)QuestManager.Status.Completed)
            return "入侵者已击败";
        if (huntGoblinStatus == (int)QuestManager.Status.Active)
            return "讨伐入侵的哥布林";
        if (knightGreeted)
            return "已与犬骑士会面";
        return "旅途开始";
    }
}

/// <summary>
/// 场景里需要跨存档保留的单位，以及巨龙是否已落地、Demo 是否已经结束。
/// </summary>
[Serializable]
public sealed class WorldSaveData
{
    public WorldActorSaveData[] actors = Array.Empty<WorldActorSaveData>();
    /// <summary>巨龙是否已经从树桩上空落地，可以交战。</summary>
    public bool dragonDescended;
    /// <summary>胜负界面是否已经出现过，读档后不要再弹一次。</summary>
    public bool demoEnded;
}

/// <summary>
/// 一个可还原单位：用名字/固定 id 匹配场景对象，并记下存活、血量和姿态。
/// </summary>
[Serializable]
public sealed class WorldActorSaveData
{
    public string id;
    public bool alive = true;
    public int hp;
    public float x;
    public float y;
    public float z;
    public float rotY;
}

/// <summary>
/// 第三人称镜头的水平角和俯仰角。hasLook 为 false 时读档用默认朝向。
/// </summary>
[Serializable]
public sealed class CameraSaveData
{
    public bool hasLook;
    public float yaw;
    public float pitch;
}
