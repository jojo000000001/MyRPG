using UnityEditor;
using UnityEngine;

/// <summary>
/// 为 Player.prefab 补齐 P1 运行时组件。
/// </summary>
public static class SetupPlayerGameplayComponents
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/MyRPG/Setup P0 And P1")]
    public static void SetupP0AndP1FromMenu()
    {
        SetupSystemPrefabs.SetupAllFromMenuSilent();
        SetupFromMenuSilent();
        SetupGameplaySceneSystems.SetupFromMenuSilent();

        EditorUtility.DisplayDialog(
            "P0 + P1 Setup",
            "Completed:\n- System prefabs\n- Player gameplay components\n- SampleScene systems",
            "OK");
    }

    internal static void SetupFromMenuSilent()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (root.GetComponent<HitFeedback>() == null)
                root.AddComponent<HitFeedback>();

            if (root.GetComponent<SaveGameController>() == null)
                root.AddComponent<SaveGameController>();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        SetupPlayerWeaponVisualPrefab.Setup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/MyRPG/Setup Player Gameplay Components (P1)")]
    public static void SetupFromMenu()
    {
        SetupFromMenuSilent();
        EditorUtility.DisplayDialog(
            "Player Gameplay Components (P1)",
            "Player.prefab updated:\n- HitFeedback\n- SaveGameController\n- WeaponVisual pivot",
            "OK");
    }
}
