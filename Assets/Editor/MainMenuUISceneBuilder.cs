using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 在 MainMenuScene 中搭建开始界面 UI，并绑定 MainMenuUI 引用。
/// </summary>
public static class MainMenuUISceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenuScene.unity";
    private const string PanelSpritePath = "Assets/Art/UI/GeneratedShop/ui_shop_panel_sheikah.png";
    private const string SlotSpritePath = "Assets/Art/UI/GeneratedShop/ui_shop_slot_sheikah.png";

    [MenuItem("Tools/MyRPG/Rebuild Main Menu Scene UI")]
    public static void RebuildFromMenu()
    {
        Rebuild();
        EditorUtility.DisplayDialog("Main Menu Scene", "MainMenuScene UI rebuilt and saved.", "OK");
    }

    public static void Rebuild()
    {
        EnsureEditorSceneExists();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ClearExistingMenuUi();

        Sprite panelSprite = LoadSprite(PanelSpritePath);
        Sprite slotSprite = LoadSprite(SlotSpritePath);

        EnsureEventSystem();

        GameObject canvasRoot = new GameObject(
            "MainMenuCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(LoginCanvasLayoutDriver),
            typeof(MainMenuUI));

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        ConfigureCanvasRoot(canvasRoot.GetComponent<RectTransform>());

        GameObject uiRoot = CreateChild(canvasRoot, "UIRoot", typeof(RectTransform));
        Stretch(uiRoot.GetComponent<RectTransform>());

        GameObject backdrop = CreateChild(uiRoot, "Backdrop", typeof(RectTransform), typeof(Image));
        Stretch(backdrop.GetComponent<RectTransform>());
        Image backdropImage = backdrop.GetComponent<Image>();
        Sprite menuBackground = LoadSprite(GeneratedLoginAssets.LoginBackground);
        if (menuBackground != null)
        {
            backdropImage.sprite = menuBackground;
            backdropImage.type = Image.Type.Simple;
            backdropImage.preserveAspect = false;
            backdropImage.color = Color.white;
        }
        else
        {
            backdropImage.color = new Color(0.05f, 0.07f, 0.09f, 1f);
        }

        backdropImage.raycastTarget = true;

        GameObject dimOverlay = CreateChild(uiRoot, "DimOverlay", typeof(RectTransform), typeof(Image));
        Stretch(dimOverlay.GetComponent<RectTransform>());
        Image dimImage = dimOverlay.GetComponent<Image>();
        dimImage.color = new Color(0.02f, 0.05f, 0.08f, 0.38f);
        dimImage.raycastTarget = false;

        GameObject card = SheikahUiStyle.CreateCard(uiRoot.transform, "MenuCard", new Vector2(520f, 560f), panelSprite, 2.4f);

        TextMeshProUGUI title = SheikahUiStyle.CreateText(
            card.transform,
            "Title",
            "MyRPG",
            40f,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            SheikahUiStyle.Orange);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -48f);
        titleRect.sizeDelta = new Vector2(-96f, 52f);

        Button continueGameButton = CreateMenuButton(card.transform, "ContinueGameButton", "继续游戏", slotSprite, SheikahUiStyle.Orange, SheikahUiStyle.Text);
        PlaceMenuButton(continueGameButton.GetComponent<RectTransform>(), 0.68f);

        Button startGameButton = CreateMenuButton(card.transform, "StartGameButton", "开始游戏", slotSprite, SheikahUiStyle.Orange, SheikahUiStyle.Text);
        PlaceMenuButton(startGameButton.GetComponent<RectTransform>(), 0.52f);

        Button settingsButton = CreateMenuButton(card.transform, "SettingsButton", "设置", slotSprite, SheikahUiStyle.Orange, SheikahUiStyle.Text);
        PlaceMenuButton(settingsButton.GetComponent<RectTransform>(), 0.36f);

        Button quitButton = CreateMenuButton(card.transform, "QuitButton", "退出游戏", slotSprite, SheikahUiStyle.Orange, SheikahUiStyle.Text);
        PlaceMenuButton(quitButton.GetComponent<RectTransform>(), 0.20f);

        MainMenuUI menuUI = canvasRoot.GetComponent<MainMenuUI>();
        SerializedObject serializedMenu = new SerializedObject(menuUI);
        serializedMenu.FindProperty("continueGameButton").objectReferenceValue = continueGameButton;
        serializedMenu.FindProperty("startGameButton").objectReferenceValue = startGameButton;
        serializedMenu.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        serializedMenu.FindProperty("quitButton").objectReferenceValue = quitButton;
        serializedMenu.FindProperty("panelSprite").objectReferenceValue = panelSprite;
        serializedMenu.FindProperty("slotSprite").objectReferenceValue = slotSprite;
        serializedMenu.FindProperty("gameSceneName").stringValue = "SampleScene";
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, Sprite slotSprite, Color tint, Color labelColor)
    {
        return SheikahUiStyle.CreateButton(parent, name, label, new Vector2(340f, 56f), tint, labelColor, slotSprite, 5.5f);
    }

    private static void EnsureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/LoginScene.unity", true),
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
        };
    }

    private static void EnsureEditorSceneExists()
    {
        if (File.Exists(ScenePath))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void ConfigureCanvasRoot(RectTransform canvasRect)
    {
        canvasRect.localScale = Vector3.one;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.sizeDelta = Vector2.zero;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
    }

    private static void ClearExistingMenuUi()
    {
        GameObject canvas = GameObject.Find("MainMenuCanvas");
        if (canvas != null)
            Object.DestroyImmediate(canvas);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void PlaceMenuButton(RectTransform rect, float anchorY)
    {
        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(340f, 56f);
    }

    private static GameObject CreateChild(GameObject parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Stretch(RectTransform rect, float horizontalPadding = 0f, float verticalPadding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        rect.localScale = Vector3.one;
    }
}
