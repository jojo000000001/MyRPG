using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveSystem
{
    public const int CurrentVersion = 1;
    public const int SlotCount = 3;
    public const string LegacySaveFileName = "save.json";

    public static string SavePath => GetSavePath(SaveSession.ActiveSlot);

    public static bool HasActiveSave => HasSave(SaveSession.ActiveSlot);

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

    public static int FindPreferredNewGameSlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (!HasSave(i))
                return i;
        }

        return 0;
    }

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
        summary.sceneName = string.IsNullOrEmpty(data.sceneName) ? "SampleScene" : data.sceneName;
        summary.savedAtDisplay = FormatSavedAt(data.savedAtUtc);
        return summary;
    }

    public static bool Save(Player player, Inventory inventory)
    {
        return Save(SaveSession.ActiveSlot, player, inventory);
    }

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

            if (data.version != CurrentVersion)
            {
                Debug.LogWarning(
                    $"SaveSystem: Slot {slotIndex + 1} has unsupported version {data.version}. Expected {CurrentVersion}.");
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
        ApplyTransform(player, data);
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

    public static SaveData Capture(Player player, Inventory inventory)
    {
        Transform transform = player.transform;
        return new SaveData
        {
            version = CurrentVersion,
            sceneName = SceneManager.GetActiveScene().name,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            posX = transform.position.x,
            posY = transform.position.y,
            posZ = transform.position.z,
            rotY = transform.eulerAngles.y,
            player = player.CaptureSaveData(),
            inventory = inventory.CaptureSaveData(),
        };
    }

    private static void ApplyTransform(Player player, SaveData data)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        Transform transform = player.transform;
        transform.SetPositionAndRotation(
            new Vector3(data.posX, data.posY, data.posZ),
            Quaternion.Euler(0f, data.rotY, 0f));

        if (controller != null)
            controller.enabled = true;
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

[Serializable]
public struct SaveSlotSummary
{
    public int slotIndex;
    public bool hasSave;
    public int level;
    public int currentHp;
    public int maxHp;
    public string sceneName;
    public string savedAtDisplay;
}
