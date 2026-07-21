using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveSystem
{
    public const int CurrentVersion = 1;
    public const string SaveFileName = "save.json";

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static bool HasSave => File.Exists(SavePath);

    public static bool Save(Player player, Inventory inventory)
    {
        if (player == null || inventory == null)
        {
            Debug.LogWarning("SaveSystem: Missing player or inventory.");
            return false;
        }

        try
        {
            SaveData data = Capture(player, inventory);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"SaveSystem: Saved to {SavePath}");
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
        data = null;
        if (!HasSave)
            return false;

        try
        {
            string json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                Debug.LogWarning("SaveSystem: Save file parsed to null.");
                return false;
            }

            if (data.version != CurrentVersion)
            {
                Debug.LogWarning($"SaveSystem: Unsupported save version {data.version}. Expected {CurrentVersion}.");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveSystem: Read failed. {exception.Message}");
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
        if (!TryRead(out SaveData data))
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
}
