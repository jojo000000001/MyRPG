using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 团结/Tuanjie 构建使用 .scene，编辑器 API 仅识别 .unity。本工具在两者之间同步并快捷打开场景。
/// </summary>
public static class SceneEditorUtility
{
    private const string SampleBuildPath = "Assets/Scenes/SampleScene.scene";
    private const string SampleEditorPath = "Assets/Scenes/SampleScene.unity";
    private const string LoginBuildPath = "Assets/Scenes/LoginScene.scene";
    private const string LoginEditorPath = "Assets/Scenes/LoginScene.unity";

    [MenuItem("Tools/MyRPG/Open Sample Scene")]
    public static void OpenSampleScene()
    {
        OpenScene(SampleBuildPath, SampleEditorPath);
    }

    [MenuItem("Tools/MyRPG/Open Login Scene")]
    public static void OpenLoginScene()
    {
        OpenScene(LoginBuildPath, LoginEditorPath);
    }

    [MenuItem("Tools/MyRPG/Sync Sample Scene (.scene → .unity)")]
    public static void SyncSampleSceneToEditor()
    {
        SyncBuildToEditor(SampleBuildPath, SampleEditorPath);
        EditorUtility.DisplayDialog("Scene Sync", "SampleScene.scene 已同步到 SampleScene.unity。", "OK");
    }

    [MenuItem("Tools/MyRPG/Save Sample Scene to Build (.unity → .scene)")]
    public static void SaveSampleSceneToBuild()
    {
        if (!File.Exists(SampleEditorPath))
        {
            EditorUtility.DisplayDialog("Scene Sync", "SampleScene.unity 不存在，请先用 Open Sample Scene 打开。", "OK");
            return;
        }

        SyncEditorToBuild(SampleEditorPath, SampleBuildPath);
        EditorUtility.DisplayDialog("Scene Sync", "SampleScene.unity 已写回 SampleScene.scene（用于打包/运行时加载）。", "OK");
    }

    private static void OpenScene(string buildPath, string editorPath)
    {
        SyncBuildToEditor(buildPath, editorPath);
        EditorSceneManager.OpenScene(editorPath, OpenSceneMode.Single);
    }

    private static void SyncBuildToEditor(string buildPath, string editorPath)
    {
        if (!File.Exists(buildPath))
        {
            EditorUtility.DisplayDialog("Scene Sync", $"找不到构建场景：{buildPath}", "OK");
            return;
        }

        File.Copy(buildPath, editorPath, true);
        AssetDatabase.ImportAsset(editorPath);
    }

    private static void SyncEditorToBuild(string editorPath, string buildPath)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isDirty)
            EditorSceneManager.SaveScene(scene, editorPath);

        File.Copy(editorPath, buildPath, true);
        AssetDatabase.ImportAsset(buildPath);
    }
}
