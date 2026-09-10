using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 把 Demo 胜负界面烘焙成预制体，并挂到 SampleScene。
/// </summary>
public static class DemoFlowUIPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/DemoFlowUI.prefab";
    private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";
    private const int OverlaySortingOrder = 500;
    private const float DefeatCardHeight = 440f;

    [MenuItem("Tools/MyRPG/Rebuild Demo Flow UI Prefab")]
    public static void RebuildPrefabAndScene()
    {
        GameObject prefab = BuildPrefabAsset();
        InstallPrefabInOpenScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("DemoFlowUIPrefabBuilder: prefab saved to " + PrefabPath);
    }

    public static GameObject BuildPrefabAsset()
    {
        EnsureFolder("Assets/Prefabs/UI");

        GameObject root = new GameObject(
            "DemoFlowUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(DemoFlowUI));

        RectTransform rootRect = root.GetComponent<RectTransform>();
        SheikahUiStyle.Stretch(rootRect);
        rootRect.localScale = Vector3.one;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.Normal
            | AdditionalCanvasShaderChannels.Tangent;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject flowRoot = SheikahUiStyle.CreateChild(root.transform, "DemoFlowRoot");
        RectTransform flowRect = flowRoot.GetComponent<RectTransform>();
        SheikahUiStyle.Stretch(flowRect);

        GameObject endScreen = SheikahUiStyle.CreateChild(flowRoot.transform, "EndScreen");
        SheikahUiStyle.Stretch(endScreen.GetComponent<RectTransform>());

        Image backdrop = SheikahUiStyle.CreateChild(endScreen.transform, "Dim", typeof(Image)).GetComponent<Image>();
        SheikahUiStyle.Stretch(backdrop.rectTransform);
        backdrop.color = SheikahUiStyle.Dim;
        backdrop.raycastTarget = true;

        GameObject card = SheikahUiStyle.CreateCard(
            endScreen.transform,
            "Card",
            new Vector2(520f, DefeatCardHeight),
            SheikahUiStyle.PanelSprite,
            2.4f);
        RectTransform cardRect = card.GetComponent<RectTransform>();

        TextMeshProUGUI title = SheikahUiStyle.CreateText(
            card.transform,
            "Title",
            "你阵亡了",
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

        TextMeshProUGUI body = SheikahUiStyle.CreateText(
            card.transform,
            "Body",
            "哥布林占领了这片空地……",
            22f,
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            SheikahUiStyle.Text);
        RectTransform bodyRect = body.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0.58f);
        bodyRect.anchorMax = new Vector2(1f, 0.58f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.sizeDelta = new Vector2(-80f, 80f);

        Sprite slotSprite = SheikahUiStyle.SlotSprite;
        Button restart = CreateMenuButton(card.transform, "RestartButton", "再试一次", 0.34f, slotSprite);
        Button loadSave = CreateMenuButton(card.transform, "LoadSaveButton", "读取存档", 0.32f, slotSprite);
        loadSave.gameObject.SetActive(false);
        Button mainMenu = CreateMenuButton(card.transform, "MainMenuButton", "返回主菜单", 0.14f, slotSprite);

        SerializedObject flow = new SerializedObject(root.GetComponent<DemoFlowUI>());
        flow.FindProperty("rootRect").objectReferenceValue = flowRect;
        flow.FindProperty("cardRect").objectReferenceValue = cardRect;
        flow.FindProperty("endScreenRoot").objectReferenceValue = endScreen;
        flow.FindProperty("endTitleText").objectReferenceValue = title;
        flow.FindProperty("endBodyText").objectReferenceValue = body;
        flow.FindProperty("restartButton").objectReferenceValue = restart;
        flow.FindProperty("loadSaveButton").objectReferenceValue = loadSave;
        flow.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
        flow.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        FixSavedPrefabRect();
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void FixSavedPrefabRect()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ApplyOverlayRect(contents.GetComponent<RectTransform>());
            Transform endScreen = contents.transform.Find("DemoFlowRoot/EndScreen");
            if (endScreen != null)
                endScreen.gameObject.SetActive(true);

            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void ApplyOverlayRect(RectTransform rect)
    {
        if (rect == null)
            return;

        SheikahUiStyle.Stretch(rect);
        rect.localScale = Vector3.one;
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, float anchorY, Sprite slotSprite)
    {
        Button button = SheikahUiStyle.CreateButton(
            parent,
            name,
            label,
            new Vector2(340f, 56f),
            SheikahUiStyle.Orange,
            SheikahUiStyle.Text,
            slotSprite,
            5.5f);
        SheikahUiStyle.PlaceCentered(button.GetComponent<RectTransform>(), anchorY, new Vector2(340f, 56f));
        return button;
    }

    private static void InstallPrefabInOpenScene(GameObject prefab)
    {
        if (prefab == null)
            return;

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != GameplayScenePath)
        {
            scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        DemoFlowUI[] existing = Object.FindObjectsOfType<DemoFlowUI>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            DemoFlowUI flow = existing[i];
            if (flow == null)
                continue;

            GameObject host = flow.gameObject;
            if (PrefabUtility.GetCorrespondingObjectFromSource(host) == prefab)
                Object.DestroyImmediate(host);
            else if (host.name == "DemoManager")
                Object.DestroyImmediate(flow);
            else if (host.name == "DemoFlowUI" || host.name == "DemoFlowOverlay")
                Object.DestroyImmediate(host);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "DemoFlowUI";
        ApplyOverlayRect(instance.GetComponent<RectTransform>());

        DemoGameManager manager = Object.FindObjectOfType<DemoGameManager>();
        if (manager != null)
        {
            SerializedObject serialized = new SerializedObject(manager);
            serialized.FindProperty("flowUI").objectReferenceValue = instance.GetComponent<DemoFlowUI>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
