using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-scoped GameObject pools for enemies and world drops.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameObjectPoolService : MonoBehaviour
{
    public static GameObjectPoolService Instance { get; private set; }

    private readonly Dictionary<int, Pool> pools = new Dictionary<int, Pool>();
    private Transform poolRoot;

    public static GameObjectPoolService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObjectPoolService existing = FindObjectOfType<GameObjectPoolService>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject(nameof(GameObjectPoolService));
        return host.AddComponent<GameObjectPoolService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        poolRoot = new GameObject("PoolRoot").transform;
        poolRoot.SetParent(transform, false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        Pool pool = GetOrCreatePool(prefab);
        for (int i = 0; i < count; i++)
            pool.Return(CreateInstance(prefab));
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
            return null;

        GameObjectPoolService service = EnsureInstance();
        Pool pool = service.GetOrCreatePool(prefab);
        GameObject instance = pool.Take() ?? service.CreateInstance(prefab);
        Transform instanceTransform = instance.transform;

        if (parent != null)
            instanceTransform.SetParent(parent, false);
        else
            instanceTransform.SetParent(null, false);

        instanceTransform.SetPositionAndRotation(position, rotation);
        NotifySpawned(instance);
        instance.SetActive(true);
        return instance;
    }

    public void Release(GameObject instance)
    {
        if (instance == null)
            return;

        PooledObject pooled = instance.GetComponent<PooledObject>();
        if (pooled == null || pooled.SourcePrefab == null)
        {
            Destroy(instance);
            return;
        }

        NotifyReturned(instance);
        instance.SetActive(false);
        instance.transform.SetParent(poolRoot, false);
        GetOrCreatePool(pooled.SourcePrefab).Return(instance);
    }

    private Pool GetOrCreatePool(GameObject prefab)
    {
        int key = prefab.GetInstanceID();
        if (!pools.TryGetValue(key, out Pool pool))
        {
            pool = new Pool();
            pools.Add(key, pool);
        }

        return pool;
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, poolRoot);
        instance.name = prefab.name;
        instance.SetActive(false);

        PooledObject pooled = instance.GetComponent<PooledObject>();
        if (pooled == null)
            pooled = instance.AddComponent<PooledObject>();

        pooled.AssignPrefab(prefab);
        return instance;
    }

    private static void NotifySpawned(GameObject instance)
    {
        IPoolable[] poolables = instance.GetComponentsInChildren<IPoolable>(true);
        for (int i = 0; i < poolables.Length; i++)
            poolables[i].OnSpawnedFromPool();
    }

    private static void NotifyReturned(GameObject instance)
    {
        IPoolable[] poolables = instance.GetComponentsInChildren<IPoolable>(true);
        for (int i = 0; i < poolables.Length; i++)
            poolables[i].OnReturnedToPool();
    }

    private sealed class Pool
    {
        private readonly Stack<GameObject> inactive = new Stack<GameObject>();

        public GameObject Take()
        {
            return inactive.Count > 0 ? inactive.Pop() : null;
        }

        public void Return(GameObject instance)
        {
            inactive.Push(instance);
        }
    }
}
