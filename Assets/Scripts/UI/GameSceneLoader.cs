using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 按 Build Settings 加载场景。
/// </summary>
public static class GameSceneLoader
{
    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("GameSceneLoader: Scene name is empty.");
            return;
        }

        int buildIndex = SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{sceneName}.unity");
        if (buildIndex >= 0)
        {
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
            return;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
