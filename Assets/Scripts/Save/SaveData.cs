using System;

[Serializable]
public sealed class SaveData
{
    public int version = 1;
    public string sceneName;
    public string savedAtUtc;
    public float posX;
    public float posY;
    public float posZ;
    public float rotY;
    public PlayerSaveData player = new PlayerSaveData();
    public InventorySaveData inventory = new InventorySaveData();
}

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
    public int equippedWeaponId;
}

[Serializable]
public sealed class InventorySaveData
{
    public int capacity = 20;
    public int circularInsertCursor;
    public InventorySlotSaveData[] slots = Array.Empty<InventorySlotSaveData>();
}

[Serializable]
public sealed class InventorySlotSaveData
{
    public int itemId;
    public int amount;
}
