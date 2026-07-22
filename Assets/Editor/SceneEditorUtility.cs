using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 快捷打开常用场景。
/// </summary>
public static class SceneEditorUtility
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string LoginScenePath = "Assets/Scenes/LoginScene.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";

    [MenuItem("Tools/MyRPG/Open Sample Scene")]
    public static void OpenSampleScene()
    {
        OpenScene(SampleScenePath);
    }

    [MenuItem("Tools/MyRPG/Open Login Scene")]
    public static void OpenLoginScene()
    {
        OpenScene(LoginScenePath);
    }

    [MenuItem("Tools/MyRPG/Open Main Menu Scene")]
    public static void OpenMainMenuScene()
    {
        OpenScene(MainMenuScenePath);
    }

    private static void OpenScene(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }
}
