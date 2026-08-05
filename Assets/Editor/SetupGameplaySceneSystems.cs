using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 在 SampleScene 放置场景级系统（P1）。
/// </summary>
public static class SetupGameplaySceneSystems
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PoolServicePrefabPath = "Assets/Prefabs/Systems/GameObjectPoolService.prefab";

    [MenuItem("Tools/MyRPG/Setup Gameplay Scene Systems (P1)")]
    public static void SetupFromMenu()
    {
        SetupFromMenuSilent();
        EditorUtility.DisplayDialog(
            "Gameplay Scene Systems (P1)",
            "SampleScene updated:\n- GameObjectPoolService\n- Verified GameSpawnPoint + BGM zones",
            "OK");
    }

    internal static void SetupFromMenuSilent()
    {
        SetupSystemPrefabs.SetupAllFromMenuSilent();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsurePoolServiceInScene();
        EnsureWorldObjects();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    internal static void EnsurePoolServiceInScene()
    {
        if (Object.FindObjectOfType<GameObjectPoolService>() != null)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PoolServicePrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"SetupGameplaySceneSystems: missing prefab at {PoolServicePrefabPath}. Run Setup System Prefabs first.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "GameObjectPoolService";
    }

    private static void EnsureWorldObjects()
    {
        SetupGameplaySpawnAndBgm.SetupFromMenuSilent();
    }
}
