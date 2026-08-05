using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在场景中生成若干 Goblin，并注册到 DemoGameManager。
/// </summary>
public class GoblinSpawner : MonoBehaviour
{
    [SerializeField] private GameObject goblinPrefab;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool keepSpawnedAsChild;
    [SerializeField] private int spawnCount = 3;
    [SerializeField] private float spawnRadius = 2.5f;

    private readonly List<GameObject> spawnedGoblins = new List<GameObject>();

    public IReadOnlyList<GameObject> SpawnedGoblins => spawnedGoblins;

    private void Start()
    {
        if (spawnOnStart)
            Spawn();
    }

    /// <summary>
    /// 按环形分布生成 Goblin；重复调用不会再次生成。
    /// </summary>
    public IReadOnlyList<GameObject> Spawn()
    {
        if (goblinPrefab == null)
        {
            Debug.LogWarning("GoblinSpawner is missing a goblin prefab.", this);
            return spawnedGoblins;
        }

        if (spawnedGoblins.Count > 0)
            return spawnedGoblins;

        int count = Mathf.Max(1, spawnCount);
        Transform parent = keepSpawnedAsChild ? transform : null;
        GameObjectPoolService pool = GameObjectPoolService.EnsureInstance();
        if (pool == null)
            return spawnedGoblins;

        pool.Prewarm(goblinPrefab, count);

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = GetSpawnOffset(i, count);
            Vector3 position = transform.position + offset;
            GameObject instance = pool.Get(goblinPrefab, position, transform.rotation, parent);
            instance.name = $"{goblinPrefab.name}_{i + 1}";
            spawnedGoblins.Add(instance);

            Monster monster = instance.GetComponent<Monster>();
            if (monster != null && DemoGameManager.Instance != null)
                DemoGameManager.Instance.RegisterEnemy(monster);
        }

        return spawnedGoblins;
    }

    private Vector3 GetSpawnOffset(int index, int count)
    {
        if (count <= 1)
            return Vector3.zero;

        float angle = (360f / count) * index * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * spawnRadius, 0f, Mathf.Sin(angle) * spawnRadius);
    }
}
