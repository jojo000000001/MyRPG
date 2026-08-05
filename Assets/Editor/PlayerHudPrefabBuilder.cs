using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PlayerHudPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/PlayerHUD.prefab";
    private const string CatalogPath = "Assets/Resources/HealthBarSpriteCatalog.asset";
    private const string InventoryPrefabPath = "Assets/Prefabs/UI/InventoryUIRoot.prefab";

    private const string HealthRootName = "PlayerHealthBarRoot";
    private const string ExpRootName = "PlayerExperienceBarRoot";
    private const string LevelUpRootName = "LevelUpNoticeRoot";
    private const string PauseMenuName = "GameplayPauseMenu";

    [MenuItem("Tools/MyRPG/Rebuild Player HUD Prefab")]
    public static void RebuildPrefabAndScene()
    {
        SetupHealthBarCatalog.BuildOrLoadCatalog();
        GameObject prefab = BuildPrefabAsset();
        InstallPrefabInOpenScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Rebuild Player HUD Prefab", "PlayerHUD prefab 已生成，当前场景已替换为 prefab 实例。", "OK");
    }

    public static GameObject BuildPrefabAsset()
    {
        EnsureFolder("Assets/Prefabs/UI");

        GameObject root = CreateHudRoot();
        BuildHealthBarHierarchy(root.transform);
        BuildExperienceBarHierarchy(root.transform);
        BuildLevelUpNoticeHierarchy(root.transform);
        BuildPauseMenuHost(root.transform);

        WireRootComponents(root);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        rootRect.localScale = Vector3.one;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateHudRoot()
    {
        GameObject root = new GameObject(
            "PlayerHUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(PlayerHealthBar),
            typeof(PlayerExperienceBar),
            typeof(LevelUpNotice),
            typeof(InventoryUIRuntimeSpawner),
            typeof(PlayerStatPanel));

        RectTransform rect = root.GetComponent<RectTransform>();
        Stretch(rect);
        rect.localScale = Vector3.one;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return root;
    }

    private static void WireRootComponents(GameObject root)
    {
        HealthBarSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<HealthBarSpriteCatalog>(CatalogPath);
        InventoryUI inventoryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPrefabPath)?.GetComponent<InventoryUI>();
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GeneratedInventory/ui_inventory_panel_brown.png");
        Sprite rowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GeneratedInventory/ui_inventory_slot_beige_light.png");

        PlayerHealthBar healthBar = root.GetComponent<PlayerHealthBar>();
        SerializedObject healthBarObject = new SerializedObject(healthBar);
        healthBarObject.FindProperty("spriteCatalog").objectReferenceValue = catalog;
        healthBarObject.FindProperty("anchoredPosition").vector2Value = HudBarVisualStyle.HealthAnchoredPosition;
        healthBarObject.FindProperty("barSize").vector2Value = HudBarVisualStyle.BarSize;
        healthBarObject.FindProperty("capWidth").floatValue = HudBarVisualStyle.CapWidth;
        healthBarObject.FindProperty("fillInset").floatValue = HudBarVisualStyle.FillInset;
        healthBarObject.FindProperty("useTrackPlate").boolValue = false;
        healthBarObject.FindProperty("showBackTrack").boolValue = false;
        healthBarObject.FindProperty("hudLayoutVersion").intValue = 2;
        healthBarObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerExperienceBar experienceBar = root.GetComponent<PlayerExperienceBar>();
        SerializedObject experienceBarObject = new SerializedObject(experienceBar);
        experienceBarObject.FindProperty("spriteCatalog").objectReferenceValue = catalog;
        experienceBarObject.FindProperty("barSize").vector2Value = HudBarVisualStyle.BarSize;
        experienceBarObject.FindProperty("capWidth").floatValue = HudBarVisualStyle.CapWidth;
        experienceBarObject.FindProperty("fillInset").floatValue = HudBarVisualStyle.FillInset;
        experienceBarObject.FindProperty("expHudLayoutVersion").intValue = 6;
        experienceBarObject.FindProperty("anchoredPosition").vector2Value = HudBarVisualStyle.ComputeExpBarAnchoredPosition(20f, 4f);
        experienceBarObject.ApplyModifiedPropertiesWithoutUndo();

        InventoryUIRuntimeSpawner spawner = root.GetComponent<InventoryUIRuntimeSpawner>();
        SerializedObject spawnerObject = new SerializedObject(spawner);
        spawnerObject.FindProperty("inventoryUIPrefab").objectReferenceValue = inventoryPrefab;
        spawnerObject.FindProperty("spawnOnAwake").boolValue = true;
        spawnerObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerStatPanel statPanel = root.GetComponent<PlayerStatPanel>();
        SerializedObject statPanelObject = new SerializedObject(statPanel);
        statPanelObject.FindProperty("panelSprite").objectReferenceValue = panelSprite;
        statPanelObject.FindProperty("rowSprite").objectReferenceValue = rowSprite;
        statPanelObject.FindProperty("panelSize").vector2Value = new Vector2(400f, 697f);
        statPanelObject.FindProperty("gapFromInventory").floatValue = 18f;
        statPanelObject.FindProperty("followInventoryVisibility").boolValue = true;
        statPanelObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildHealthBarHierarchy(Transform parent)
    {
        GameObject root = CreateChild(parent, HealthRootName, typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = HudBarVisualStyle.HealthAnchoredPosition;
        rootRect.sizeDelta = HudBarVisualStyle.BarSize;

        GameObject fillMask = CreateChild(root.transform, "FillMask", typeof(RectTransform), typeof(RectMask2D));
        RectTransform fillMaskRect = fillMask.GetComponent<RectTransform>();
        fillMaskRect.anchorMin = new Vector2(0f, 0f);
        fillMaskRect.anchorMax = new Vector2(0f, 1f);
        fillMaskRect.pivot = new Vector2(0f, 0.5f);
        fillMaskRect.anchoredPosition = new Vector2(HudBarVisualStyle.FillInset, 0f);
        fillMaskRect.sizeDelta = new Vector2(Mathf.Max(0f, HudBarVisualStyle.BarSize.x - HudBarVisualStyle.FillInset * 2f), 0f);

        GameObject fill = CreateChild(fillMask.transform, "Fill", typeof(RectTransform));
        Stretch(fill.GetComponent<RectTransform>());
        CreateBarSegment(fill.transform, "Left", HudBarVisualStyle.CapWidth);
        CreateBarSegment(fill.transform, "Mid", Mathf.Max(0f, HudBarVisualStyle.BarSize.x - HudBarVisualStyle.CapWidth * 2f - HudBarVisualStyle.FillInset * 2f));
        CreateBarSegment(fill.transform, "Right", HudBarVisualStyle.CapWidth);
    }

    private static void BuildExperienceBarHierarchy(Transform parent)
    {
        const float labelHeight = 20f;
        const float labelBarGap = 4f;
        Vector2 anchoredPosition = HudBarVisualStyle.ComputeExpBarAnchoredPosition(labelHeight, labelBarGap);
        Vector2 barSize = HudBarVisualStyle.BarSize;

        GameObject root = CreateChild(parent, ExpRootName, typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(barSize.x, labelHeight + labelBarGap + barSize.y);

        TextMeshProUGUI levelText = CreateTmpLabel(root.transform, "LevelText", "Lv.1", TextAlignmentOptions.Left, 18f);
        ConfigureLabelRect(levelText.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, labelHeight));

        TextMeshProUGUI expText = CreateTmpLabel(root.transform, "ExpText", "0 / 100", TextAlignmentOptions.Right, 16f);
        ConfigureLabelRect(expText.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, labelHeight));
        expText.color = new Color(0.22f, 0.28f, 0.36f, 1f);

        float barTop = -(labelHeight + labelBarGap);
        GameObject barArea = CreateChild(root.transform, "BarArea", typeof(RectTransform));
        RectTransform barAreaRect = barArea.GetComponent<RectTransform>();
        barAreaRect.anchorMin = new Vector2(0f, 1f);
        barAreaRect.anchorMax = new Vector2(0f, 1f);
        barAreaRect.pivot = new Vector2(0f, 1f);
        barAreaRect.anchoredPosition = new Vector2(0f, barTop);
        barAreaRect.sizeDelta = barSize;

        float innerWidth = Mathf.Max(0f, barSize.x - HudBarVisualStyle.FillInset * 2f);
        BuildExpTrackGroup(barArea.transform, "Back", innerWidth, true);

        GameObject fillMask = CreateChild(barArea.transform, "FillMask", typeof(RectTransform), typeof(RectMask2D));
        fillMask.transform.SetAsLastSibling();
        RectTransform fillMaskRect = fillMask.GetComponent<RectTransform>();
        fillMaskRect.anchorMin = new Vector2(0f, 0f);
        fillMaskRect.anchorMax = new Vector2(0f, 1f);
        fillMaskRect.pivot = new Vector2(0f, 0.5f);
        fillMaskRect.anchoredPosition = new Vector2(HudBarVisualStyle.FillInset, 0f);
        fillMaskRect.sizeDelta = new Vector2(innerWidth, 0f);

        GameObject fill = CreateChild(fillMask.transform, "Fill", typeof(RectTransform));
        Stretch(fill.GetComponent<RectTransform>());
        CreateBarSegment(fill.transform, "Left", HudBarVisualStyle.CapWidth);
        CreateBarSegment(fill.transform, "Mid", Mathf.Max(0f, innerWidth - HudBarVisualStyle.CapWidth * 2f));
        CreateBarSegment(fill.transform, "Right", HudBarVisualStyle.CapWidth);
    }

    private static void BuildExpTrackGroup(Transform parent, string groupName, float width, bool inset)
    {
        GameObject group = CreateChild(parent, groupName, typeof(RectTransform));
        RectTransform groupRect = group.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0f, 0f);
        groupRect.anchorMax = new Vector2(0f, 1f);
        groupRect.pivot = new Vector2(0f, 0.5f);
        groupRect.anchoredPosition = new Vector2(inset ? HudBarVisualStyle.FillInset : 0f, 0f);
        groupRect.sizeDelta = new Vector2(width, 0f);
        CreateBarSegment(group.transform, "Left", HudBarVisualStyle.CapWidth);
        CreateBarSegment(group.transform, "Mid", Mathf.Max(0f, width - HudBarVisualStyle.CapWidth * 2f));
        CreateBarSegment(group.transform, "Right", HudBarVisualStyle.CapWidth);
    }

    private static void BuildLevelUpNoticeHierarchy(Transform parent)
    {
        GameObject root = CreateChild(parent, LevelUpRootName, typeof(RectTransform), typeof(CanvasGroup));
        Stretch(root.GetComponent<RectTransform>());
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;
        root.SetActive(false);

        GameObject backdrop = CreateChild(root.transform, "Backdrop", typeof(RectTransform), typeof(Image));
        Stretch(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
        backdrop.GetComponent<Image>().raycastTarget = false;

        GameObject panel = CreateChild(root.transform, "Panel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620f, 150f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.18f, 0.12f, 0.08f, 0.94f);
        panelImage.raycastTarget = false;

        CreateLegacyLabel(panel.transform, "Title", "升级！", 42, new Vector2(0f, 28f), new Vector2(580f, 56f));
        Text detail = CreateLegacyLabel(panel.transform, "Detail", string.Empty, 22, new Vector2(0f, -34f), new Vector2(580f, 48f));
        detail.color = new Color(0.92f, 0.84f, 0.62f, 1f);
    }

    private static void BuildPauseMenuHost(Transform parent)
    {
        GameObject host = CreateChild(parent, PauseMenuName, typeof(RectTransform), typeof(GameplayPauseMenu));
        Stretch(host.GetComponent<RectTransform>());
    }

    private static void InstallPrefabInOpenScene(GameObject prefab)
    {
        if (prefab == null)
            return;

        Player existingHud = Object.FindObjectOfType<Player>();

        Inventory inventory = existingHud != null ? existingHud.GetComponent<Inventory>() : null;

        GameObject oldHud = GameObject.Find("PlayerHUD");
        Transform oldTransform = oldHud != null ? oldHud.transform : null;
        Vector3 position = oldTransform != null ? oldTransform.position : Vector3.zero;
        Quaternion rotation = oldTransform != null ? oldTransform.rotation : Quaternion.identity;

        if (oldHud != null)
            Undo.DestroyObjectImmediate(oldHud);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return;

        Undo.RegisterCreatedObjectUndo(instance, "Install PlayerHUD Prefab");
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Vector3.one;

        PlayerHealthBar healthBar = instance.GetComponent<PlayerHealthBar>();
        if (healthBar != null && existingHud != null)
        {
            SerializedObject healthBarObject = new SerializedObject(healthBar);
            healthBarObject.FindProperty("player").objectReferenceValue = existingHud;
            healthBarObject.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerExperienceBar experienceBar = instance.GetComponent<PlayerExperienceBar>();
        if (experienceBar != null && existingHud != null)
        {
            SerializedObject experienceBarObject = new SerializedObject(experienceBar);
            experienceBarObject.FindProperty("player").objectReferenceValue = existingHud;
            experienceBarObject.ApplyModifiedPropertiesWithoutUndo();
        }

        LevelUpNotice levelUpNotice = instance.GetComponent<LevelUpNotice>();
        if (levelUpNotice != null && existingHud != null)
        {
            SerializedObject levelUpObject = new SerializedObject(levelUpNotice);
            levelUpObject.FindProperty("player").objectReferenceValue = existingHud;
            levelUpObject.ApplyModifiedPropertiesWithoutUndo();
        }

        InventoryUIRuntimeSpawner spawner = instance.GetComponent<InventoryUIRuntimeSpawner>();
        if (spawner != null)
        {
            SerializedObject spawnerObject = new SerializedObject(spawner);
            spawnerObject.FindProperty("player").objectReferenceValue = existingHud;
            spawnerObject.FindProperty("inventory").objectReferenceValue = inventory;
            spawnerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerStatPanel statPanel = instance.GetComponent<PlayerStatPanel>();
        if (statPanel != null && existingHud != null)
        {
            SerializedObject statPanelObject = new SerializedObject(statPanel);
            statPanelObject.FindProperty("player").objectReferenceValue = existingHud;
            statPanelObject.ApplyModifiedPropertiesWithoutUndo();
        }

        GameplayPauseMenu pauseMenu = instance.GetComponentInChildren<GameplayPauseMenu>(true);
        if (pauseMenu != null && existingHud != null)
        {
            SerializedObject pauseObject = new SerializedObject(pauseMenu);
            pauseObject.FindProperty("player").objectReferenceValue = existingHud;
            pauseObject.FindProperty("inventory").objectReferenceValue = inventory;
            pauseObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveOpenScenes();
    }

    private static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void CreateBarSegment(Transform parent, string name, float width)
    {
        GameObject segment = CreateChild(parent, name, typeof(RectTransform), typeof(Image));
        RectTransform rect = segment.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);

        float x = name == "Left" ? 0f : name == "Right" ? parent.GetComponent<RectTransform>().sizeDelta.x - width : HudBarVisualStyle.CapWidth;
        if (name == "Mid")
            x = HudBarVisualStyle.CapWidth;

        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, 0f);
        segment.GetComponent<Image>().raycastTarget = false;
    }

    private static TextMeshProUGUI CreateTmpLabel(Transform parent, string name, string text, TextAlignmentOptions alignment, float fontSize)
    {
        GameObject labelObject = CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = alignment;
        label.color = new Color(0.18f, 0.16f, 0.14f, 1f);
        label.raycastTarget = false;
        ChineseUITmpFont.Apply(label, fontSize, FontStyles.Bold);
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(20, 12, 8, 220);
        return label;
    }

    private static Text CreateLegacyLabel(Transform parent, string name, string text, int fontSize, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(Text));
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = new Color(1f, 0.92f, 0.55f, 1f);
        label.raycastTarget = false;
        ChineseUIFont.Apply(label, fontSize, FontStyle.Bold);

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return label;
    }

    private static void ConfigureLabelRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
