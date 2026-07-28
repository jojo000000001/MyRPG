using UnityEngine;

/// <summary>
/// 新游戏默认出生点（与 SampleScene 中 Player 初始位置一致）。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSpawnPoint : MonoBehaviour
{
    private static GameSpawnPoint instance;

    [SerializeField] private Vector3 spawnPosition = new Vector3(36.6f, 0.81f, -40.23f);
    [SerializeField] private float spawnRotationY = 352.3f;

    public static GameSpawnPoint Instance => instance;

    public Vector3 SpawnPosition => spawnPosition;
    public float SpawnRotationY => spawnRotationY;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static GameSpawnPoint FindInScene()
    {
        if (instance != null)
            return instance;

        return FindObjectOfType<GameSpawnPoint>();
    }

    public void SetSpawn(Vector3 position, float rotationY)
    {
        spawnPosition = position;
        spawnRotationY = rotationY;
    }

    public SaveData CreateNewGameSaveData()
    {
        return new SaveData
        {
            version = SaveSystem.CurrentVersion,
            sceneName = "SampleScene",
            savedAtUtc = System.DateTime.UtcNow.ToString("o"),
            posX = spawnPosition.x,
            posY = spawnPosition.y,
            posZ = spawnPosition.z,
            rotY = spawnRotationY,
            player = new PlayerSaveData(),
            inventory = new InventorySaveData(),
        };
    }

    public void ApplyTo(Player player)
    {
        if (player == null)
            return;

        SaveSystem.ApplyPlayerTransform(player, CreateNewGameSaveData());
    }
}
