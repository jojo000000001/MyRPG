using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 在 LoginScene 中搭建登录 UI，并绑定 LoginUI 引用。
/// </summary>
public static class LoginUISceneBuilder
{
    private const string EditorScenePath = "Assets/Scenes/LoginScene.unity";
    private const string BuildScenePath = "Assets/Scenes/LoginScene.scene";

    private static readonly Color PrimaryTextColor = new Color(0.18f, 0.1f, 0.045f, 1f);
    private static readonly Color MutedTextColor = new Color(0.35f, 0.24f, 0.14f, 0.75f);
    private static readonly Color ButtonTextColor = new Color(0.98f, 0.93f, 0.82f, 1f);
    private static readonly Color ErrorTextColor = new Color(0.72f, 0.18f, 0.12f, 1f);

    private const string PanelSpritePath = "Assets/Art/UI/GeneratedInventory/ui_inventory_panel_brown.png";
    private const string FieldSpritePath = KenneyUIAssets.PanelInsetBeigeLight;
    private const string ButtonSpritePath = KenneyUIAssets.ButtonLongBrown;
    private const string ButtonPressedSpritePath = KenneyUIAssets.ButtonLongBrownPressed;

    [MenuItem("Tools/MyRPG/Rebuild Login Scene UI")]
    public static void RebuildFromMenu()
    {
        Rebuild();
        EditorUtility.DisplayDialog("Login Scene", "LoginScene UI rebuilt and saved.", "OK");
    }

    public static void Rebuild()
    {
        EnsureEditorSceneExists();

        var scene = EditorSceneManager.OpenScene(EditorScenePath, OpenSceneMode.Single);
        ClearExistingLoginUi();

        Sprite panelSprite = LoadSprite(PanelSpritePath);
        Sprite fieldSprite = LoadSprite(FieldSpritePath);
        Sprite buttonSprite = LoadSprite(ButtonSpritePath);
        Sprite buttonPressedSprite = LoadSprite(ButtonPressedSpritePath);

        EnsureEventSystem();

        GameObject canvasRoot = new GameObject("LoginCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LoginCanvasLayoutDriver), typeof(LoginUI));
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        ConfigureCanvasRoot(canvasRect);

        GameObject uiRoot = CreateChild(canvasRoot, "UIRoot", typeof(RectTransform));
        Stretch(uiRoot.GetComponent<RectTransform>());

        GameObject backdrop = CreateChild(uiRoot, "Backdrop", typeof(RectTransform), typeof(Image));
        Stretch(backdrop.GetComponent<RectTransform>());
        Image backdropImage = backdrop.GetComponent<Image>();
        Sprite loginBackground = LoadSprite(GeneratedLoginAssets.LoginBackground);
        if (loginBackground != null)
        {
            backdropImage.sprite = loginBackground;
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

        GameObject card = CreateChild(uiRoot, "LoginCard", typeof(RectTransform), typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(480f, 400f);
        ConfigureImage(card.GetComponent<Image>(), panelSprite, Image.Type.Sliced, Color.white);

        Text title = CreateText(card, "Title", "MyRPG", 36, TextAnchor.MiddleCenter, FontStyle.Bold, PrimaryTextColor);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -32f);
        titleRect.sizeDelta = new Vector2(-48f, 52f);

        Text usernameLabel = CreateText(card, "UsernameLabel", "账号", 22, TextAnchor.MiddleLeft, FontStyle.Normal, PrimaryTextColor);
        PlaceLabel(usernameLabel.rectTransform, 0.76f);

        InputField usernameField = CreateInputField(card.transform, "UsernameField", "请输入账号", fieldSprite);
        PlaceField(usernameField.GetComponent<RectTransform>(), 0.66f);

        Text passwordLabel = CreateText(card, "PasswordLabel", "密码", 22, TextAnchor.MiddleLeft, FontStyle.Normal, PrimaryTextColor);
        PlaceLabel(passwordLabel.rectTransform, 0.56f);

        InputField passwordField = CreateInputField(card.transform, "PasswordField", "请输入密码", fieldSprite, true);
        PlaceField(passwordField.GetComponent<RectTransform>(), 0.46f);

        Text statusText = CreateText(card, "StatusText", string.Empty, 20, TextAnchor.MiddleCenter, FontStyle.Normal, ErrorTextColor);
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0.28f);
        statusRect.anchorMax = new Vector2(1f, 0.28f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = Vector2.zero;
        statusRect.sizeDelta = new Vector2(-48f, 36f);
        statusText.color = ErrorTextColor;

        Button loginButton = CreateButton(card.transform, "LoginButton", "进入游戏", buttonSprite, buttonPressedSprite, ButtonTextColor);
        RectTransform buttonRect = loginButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 36f);
        buttonRect.sizeDelta = new Vector2(260f, 52f);

        LoginUI loginUI = canvasRoot.GetComponent<LoginUI>();
        SerializedObject serializedLogin = new SerializedObject(loginUI);
        serializedLogin.FindProperty("gameSceneName").stringValue = "SampleScene";
        serializedLogin.FindProperty("minUsernameLength").intValue = 1;
        serializedLogin.FindProperty("minPasswordLength").intValue = 1;
        serializedLogin.FindProperty("usernameField").objectReferenceValue = usernameField;
        serializedLogin.FindProperty("passwordField").objectReferenceValue = passwordField;
        serializedLogin.FindProperty("statusText").objectReferenceValue = statusText;
        serializedLogin.FindProperty("loginButton").objectReferenceValue = loginButton;
        serializedLogin.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, EditorScenePath);
        SyncBuildSceneFile();
        AssetDatabase.SaveAssets();
    }

    private static void EnsureEditorSceneExists()
    {
        if (File.Exists(EditorScenePath))
            return;

        if (File.Exists(BuildScenePath))
        {
            File.Copy(BuildScenePath, EditorScenePath);
            AssetDatabase.ImportAsset(EditorScenePath);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, EditorScenePath);
    }

    private static void SyncBuildSceneFile()
    {
        if (!File.Exists(EditorScenePath))
            return;

        File.Copy(EditorScenePath, BuildScenePath, true);
        AssetDatabase.ImportAsset(BuildScenePath);
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

    private static void ClearExistingLoginUi()
    {
        GameObject bootstrap = GameObject.Find("LoginBootstrap");
        if (bootstrap != null)
            Object.DestroyImmediate(bootstrap);

        GameObject canvas = GameObject.Find("LoginCanvas");
        if (canvas != null)
            Object.DestroyImmediate(canvas);

        GameObject strayArrow = GameObject.Find("arrowBeige_left");
        if (strayArrow != null)
            Object.DestroyImmediate(strayArrow);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void PlaceLabel(RectTransform rect, float anchorY)
    {
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(36f, 0f);
        rect.sizeDelta = new Vector2(-72f, 28f);
    }

    private static void PlaceField(RectTransform rect, float anchorY)
    {
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-72f, 44f);
    }

    private static InputField CreateInputField(Transform parent, string name, string placeholder, Sprite backgroundSprite, bool isPassword = false)
    {
        GameObject fieldObject = CreateChild(parent.gameObject, name, typeof(RectTransform), typeof(Image), typeof(InputField));
        ConfigureImage(fieldObject.GetComponent<Image>(), backgroundSprite, Image.Type.Sliced, Color.white);

        GameObject placeholderObject = CreateChild(fieldObject, "Placeholder", typeof(RectTransform), typeof(Text));
        Text placeholderText = placeholderObject.GetComponent<Text>();
        placeholderText.text = placeholder;
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.color = MutedTextColor;
        placeholderText.raycastTarget = false;
        ChineseUIFont.Apply(placeholderText, 20);
        Stretch(placeholderObject.GetComponent<RectTransform>(), 12f, 8f);

        GameObject textObject = CreateChild(fieldObject, "Text", typeof(RectTransform), typeof(Text));
        Text inputText = textObject.GetComponent<Text>();
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.color = PrimaryTextColor;
        inputText.supportRichText = false;
        inputText.raycastTarget = false;
        ChineseUIFont.Apply(inputText, 20);
        Stretch(textObject.GetComponent<RectTransform>(), 12f, 8f);

        InputField inputField = fieldObject.GetComponent<InputField>();
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.lineType = InputField.LineType.SingleLine;
        if (isPassword)
            inputField.contentType = InputField.ContentType.Password;

        return inputField;
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
