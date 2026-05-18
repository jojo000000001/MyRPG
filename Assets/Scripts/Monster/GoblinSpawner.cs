using UnityEngine;

/// <summary>
/// 负责在场景中生成一个 Goblin 实例，并缓存已生成对象避免重复生成。
/// </summary>
public class GoblinSpawner : MonoBehaviour
{
    [SerializeField] private GameObject goblinPrefab;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool keepSpawnedAsChild;

    // 保存当前生成出来的怪物，后续调用 Spawn 会直接复用。
    private GameObject spawnedGoblin;

    public GameObject SpawnedGoblin => spawnedGoblin;

    private void Start()
    {
        if (spawnOnStart)
            Spawn();
    }

    /// <summary>
    /// 生成 Goblin；如果已经生成过，则直接返回已有实例。
    /// </summary>
    public GameObject Spawn()
    {
        if (goblinPrefab == null)
        {
            Debug.LogWarning("GoblinSpawner is missing a goblin prefab.", this);
            return null;
        }

        if (spawnedGoblin != null)
            return spawnedGoblin;

        Transform parent = keepSpawnedAsChild ? transform : null;
        spawnedGoblin = Instantiate(goblinPrefab, transform.position, transform.rotation, parent);
        spawnedGoblin.name = goblinPrefab.name;
        return spawnedGoblin;
    }
}
