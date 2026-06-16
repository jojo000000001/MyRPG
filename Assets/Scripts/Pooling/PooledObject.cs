using UnityEngine;

/// <summary>
/// Tracks which prefab an instance came from so it can be returned to the correct pool.
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledObject : MonoBehaviour
{
    private GameObject sourcePrefab;

    public GameObject SourcePrefab => sourcePrefab;

    internal void AssignPrefab(GameObject prefab)
    {
        sourcePrefab = prefab;
    }

    public void Release()
    {
        GameObjectPoolService service = GameObjectPoolService.Instance;
        if (service != null)
            service.Release(gameObject);
        else
            Destroy(gameObject);
    }

    public static bool TryRelease(GameObject target)
    {
        if (target == null)
            return false;

        PooledObject pooled = target.GetComponent<PooledObject>();
        if (pooled == null)
            return false;

        pooled.Release();
        return true;
    }
}
