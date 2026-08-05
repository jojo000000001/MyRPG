using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 创建 BgmManager Prefab（含两个 AudioSource 子节点）并同步到 Resources。
/// </summary>
public static class SetupBgmManagerPrefab
{
    private const string PrefabPath = "Assets/Prefabs/Systems/BgmManager.prefab";
    private const string RuntimePrefabPath = "Assets/Resources/Systems/BgmManager.prefab";
    private const string ClipSetAssetPath = "Assets/Resources/BgmClipSet.asset";

    [MenuItem("Tools/MyRPG/Setup BGM Manager Prefab")]
    public static void SetupFromMenu()
    {
        SetupFromMenuSilent();
        EditorUtility.DisplayDialog(
            "BGM Manager Prefab",
            "Created/updated:\n- Assets/Prefabs/Systems/BgmManager.prefab\n- Assets/Resources/Systems/BgmManager.prefab",
            "OK");
    }

    internal static void SetupFromMenuSilent()
    {
        EnsureFolder("Assets/Prefabs/Systems");
        EnsureFolder("Assets/Resources/Systems");

        GameObject root = new GameObject("BgmManager");
        try
        {
            BgmManager manager = root.AddComponent<BgmManager>();
            AudioSource activeSource = CreateBgmSource(root.transform, "BgmActive");
            AudioSource fadeSource = CreateBgmSource(root.transform, "BgmFade");

            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("activeSource").objectReferenceValue = activeSource;
            serializedManager.FindProperty("fadeSource").objectReferenceValue = fadeSource;

            BgmClipSet clipSet = AssetDatabase.LoadAssetAtPath<BgmClipSet>(ClipSetAssetPath);
            if (clipSet != null)
                serializedManager.FindProperty("clipSet").objectReferenceValue = clipSet;

            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabPath);
            SavePrefab(root, RuntimePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static AudioSource CreateBgmSource(Transform parent, string sourceName)
    {
        var sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(parent, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 0;
        source.volume = 1f;
        return source;
    }

    private static void SavePrefab(GameObject root, string assetPath)
    {
        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
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
