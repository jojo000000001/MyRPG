using System.IO;
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

    private static readonly Color PrimaryTextColor = new Color(0.18f, 0.1f, 0.045f, 1f);
    private static readonly Color ButtonTextColor = new Color(0.98f, 0.93f, 0.82f, 1f);

    private const string PanelSpritePath = "Assets/Art/UI/GeneratedInventory/ui_inventory_panel_brown.png";
    private const string ButtonSpritePath = KenneyUIAssets.ButtonLongBrown;
    private const string ButtonPressedSpritePath = KenneyUIAssets.ButtonLongBrownPressed;

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
        Sprite buttonSprite = LoadSprite(ButtonSpritePath);
        Sprite buttonPressedSprite = LoadSprite(ButtonPressedSpritePath);

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
            backdropImage.color = new Color(0.08f, 0.07f, 0.06f, 1f);
        }

        backdropImage.raycastTarget = true;

        GameObject dimOverlay = CreateChild(uiRoot, "DimOverlay", typeof(RectTransform), typeof(Image));
        Stretch(dimOverlay.GetComponent<RectTransform>());
        Image dimImage = dimOverlay.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.32f);
        dimImage.raycastTarget = false;

        GameObject card = CreateChild(uiRoot, "MenuCard", typeof(RectTransform), typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(480f, 580f);
        ConfigureImage(card.GetComponent<Image>(), panelSprite, Image.Type.Sliced, Color.white);

        Text title = CreateText(card, "Title", "MyRPG", 36, TextAnchor.MiddleCenter, FontStyle.Bold, PrimaryTextColor);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -32f);
        titleRect.sizeDelta = new Vector2(-48f, 52f);

        Button continueGameButton = CreateButton(card.transform, "ContinueGameButton", "继续游戏", buttonSprite, buttonPressedSprite, ButtonTextColor);
        PlaceMenuButton(continueGameButton.GetComponent<RectTransform>(), 0.76f);

        Button startGameButton = CreateButton(card.transform, "StartGameButton", "开始游戏", buttonSprite, buttonPressedSprite, ButtonTextColor);
        PlaceMenuButton(startGameButton.GetComponent<RectTransform>(), 0.62f);

        Button loadSaveButton = CreateButton(card.transform, "LoadSaveButton", "读取存档", buttonSprite, buttonPressedSprite, ButtonTextColor);
        PlaceMenuButton(loadSaveButton.GetComponent<RectTransform>(), 0.48f);

        Button settingsButton = CreateButton(card.transform, "SettingsButton", "设置", buttonSprite, buttonPressedSprite, ButtonTextColor);
        PlaceMenuButton(settingsButton.GetComponent<RectTransform>(), 0.34f);

        Button quitButton = CreateButton(card.transform, "QuitButton", "退出游戏", buttonSprite, buttonPressedSprite, ButtonTextColor);
        PlaceMenuButton(quitButton.GetComponent<RectTransform>(), 0.20f);

        MainMenuUI menuUI = canvasRoot.GetComponent<MainMenuUI>();
        SerializedObject serializedMenu = new SerializedObject(menuUI);
        serializedMenu.FindProperty("continueGameButton").objectReferenceValue = continueGameButton;
        serializedMenu.FindProperty("startGameButton").objectReferenceValue = startGameButton;
        serializedMenu.FindProperty("loadSaveButton").objectReferenceValue = loadSaveButton;
        serializedMenu.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        serializedMenu.FindProperty("quitButton").objectReferenceValue = quitButton;
        serializedMenu.FindProperty("gameSceneName").stringValue = "SampleScene";
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
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
        rect.sizeDelta = new Vector2(300f, 52f);
    }

    private static Button CreateButton(Transform parent, string name, string label, Sprite normalSprite, Sprite pressedSprite, Color labelColor)
    {
        GameObject buttonObject = CreateChild(parent.gameObject, name, typeof(RectTransform), typeof(Image), typeof(Button));
        Image image = buttonObject.GetComponent<Image>();
        ConfigureImage(image, normalSprite, Image.Type.Sliced, Color.white);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        SpriteState states = button.spriteState;
        states.pressedSprite = pressedSprite;
        button.spriteState = states;

        Text text = CreateText(buttonObject, "Label", label, 24, TextAnchor.MiddleCenter, FontStyle.Bold, labelColor);
        Stretch(text.rectTransform);

        return button;
    }

    private static Text CreateText(GameObject parent, string name, string text, int fontSize, TextAnchor alignment, FontStyle style, Color color)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(Text));
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        ChineseUIFont.Apply(label, fontSize, style);
        return label;
    }

    private static GameObject CreateChild(GameObject parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static void ConfigureImage(Image image, Sprite sprite, Image.Type type, Color color)
    {
        image.sprite = sprite;
        image.type = sprite != null ? type : Image.Type.Simple;
        image.color = color;
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
