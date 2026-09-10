using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class InventoryUIPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/InventoryUIRoot.prefab";
    private const string PanelSpritePath = "Assets/Art/UI/GeneratedInventory/ui_inventory_panel_fantasy.png";
    private const string SlotSpritePath = "Assets/Art/UI/GeneratedInventory/ui_inventory_slot_leather.png";
    private const string StatRowSpritePath = "Assets/Art/UI/GeneratedInventory/ui_stat_row_leather.png";
    private const string ButtonSpritePath = "Assets/Art/UI/GeneratedInventory/ui_inventory_button_brown.png";
    private const string CloseIconSpritePath = "Assets/Art/UI/GeneratedInventory/ui_inventory_close_cross_brown.png";

    static InventoryUIPrefabBuilder()
    {
        EditorApplication.delayCall += AutoBuildIfNeeded;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/MyRPG/Rebuild Inventory UI Prefab")]
    public static void RebuildInventoryPrefabAndScene()
    {
        GameObject prefab = BuildPrefab();
        InstallRuntimeSpawnerInOpenScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void AutoBuildIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && SceneHasRuntimeSpawner())
            return;

        RebuildInventoryPrefabAndScene();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += AutoBuildIfNeeded;
    }

    private static GameObject BuildPrefab()
    {
        EnsureFolder("Assets/Prefabs/UI");

        Sprite panelSprite = LoadSprite(PanelSpritePath);
        Sprite slotSprite = LoadSprite(SlotSpritePath);
        Sprite buttonSprite = LoadSprite(ButtonSpritePath);
        Sprite closeIconSprite = LoadSprite(CloseIconSpritePath);

        GameObject root = new GameObject("InventoryUIRoot", typeof(RectTransform), typeof(InventoryUI));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        InventoryUI inventoryUI = root.GetComponent<InventoryUI>();
        SerializedObject ui = new SerializedObject(inventoryUI);
        ui.FindProperty("panelSprite").objectReferenceValue = panelSprite;
        ui.FindProperty("slotSprite").objectReferenceValue = slotSprite;
        ui.FindProperty("buttonSprite").objectReferenceValue = buttonSprite;
        ui.FindProperty("closeIconSprite").objectReferenceValue = closeIconSprite;
        ui.FindProperty("toggleKey").intValue = (int)KeyCode.I;
        ui.FindProperty("alternateToggleKey").intValue = (int)KeyCode.B;
        ui.FindProperty("openOnStart").boolValue = false;
        ui.FindProperty("columns").intValue = 5;
        ui.FindProperty("rows").intValue = 4;
        ui.FindProperty("panelSize").vector2Value = new Vector2(520f, 560f);
        ui.FindProperty("slotSize").vector2Value = new Vector2(76f, 76f);
        ui.FindProperty("slotSpacing").vector2Value = new Vector2(10f, 10f);
        ui.FindProperty("longPressSeconds").floatValue = 0.35f;
        ui.ApplyModifiedPropertiesWithoutUndo();

        GameObject panel = CreateChild(root, "InventoryPanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-36f, 0f);
        panelRect.sizeDelta = new Vector2(520f, 560f);
        ConfigureImage(panel.GetComponent<Image>(), panelSprite, Image.Type.Sliced, Color.white);

        TextMeshProUGUI title = CreateText(panel, "Title", "背包", 34f, TextAlignmentOptions.Left);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(28f, -24f);
        titleRect.sizeDelta = new Vector2(-92f, 48f);
        title.color = new Color(0.18f, 0.1f, 0.045f, 1f);

        CreateCloseButton(panel, buttonSprite, closeIconSprite);
        CreateSlotGrid(panel, slotSprite);

        TextMeshProUGUI detail = CreateText(panel, "DetailText", string.Empty, 19f, TextAlignmentOptions.Left);
        RectTransform detailRect = detail.rectTransform;
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 0f);
        detailRect.pivot = new Vector2(0.5f, 0f);
        detailRect.anchoredPosition = new Vector2(28f, 24f);
        detailRect.sizeDelta = new Vector2(-56f, 86f);
        detail.color = new Color(0.18f, 0.1f, 0.045f, 1f);
        detail.enableWordWrapping = true;

        GameObject dragIcon = CreateChild(root, "DragIcon", typeof(RectTransform), typeof(Image));
        RectTransform dragRect = dragIcon.GetComponent<RectTransform>();
        dragRect.anchorMin = new Vector2(0.5f, 0.5f);
        dragRect.anchorMax = new Vector2(0.5f, 0.5f);
        dragRect.pivot = new Vector2(0.5f, 0.5f);
        dragRect.sizeDelta = new Vector2(66f, 66f);
        Image dragImage = dragIcon.GetComponent<Image>();
        dragImage.preserveAspect = true;
        dragImage.raycastTarget = false;
        dragIcon.SetActive(false);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void InstallRuntimeSpawnerInOpenScene(GameObject prefab)
    {
        if (prefab == null)
            return;

        GameObject hud = GameObject.Find("PlayerHUD");
        if (hud == null)
            return;

        Transform existing = hud.transform.Find("InventoryUIRoot");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        InventoryUIRuntimeSpawner spawner = hud.GetComponent<InventoryUIRuntimeSpawner>();
        if (spawner == null)
            spawner = hud.AddComponent<InventoryUIRuntimeSpawner>();

        Player player = Player.Resolve();
        Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("inventoryUIPrefab").objectReferenceValue = prefab.GetComponent<InventoryUI>();
        serializedSpawner.FindProperty("player").objectReferenceValue = player;
        serializedSpawner.FindProperty("inventory").objectReferenceValue = inventory;
        serializedSpawner.FindProperty("spawnOnAwake").boolValue = true;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        PlayerStatPanel statPanel = hud.GetComponent<PlayerStatPanel>();
        if (statPanel == null)
            statPanel = hud.AddComponent<PlayerStatPanel>();

        SerializedObject serializedStats = new SerializedObject(statPanel);
        serializedStats.FindProperty("player").objectReferenceValue = player;
        serializedStats.FindProperty("panelSprite").objectReferenceValue = LoadSprite(PanelSpritePath);
        serializedStats.FindProperty("rowSprite").objectReferenceValue = LoadSprite(StatRowSpritePath);
        serializedStats.FindProperty("followInventoryVisibility").boolValue = true;
        serializedStats.FindProperty("gapFromInventory").floatValue = 18f;
        serializedStats.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawner);
        EditorUtility.SetDirty(statPanel);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    private static void CreateCloseButton(GameObject parent, Sprite buttonSprite, Sprite closeIconSprite)
    {
        GameObject button = CreateChild(parent, "CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-24f, -22f);
        buttonRect.sizeDelta = new Vector2(48f, 48f);
        ConfigureImage(button.GetComponent<Image>(), buttonSprite, Image.Type.Sliced, Color.white);

        GameObject icon = CreateChild(button, "Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(20f, 20f);
        ConfigureImage(icon.GetComponent<Image>(), closeIconSprite, Image.Type.Simple, Color.white);
        icon.GetComponent<Image>().raycastTarget = false;
    }

    private static void CreateSlotGrid(GameObject parent, Sprite slotSprite)
    {
        GameObject grid = CreateChild(parent, "SlotGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 1f);
        gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchoredPosition = new Vector2(0f, -92f);
        gridRect.sizeDelta = new Vector2(420f, 334f);

        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(76f, 76f);
        layout.spacing = new Vector2(10f, 10f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        layout.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < 20; i++)
            CreateSlot(grid, i, slotSprite);
    }

    private static void CreateSlot(GameObject parent, int index, Sprite slotSprite)
    {
        GameObject slot = CreateChild(parent, "Slot_" + (index + 1).ToString("00"), typeof(RectTransform), typeof(Image), typeof(InventorySlotUI));
        ConfigureImage(slot.GetComponent<Image>(), slotSprite, Image.Type.Sliced, Color.white);

        GameObject icon = CreateChild(slot, "Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(58f, 58f);
        Image iconImage = icon.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        TextMeshProUGUI count = CreateText(slot, "Count", string.Empty, 18f, TextAlignmentOptions.BottomRight);
        RectTransform countRect = count.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = new Vector2(5f, 4f);
        countRect.offsetMax = new Vector2(-7f, -4f);
        count.color = Color.white;
        count.fontStyle = FontStyles.Bold;
        count.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateText(GameObject parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = alignment;
        ChineseUITmpFont.Apply(label, fontSize);
        label.raycastTarget = false;
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
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
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

    private static bool SceneHasRuntimeSpawner()
    {
        GameObject hud = GameObject.Find("PlayerHUD");
        if (hud == null)
            return false;

        InventoryUIRuntimeSpawner spawner = hud.GetComponent<InventoryUIRuntimeSpawner>();
        PlayerStatPanel statPanel = hud.GetComponent<PlayerStatPanel>();
        return spawner != null && statPanel != null && hud.transform.Find("InventoryUIRoot") == null;
    }
}
