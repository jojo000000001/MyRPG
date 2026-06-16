/// <summary>
/// Optional lifecycle hooks for objects spawned from <see cref="GameObjectPoolService"/>.
/// </summary>
public interface IPoolable
{
    void OnSpawnedFromPool();
    void OnReturnedToPool();
}
