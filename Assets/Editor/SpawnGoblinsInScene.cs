using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在 SampleScene 中放置 GameObjectPoolService 并生成 Goblin。
/// </summary>
public static class SpawnGoblinsInScene
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PoolPrefabPath = "Assets/Prefabs/Systems/GameObjectPoolService.prefab";
    private const string GoblinPrefabPath = "Assets/Prefabs/Goblin.prefab";

    [MenuItem("Tools/MyRPG/Spawn Goblins In Scene")]
    public static void SpawnFromMenu()
    {
        string result = Spawn();
        EditorUtility.DisplayDialog("Spawn Goblins", result, "OK");
    }

    internal static string Spawn()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsurePoolServiceInScene();

        GoblinSpawner spawner = Object.FindObjectOfType<GoblinSpawner>();
        if (spawner == null)
            return "GoblinSpawner not found in SampleScene.";

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        GameObject goblinPrefab = serializedSpawner.FindProperty("goblinPrefab").objectReferenceValue as GameObject;
        if (goblinPrefab == null)
            goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath);
        if (goblinPrefab == null)
            return "Goblin prefab missing.";

        int spawnCount = Mathf.Max(1, serializedSpawner.FindProperty("spawnCount").intValue);
        float spawnRadius = serializedSpawner.FindProperty("spawnRadius").floatValue;

        RemoveExistingGoblins();

        Transform spawnerTransform = spawner.transform;
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 offset = GetSpawnOffset(i, spawnCount, spawnRadius);
            Vector3 position = spawnerTransform.position + offset;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(goblinPrefab, scene);
            instance.name = $"{goblinPrefab.name}_{i + 1}";
            instance.transform.SetPositionAndRotation(position, spawnerTransform.rotation);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Spawned {spawnCount} goblins near GoblinSpawner.\nGameObjectPoolService is ready for Play mode.";
    }

    private static void EnsurePoolServiceInScene()
    {
        if (Object.FindObjectOfType<GameObjectPoolService>() != null)
            return;

        GameObject poolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PoolPrefabPath);
        if (poolPrefab == null)
        {
            Debug.LogError($"SpawnGoblinsInScene: missing prefab at {PoolPrefabPath}. Run Tools/MyRPG/Setup System Prefabs (P0).");
            return;
        }

        GameObject poolInstance = (GameObject)PrefabUtility.InstantiatePrefab(poolPrefab);
        poolInstance.name = "GameObjectPoolService";
    }

    private static void RemoveExistingGoblins()
    {
        Goblin[] existing = Object.FindObjectsOfType<Goblin>();
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                Object.DestroyImmediate(existing[i].gameObject);
        }
    }

    private static Vector3 GetSpawnOffset(int index, int count, float radius)
    {
        if (count <= 1)
            return Vector3.zero;

        float angle = (360f / count) * index * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }
}
