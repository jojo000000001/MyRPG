using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 存档文件的读写入口：三个槽位的 JSON、采集/还原、以及槽位摘要。
/// </summary>
public static class SaveSystem
{
    /// <summary>当前写入的格式版本。读取时接受 1 到该值。</summary>
    public const int CurrentVersion = 2;
    /// <summary>主菜单和暂停菜单共用的槽位数。</summary>
    public const int SlotCount = 3;
    /// <summary>更早版本单文件存档的文件名，启动时会迁到槽位 0。</summary>
    public const string LegacySaveFileName = "save.json";

    public static string SavePath => GetSavePath(SaveSession.ActiveSlot);

    public static bool HasActiveSave => SaveSession.HasValidActiveSlot && HasSave(SaveSession.ActiveSlot);

    /// <summary>
    /// 若还没有槽位文件、但 persistentDataPath 里有旧的 save.json，则复制到槽位 0。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void MigrateLegacySaveIfNeeded()
    {
        string legacyPath = Path.Combine(Application.persistentDataPath, LegacySaveFileName);
        if (!File.Exists(legacyPath))
            return;

        string slotZeroPath = GetSavePath(0);
        if (File.Exists(slotZeroPath))
            return;

        try
        {
            File.Copy(legacyPath, slotZeroPath);
            Debug.Log($"SaveSystem: Migrated legacy save to slot 0 ({slotZeroPath}).");
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveSystem: Legacy save migration failed. {exception.Message}");
        }
    }

    /// <summary>槽位对应的磁盘路径：save_slot_{index}.json。</summary>
    public static string GetSavePath(int slotIndex)
    {
        ValidateSlotIndex(slotIndex);
        return Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
    }

    public static bool HasSave(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        return File.Exists(GetSavePath(slotIndex));
    }

    public static bool HasAnySave()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (HasSave(i))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 按保存时间找最近用过的槽，主菜单用来标「最近」。
    /// </summary>
    public static bool TryGetMostRecentSlot(out int slotIndex)
    {
        slotIndex = SaveSession.InvalidSlot;
        DateTime latestSavedAt = DateTime.MinValue;

        for (int i = 0; i < SlotCount; i++)
        {
            if (!HasSave(i))
                continue;

            DateTime savedAt = GetSaveTimestampUtc(i);
            if (savedAt < latestSavedAt)
                continue;

            latestSavedAt = savedAt;
            slotIndex = i;
        }

        return IsValidSlotIndex(slotIndex);
    }

    /// <summary>优先返回空槽；三个都有存档时回落到槽位 0。</summary>
    public static int FindPreferredNewGameSlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (!HasSave(i))
                return i;
        }

        return 0;
    }

    /// <summary>
    /// 槽位列表用的摘要：等级、卢比、任务一句、时长和保存时间。
    /// </summary>
    public static SaveSlotSummary GetSlotSummary(int slotIndex)
    {
        var summary = new SaveSlotSummary
        {
            slotIndex = slotIndex,
            hasSave = false,
        };

        if (!TryRead(slotIndex, out SaveData data) || data?.player == null)
            return summary;

        summary.hasSave = true;
        summary.level = data.player.level;
        summary.currentHp = data.player.currentHp;
        summary.maxHp = data.player.maxHp;
        summary.rupees = Mathf.Max(0, data.player.rupees);
        summary.questLabel = data.quest != null ? data.quest.GetSummaryLabel() : "旅途开始";
        summary.playTimeDisplay = FormatPlayTime(data.playTimeSeconds);
        summary.sceneName = string.IsNullOrEmpty(data.sceneName) ? "SampleScene" : data.sceneName;
        summary.savedAtDisplay = FormatSavedAt(data.savedAtUtc);
        return summary;
    }

    public static bool Save(Player player, Inventory inventory)
    {
        return Save(SaveSession.ActiveSlot, player, inventory);
    }

    /// <summary>
    /// 采集当前进度并写入指定槽。槽位非法或缺少玩家/背包时失败。
    /// </summary>
    public static bool Save(int slotIndex, Player player, Inventory inventory)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        if (player == null || inventory == null)
        {
            Debug.LogWarning("SaveSystem: Missing player or inventory.");
            return false;
        }

        try
        {
            SaveData data = Capture(player, inventory);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetSavePath(slotIndex), json);
            Debug.Log($"SaveSystem: Saved slot {slotIndex + 1} to {GetSavePath(slotIndex)}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveSystem: Save failed. {exception.Message}");
            return false;
        }
    }

    public static bool TryRead(out SaveData data)
    {
        return TryRead(SaveSession.ActiveSlot, out data);
    }

    /// <summary>
    /// 读槽位 JSON。缺文件或版本不支持时失败；缺的嵌套对象会补成空结构，旧存档仍能读。
    /// </summary>
    public static bool TryRead(int slotIndex, out SaveData data)
    {
        data = null;
        if (!HasSave(slotIndex))
            return false;

        try
        {
            string json = File.ReadAllText(GetSavePath(slotIndex));
            data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                Debug.LogWarning($"SaveSystem: Slot {slotIndex + 1} parsed to null.");
                return false;
            }

            Normalize(data);
            if (data.version < 1 || data.version > CurrentVersion)
            {
                Debug.LogWarning(
                    $"SaveSystem: Slot {slotIndex + 1} has unsupported version {data.version}. Expected 1..{CurrentVersion}.");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveSystem: Read slot {slotIndex + 1} failed. {exception.Message}");
            return false;
        }
    }

    public static bool Delete(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        string path = GetSavePath(slotIndex);
        if (!File.Exists(path))
            return true;

        try
        {
            File.Delete(path);
            Debug.Log($"SaveSystem: Deleted slot {slotIndex + 1}.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveSystem: Delete slot {slotIndex + 1} failed. {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// 把存档套到当前场景：人物、背包、坐标、世界单位、任务、镜头和累计时长。
    /// 应在哥布林营地已经生成之后调用。
    /// </summary>
    public static bool Apply(SaveData data, Player player, Inventory inventory, ItemCatalog itemCatalog)
    {
        if (data == null || player == null || inventory == null)
            return false;

        if (itemCatalog == null)
            itemCatalog = ItemCatalog.EnsureAvailable();

        if (itemCatalog == null)
        {
            Debug.LogError("SaveSystem: ItemCatalog is missing.");
            return false;
        }

        player.ApplySaveData(data.player, itemCatalog);
        inventory.ApplySaveData(data.inventory, itemCatalog);
        ApplyPlayerTransform(player, data);
        WorldSaveState.Apply(data.world);

        if (data.quest != null && (data.quest.knightGreeted || HasAnyQuestProgress(data.quest)))
            QuestManager.Ensure().ApplySaveData(data.quest, data.world);

        ApplyCamera(data.camera);
        SaveSession.MarkPlayTime(data.playTimeSeconds);
        Debug.Log("SaveSystem: Save applied.");
        return true;
    }

    public static bool Load(Player player, Inventory inventory, ItemCatalog itemCatalog)
    {
        return Load(SaveSession.ActiveSlot, player, inventory, itemCatalog);
    }

    public static bool Load(int slotIndex, Player player, Inventory inventory, ItemCatalog itemCatalog)
    {
        if (!TryRead(slotIndex, out SaveData data))
            return false;

        return Apply(data, player, inventory, itemCatalog);
    }

    /// <summary>
    /// 从当前场景采集一份完整存档。没有 QuestManager 时仍会记下犬骑士是否打过招呼。
    /// </summary>
    public static SaveData Capture(Player player, Inventory inventory)
    {
        Transform transform = player.transform;
        ThirdPersonCameraRig cameraRig = UnityEngine.Object.FindObjectOfType<ThirdPersonCameraRig>();
        float yaw = 0f;
        float pitch = 0f;
        bool hasLook = cameraRig != null;
        if (hasLook)
            cameraRig.CaptureLook(out yaw, out pitch);

        return new SaveData
        {
            version = CurrentVersion,
            sceneName = SceneManager.GetActiveScene().name,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            playTimeSeconds = SaveSession.CurrentPlayTimeSeconds,
            posX = transform.position.x,
            posY = transform.position.y,
            posZ = transform.position.z,
            rotY = transform.eulerAngles.y,
            player = player.CaptureSaveData(),
            inventory = inventory.CaptureSaveData(),
            quest = QuestManager.Instance != null
                ? QuestManager.Instance.CaptureSaveData()
                : CaptureQuestFromNpc(),
            world = WorldSaveState.Capture(),
            camera = new CameraSaveData
            {
                hasLook = hasLook,
                yaw = yaw,
                pitch = pitch,
            },
        };
    }

    /// <summary>
    /// 瞬移玩家。先关掉 CharacterController，避免把人弹开。
    /// </summary>
    public static void ApplyPlayerTransform(Player player, SaveData data)
    {
        if (player == null || data == null)
            return;

        Vector3 position = new Vector3(data.posX, data.posY, data.posZ);
        Quaternion rotation = Quaternion.Euler(0f, data.rotY, 0f);
        Transform transform = player.transform;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        Rigidbody rigidbody = player.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (rigidbody != null)
        {
            rigidbody.position = position;
            rigidbody.rotation = rotation;
        }

        if (controller != null)
        {
            controller.enabled = true;
            controller.Move(Vector3.zero);
        }

        Physics.SyncTransforms();
    }

    private static void ApplyCamera(CameraSaveData data)
    {
        if (data == null || !data.hasLook)
            return;

        ThirdPersonCameraRig rig = UnityEngine.Object.FindObjectOfType<ThirdPersonCameraRig>();
        if (rig != null)
            rig.ApplyLook(data.yaw, data.pitch);
    }

    /// <summary>任务系统还没创建时，只从犬骑士身上采「是否打过招呼」。</summary>
    private static QuestSaveData CaptureQuestFromNpc()
    {
        DogKnightNpc knight = UnityEngine.Object.FindObjectOfType<DogKnightNpc>();
        return new QuestSaveData
        {
            knightGreeted = knight != null && knight.HasGreeted,
        };
    }

    private static bool HasAnyQuestProgress(QuestSaveData quest)
    {
        if (quest == null)
            return false;

        return quest.huntGoblinStatus > 0
            || quest.huntAllStatus > 0
            || quest.slayDragonStatus > 0;
    }

    /// <summary>
    /// 旧 JSON 缺嵌套对象时补空结构，避免读档空引用。
    /// </summary>
    private static void Normalize(SaveData data)
    {
        if (data.player == null)
            data.player = new PlayerSaveData();
        if (data.inventory == null)
            data.inventory = new InventorySaveData();
        if (data.quest == null)
            data.quest = new QuestSaveData();
        if (data.world == null)
            data.world = new WorldSaveData();
        if (data.camera == null)
            data.camera = new CameraSaveData();
        if (data.world.actors == null)
            data.world.actors = Array.Empty<WorldActorSaveData>();
        if (data.inventory.slots == null)
            data.inventory.slots = Array.Empty<InventorySlotSaveData>();
        if (data.inventory.hotbarItemIds == null)
            data.inventory.hotbarItemIds = Array.Empty<int>();
    }

    private static string FormatPlayTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        if (hours > 0)
            return hours + "小时" + minutes + "分";
        return minutes + "分钟";
    }

    private static string FormatSavedAt(string savedAtUtc)
    {
        if (string.IsNullOrEmpty(savedAtUtc))
            return "未知时间";

        if (!DateTime.TryParse(savedAtUtc, out DateTime savedAt))
            return savedAtUtc;

        return savedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private static DateTime GetSaveTimestampUtc(int slotIndex)
    {
        if (TryRead(slotIndex, out SaveData data) &&
            !string.IsNullOrEmpty(data.savedAtUtc) &&
            DateTime.TryParse(data.savedAtUtc, out DateTime savedAt))
        {
            return savedAt.ToUniversalTime();
        }

        return File.GetLastWriteTimeUtc(GetSavePath(slotIndex));
    }

    private static bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount;
    }

    private static void ValidateSlotIndex(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, $"Slot index must be 0..{SlotCount - 1}.");
    }
}

/// <summary>
/// 存档槽列表上展示的摘要，不包含完整进度。
/// </summary>
[Serializable]
public struct SaveSlotSummary
{
    public int slotIndex;
    public bool hasSave;
    public int level;
    public int currentHp;
    public int maxHp;
    public int rupees;
    public string questLabel;
    public string playTimeDisplay;
    public string sceneName;
    public string savedAtDisplay;
}
