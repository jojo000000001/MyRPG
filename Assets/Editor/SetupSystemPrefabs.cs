using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 创建全局系统 Prefab（P0）并同步到 Resources。
/// </summary>
public static class SetupSystemPrefabs
{
    private const string SystemsPrefabDir = "Assets/Prefabs/Systems";
    private const string SystemsResourceDir = "Assets/Resources/Systems";

    [MenuItem("Tools/MyRPG/Setup System Prefabs (P0)")]
    public static void SetupAllFromMenu()
    {
        SetupAllFromMenuSilent();
        EditorUtility.DisplayDialog(
            "System Prefabs (P0)",
            "Created/updated:\n" +
            "- DamageNumberSpawner\n" +
            "- HitImpactManager\n" +
            "- GameObjectPoolService (scene prefab only)\n" +
            "- BgmManager",
            "OK");
    }

    internal static void SetupAllFromMenuSilent()
    {
        EnsureFolder(SystemsPrefabDir);
        EnsureFolder(SystemsResourceDir);

        SetupDamageNumberSpawnerPrefab();
        SetupHitImpactManagerPrefab();
        SetupGameObjectPoolServicePrefab();
        SetupBgmManagerPrefab.SetupFromMenuSilent();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void SetupDamageNumberSpawnerPrefab()
    {
        GameObject root = new GameObject("DamageNumberSpawner");
        try
        {
            DamageNumberSpawner spawner = root.AddComponent<DamageNumberSpawner>();
            Transform poolRoot = CreateChild(root.transform, "DamageNumberPool");

            SerializedObject serialized = new SerializedObject(spawner);
            serialized.FindProperty("popupRoot").objectReferenceValue = poolRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SaveRuntimePrefab(root, "DamageNumberSpawner.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetupHitImpactManagerPrefab()
    {
        GameObject root = new GameObject("HitImpactManager");
        try
        {
            root.AddComponent<HitImpactManager>();
            SaveRuntimePrefab(root, "HitImpactManager.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetupGameObjectPoolServicePrefab()
    {
        GameObject root = new GameObject("GameObjectPoolService");
        try
        {
            GameObjectPoolService service = root.AddComponent<GameObjectPoolService>();
            Transform poolRoot = CreateChild(root.transform, "PoolRoot");

            SerializedObject serialized = new SerializedObject(service);
            serialized.FindProperty("poolRoot").objectReferenceValue = poolRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = $"{SystemsPrefabDir}/GameObjectPoolService.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SaveRuntimePrefab(GameObject root, string fileName)
    {
        string editPath = $"{SystemsPrefabDir}/{fileName}";
        string runtimePath = $"{SystemsResourceDir}/{fileName}";
        PrefabUtility.SaveAsPrefabAsset(root, editPath);
        PrefabUtility.SaveAsPrefabAsset(root, runtimePath);
    }

    private static Transform CreateChild(Transform parent, string childName)
    {
        var childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
