using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupHealthBarCatalog
{
    private const string KenneyRoot = "Assets/Art/UI/kenney_ui-pack-rpg-expansion/PNG/";
    private const string ResourcesRoot = "Assets/Resources/UI/HealthBar/";
    private const string CatalogPath = "Assets/Resources/HealthBarSpriteCatalog.asset";

    private static readonly string[] BarFiles =
    {
        "barBack_horizontalLeft",
        "barBack_horizontalMid",
        "barBack_horizontalRight",
        "barGreen_horizontalLeft",
        "barGreen_horizontalMid",
        "barGreen_horizontalRight",
        "barRed_horizontalLeft",
        "barRed_horizontalMid",
        "barRed_horizontalRight",
        "barBlue_horizontalLeft",
        "barBlue_horizontalBlue",
        "barBlue_horizontalRight",
    };

    [InitializeOnLoadMethod]
    private static void AutoFixCatalogOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            HealthBarSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<HealthBarSpriteCatalog>(CatalogPath);
            if (catalog == null || !CatalogUsesResourcesSprites(catalog))
                BuildOrLoadCatalog();
        };
    }

    private static bool CatalogUsesResourcesSprites(HealthBarSpriteCatalog catalog)
    {
        if (catalog == null || !catalog.HasBackSprites)
            return false;

        string backMidPath = AssetDatabase.GetAssetPath(catalog.backMid);
        return !string.IsNullOrEmpty(backMidPath) &&
            backMidPath.StartsWith(ResourcesRoot, System.StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem("Tools/MyRPG/Rebuild Player HUD")]
    public static void RebuildPlayerHudInScene()
    {
        PlayerHealthBar healthBar = Object.FindObjectOfType<PlayerHealthBar>(true);
        if (healthBar == null)
        {
            EditorUtility.DisplayDialog("Rebuild Player HUD", "场景里找不到 PlayerHUD / PlayerHealthBar。", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(healthBar.gameObject, "Rebuild Player HUD");

        Transform hud = healthBar.transform;
        for (int i = hud.childCount - 1; i >= 0; i--)
        {
            Transform child = hud.GetChild(i);
            if (child.name.StartsWith("PlayerHealthBarRoot") || child.name.StartsWith("PlayerExperienceBarRoot"))
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        SerializedObject healthBarObject = new SerializedObject(healthBar);
        healthBarObject.FindProperty("anchoredPosition").vector2Value = HudBarVisualStyle.HealthAnchoredPosition;
        healthBarObject.FindProperty("barSize").vector2Value = HudBarVisualStyle.BarSize;
        healthBarObject.FindProperty("capWidth").floatValue = HudBarVisualStyle.CapWidth;
        healthBarObject.FindProperty("fillInset").floatValue = HudBarVisualStyle.FillInset;
        healthBarObject.FindProperty("useTrackPlate").boolValue = false;
        healthBarObject.FindProperty("showBackTrack").boolValue = false;
        healthBarObject.FindProperty("hudLayoutVersion").intValue = 2;
        healthBarObject.ApplyModifiedPropertiesWithoutUndo();

        RectTransform hudRect = hud as RectTransform;
        if (hudRect != null && hudRect.localScale.sqrMagnitude < 0.0001f)
            hudRect.localScale = Vector3.one;

        PlayerExperienceBar experienceBar = healthBar.GetComponent<PlayerExperienceBar>();
        if (experienceBar == null)
            experienceBar = healthBar.gameObject.AddComponent<PlayerExperienceBar>();

        SerializedObject experienceBarObject = new SerializedObject(experienceBar);
        experienceBarObject.FindProperty("barSize").vector2Value = HudBarVisualStyle.BarSize;
        experienceBarObject.FindProperty("capWidth").floatValue = HudBarVisualStyle.CapWidth;
        experienceBarObject.FindProperty("fillInset").floatValue = HudBarVisualStyle.FillInset;
        experienceBarObject.FindProperty("useTrackPlate").boolValue = false;
        experienceBarObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(healthBar);
        EditorSceneManager.MarkSceneDirty(healthBar.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog(
            "Rebuild Player HUD",
            "已清理旧的手动血条/经验条节点，并写入新的 HUD 布局参数。\n请进入 Play 模式查看最终效果。",
            "OK");
    }

    [MenuItem("Tools/MyRPG/Setup Health Bar Catalog")]
    public static void SetupFromMenu()
    {
        int configured = KenneyUISpriteSetup.SetupAll();
        SyncResourcesCopies();
        HealthBarSpriteCatalog catalog = BuildOrLoadCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SetupHealthBarCatalog] Kenney textures configured: {configured}. Catalog: {CatalogPath}");
    }

    public static HealthBarSpriteCatalog BuildOrLoadCatalog()
    {
        HealthBarSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<HealthBarSpriteCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<HealthBarSpriteCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.backLeft = LoadRuntimeSprite("barBack_horizontalLeft");
        catalog.backMid = LoadRuntimeSprite("barBack_horizontalMid");
        catalog.backRight = LoadRuntimeSprite("barBack_horizontalRight");

        catalog.greenLeft = LoadRuntimeSprite("barGreen_horizontalLeft");
        catalog.greenMid = LoadRuntimeSprite("barGreen_horizontalMid");
        catalog.greenRight = LoadRuntimeSprite("barGreen_horizontalRight");

        catalog.redLeft = LoadRuntimeSprite("barRed_horizontalLeft");
        catalog.redMid = LoadRuntimeSprite("barRed_horizontalMid");
        catalog.redRight = LoadRuntimeSprite("barRed_horizontalRight");

        catalog.blueLeft = LoadRuntimeSprite("barBlue_horizontalLeft");
        catalog.blueMid = LoadRuntimeSprite("barBlue_horizontalBlue");
        catalog.blueRight = LoadRuntimeSprite("barBlue_horizontalRight");

        EditorUtility.SetDirty(catalog);
        AssignCatalogToSceneHud(catalog);
        return catalog;
    }

    private static void SyncResourcesCopies()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI/HealthBar"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");
            AssetDatabase.CreateFolder("Assets/Resources/UI", "HealthBar");
        }

        foreach (string fileName in BarFiles)
        {
            string sourcePath = $"{KenneyRoot}{fileName}.png";
            string destPath = $"{ResourcesRoot}{fileName}.png";
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[SetupHealthBarCatalog] Missing Kenney sprite: {sourcePath}");
                continue;
            }

            File.Copy(sourcePath, destPath, true);
            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
            KenneyUISpriteSetup.ConfigureAtPath(destPath);
        }
    }

    private static void AssignCatalogToSceneHud(HealthBarSpriteCatalog catalog)
    {
        if (catalog == null)
            return;

        PlayerHealthBar healthBar = Object.FindObjectOfType<PlayerHealthBar>(true);
        if (healthBar == null)
            return;

        SerializedObject healthBarObject = new SerializedObject(healthBar);
        healthBarObject.FindProperty("spriteCatalog").objectReferenceValue = catalog;
        healthBarObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerExperienceBar experienceBar = healthBar.GetComponent<PlayerExperienceBar>();
        if (experienceBar != null)
        {
            SerializedObject experienceBarObject = new SerializedObject(experienceBar);
            experienceBarObject.FindProperty("spriteCatalog").objectReferenceValue = catalog;
            experienceBarObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(healthBar);
    }

    private static Sprite LoadRuntimeSprite(string fileName)
    {
        Sprite resourcesSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourcesRoot}{fileName}.png");
        if (resourcesSprite != null)
            return resourcesSprite;

        return AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyRoot}{fileName}.png");
    }
}
