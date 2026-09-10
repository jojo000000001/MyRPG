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
    private const string HotbarSlotSpritePath = "Assets/Art/UI/GeneratedInventory/ui_hotbar_slot_translucent.png";

    private static Sprite LoadHotbarSlotSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(HotbarSlotSpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 1200f;
            importer.spriteBorder = new Vector4(40f, 40f, 40f, 40f);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(HotbarSlotSpritePath);
    }

    [MenuItem("Tools/MyRPG/Install Hotbar Into Player HUD Prefab")]
    public static void InstallHotbarIntoExistingPrefab()
    {
        Sprite slotSprite = LoadHotbarSlotSprite();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            PlayerHotbar hotbar = root.GetComponent<PlayerHotbar>();
            if (hotbar == null)
                hotbar = root.AddComponent<PlayerHotbar>();

            Transform existingHotbar = root.transform.Find("PlayerHotbarRoot");
            if (existingHotbar == null)
                existingHotbar = BuildHotbarHierarchy(root.transform, slotSprite).transform;
            else
                ApplyHotbarLayout(existingHotbar, slotSprite);

            SerializedObject hotbarObject = new SerializedObject(hotbar);
            hotbarObject.FindProperty("hotbarRoot").objectReferenceValue = existingHotbar as RectTransform;
            hotbarObject.FindProperty("slotSprite").objectReferenceValue = slotSprite;
            hotbarObject.FindProperty("slotCount").intValue = Inventory.DefaultHotbarSize;
            hotbarObject.FindProperty("slotSize").vector2Value = new Vector2(96f, 96f);
            hotbarObject.FindProperty("slotSpacing").floatValue = 0f;
            hotbarObject.FindProperty("leftMargin").floatValue = 0f;
            hotbarObject.FindProperty("bottomMargin").floatValue = 20f;
            hotbarObject.ApplyModifiedPropertiesWithoutUndo();

            PlayerHUD hud = root.GetComponent<PlayerHUD>();
            if (hud != null)
            {
                SerializedObject hudObject = new SerializedObject(hud);
                hudObject.FindProperty("hotbar").objectReferenceValue = hotbar;
                hudObject.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/MyRPG/Install Rupee Counter Into Player HUD Prefab")]
    public static void InstallRupeeCounterIntoExistingPrefab()
    {
        Sprite rupeeSprite = LoadRupeeSprite();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            PlayerRupeeHud rupeeHud = root.GetComponent<PlayerRupeeHud>();
            if (rupeeHud == null)
                rupeeHud = root.AddComponent<PlayerRupeeHud>();

            BuildRupeeHierarchy(root.transform, rupeeSprite);

            SerializedObject rupeeObject = new SerializedObject(rupeeHud);
            rupeeObject.FindProperty("rupeeSprite").objectReferenceValue = rupeeSprite;
            rupeeObject.FindProperty("anchoredPosition").vector2Value = new Vector2(-28f, -18f);
            rupeeObject.FindProperty("rootSize").vector2Value = new Vector2(210f, 56f);
            rupeeObject.FindProperty("iconSize").floatValue = 48f;
            rupeeObject.ApplyModifiedPropertiesWithoutUndo();

            PlayerHUD hud = root.GetComponent<PlayerHUD>();
            if (hud != null)
            {
                SerializedObject hudObject = new SerializedObject(hud);
                hudObject.FindProperty("rupeeHud").objectReferenceValue = rupeeHud;
                hudObject.ApplyModifiedPropertiesWithoutUndo();
            }

            QuestTrackerUI questTracker = root.GetComponentInChildren<QuestTrackerUI>(true);
            if (questTracker != null)
            {
                SerializedObject questObject = new SerializedObject(questTracker);
                questObject.FindProperty("anchoredPosition").vector2Value = new Vector2(-28f, -86f);
                questObject.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Installed rupee counter into PlayerHUD prefab.");
    }

    [MenuItem("Tools/MyRPG/Install Mouse Hint Into Player HUD Prefab")]
    public static void InstallMouseHintIntoExistingPrefab()
    {
        Sprite mouseSprite = LoadHudSprite(MouseHintSpritePath);
        Sprite shiftSprite = LoadHudSprite(ShiftKeySpritePath);
        Sprite spaceSprite = LoadHudSprite(SpaceKeySpritePath);
        Sprite inventorySprite = LoadHudSprite(InventoryKeySpritePath);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            PlayerMouseHint mouseHint = root.GetComponent<PlayerMouseHint>();
            if (mouseHint == null)
                mouseHint = root.AddComponent<PlayerMouseHint>();

            BuildMouseHintHierarchy(root.transform, mouseSprite, shiftSprite, spaceSprite, inventorySprite);

            SerializedObject hintObject = new SerializedObject(mouseHint);
            hintObject.FindProperty("mouseSprite").objectReferenceValue = mouseSprite;
            hintObject.FindProperty("shiftSprite").objectReferenceValue = shiftSprite;
            hintObject.FindProperty("spaceSprite").objectReferenceValue = spaceSprite;
            hintObject.FindProperty("inventorySprite").objectReferenceValue = inventorySprite;
            hintObject.FindProperty("anchoredPosition").vector2Value = new Vector2(-16f, 18f);
            hintObject.FindProperty("rootSize").vector2Value = new Vector2(292f, 260f);
            hintObject.FindProperty("mouseSize").vector2Value = new Vector2(92f, 128f);
            hintObject.FindProperty("shiftSize").vector2Value = new Vector2(78f, 36f);
            hintObject.FindProperty("spaceSize").vector2Value = new Vector2(128f, 36f);
            hintObject.FindProperty("inventorySize").vector2Value = new Vector2(42f, 42f);
            hintObject.ApplyModifiedPropertiesWithoutUndo();

            PlayerHUD hud = root.GetComponent<PlayerHUD>();
            if (hud != null)
            {
                SerializedObject hudObject = new SerializedObject(hud);
                hudObject.FindProperty("mouseHint").objectReferenceValue = mouseHint;
                hudObject.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Installed mouse hint into PlayerHUD prefab.");
    }

    private const string RupeeSpritePath = "Assets/Resources/UI/Hud/ui_rupee_green.png";
    private const string MouseHintSpritePath = "Assets/Resources/UI/Hud/ui_mouse_hint.png";
    private const string ShiftKeySpritePath = "Assets/Resources/UI/Hud/ui_key_shift.png";
    private const string SpaceKeySpritePath = "Assets/Resources/UI/Hud/ui_key_space.png";
    private const string InventoryKeySpritePath = "Assets/Resources/UI/Hud/ui_key_i.png";

    private static Sprite LoadRupeeSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(RupeeSpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = Vector4.zero;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(RupeeSpritePath);
    }

    private static Sprite LoadMouseHintSprite()
    {
        return LoadHudSprite(MouseHintSpritePath);
    }

    private static Sprite LoadHudSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = Vector4.zero;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void BuildMouseHintHierarchy(
        Transform parent,
        Sprite mouseSprite,
        Sprite shiftSprite,
        Sprite spaceSprite,
        Sprite inventorySprite)
    {
        Transform existing = parent.Find(PlayerMouseHint.RootName);
        GameObject root = existing != null
            ? existing.gameObject
            : CreateChild(parent, PlayerMouseHint.RootName, typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = new Vector2(-16f, 18f);
        rootRect.sizeDelta = new Vector2(292f, 260f);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        CreateHintIcon(root.transform, "Mouse", mouseSprite, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(92f, 128f));
        CreateHintIcon(root.transform, "KeyShift", shiftSprite, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 28f), new Vector2(78f, 36f));
        CreateHintIcon(root.transform, "KeySpace", spaceSprite, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 28f), new Vector2(128f, 36f));
        CreateHintIcon(root.transform, "KeyInventory", inventorySprite, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(234f, 26f), new Vector2(42f, 42f));

        CreateHintLabel(root.transform, "AttackCaption", "左键", 15f, new Vector2(0.5f, 1f), new Vector2(-48f, -8f), new Vector2(80f, 20f), new Color(0.92f, 0.86f, 0.70f, 0.72f));
        CreateHintLabel(root.transform, "AttackLabel", "攻击", 22f, new Vector2(0.5f, 1f), new Vector2(-48f, -26f), new Vector2(80f, 28f), new Color(0.98f, 0.93f, 0.78f, 0.88f));
        CreateHintLabel(root.transform, "DodgeCaption", "右键", 15f, new Vector2(0.5f, 1f), new Vector2(48f, -8f), new Vector2(80f, 20f), new Color(0.92f, 0.86f, 0.70f, 0.72f));
        CreateHintLabel(root.transform, "DodgeLabel", "闪避", 22f, new Vector2(0.5f, 1f), new Vector2(48f, -26f), new Vector2(80f, 28f), new Color(0.98f, 0.93f, 0.78f, 0.88f));
        CreateHintLabel(root.transform, "LabelShift", "加速", 16f, new Vector2(0f, 0f), new Vector2(49f, 6f), new Vector2(78f, 20f), new Color(0.98f, 0.93f, 0.78f, 0.88f), new Vector2(0.5f, 0f));
        CreateHintLabel(root.transform, "LabelSpace", "跳跃", 16f, new Vector2(0f, 0f), new Vector2(160f, 6f), new Vector2(128f, 20f), new Color(0.98f, 0.93f, 0.78f, 0.88f), new Vector2(0.5f, 0f));
        CreateHintLabel(root.transform, "LabelInventory", "背包", 16f, new Vector2(0f, 0f), new Vector2(255f, 6f), new Vector2(50f, 20f), new Color(0.98f, 0.93f, 0.78f, 0.88f), new Vector2(0.5f, 0f));
    }

    private static Image CreateHintIcon(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject iconObject = existing != null
            ? existing.gameObject
            : CreateChild(parent, name, typeof(RectTransform), typeof(Image));
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.86f);
        return image;
    }

    private static TextMeshProUGUI CreateHintLabel(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        Vector2? pivot = null)
    {
        Transform existing = parent.Find(name);
        GameObject labelObject = existing != null
            ? existing.gameObject
            : CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        ChineseUITmpFont.Apply(label, fontSize, FontStyles.Bold);
        label.outlineWidth = 0.26f;
        label.outlineColor = new Color(0.07f, 0.05f, 0.03f, 0.82f);
        return label;
    }

    private static void BuildRupeeHierarchy(Transform parent, Sprite rupeeSprite)
    {
        Transform existing = parent.Find("PlayerRupeeRoot");
        GameObject root = existing != null ? existing.gameObject : CreateChild(parent, "PlayerRupeeRoot", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-28f, -18f);
        rootRect.sizeDelta = new Vector2(210f, 56f);

        Transform iconTransform = root.transform.Find("Icon");
        GameObject iconObject = iconTransform != null
            ? iconTransform.gameObject
            : CreateChild(root.transform, "Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(4f, 0f);
        iconRect.sizeDelta = new Vector2(48f, 48f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = rupeeSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = Color.white;

        Transform amountTransform = root.transform.Find("Amount");
        GameObject amountObject = amountTransform != null
            ? amountTransform.gameObject
            : CreateChild(root.transform, "Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform amountRect = amountObject.GetComponent<RectTransform>();
        amountRect.anchorMin = new Vector2(0f, 0f);
        amountRect.anchorMax = new Vector2(1f, 1f);
        amountRect.pivot = new Vector2(0f, 0.5f);
        amountRect.offsetMin = new Vector2(62f, 0f);
        amountRect.offsetMax = new Vector2(-4f, 0f);

        TextMeshProUGUI amount = amountObject.GetComponent<TextMeshProUGUI>();
        amount.text = "0";
        amount.alignment = TextAlignmentOptions.MidlineLeft;
        amount.color = new Color(0.55f, 1f, 0.62f, 1f);
        amount.raycastTarget = false;
        amount.enableWordWrapping = false;
        amount.overflowMode = TextOverflowModes.Overflow;
        ChineseUITmpFont.Apply(amount, 34f, FontStyles.Bold);
        amount.outlineWidth = 0.28f;
        amount.outlineColor = new Color(0.04f, 0.10f, 0.08f, 0.92f);
    }

    [MenuItem("Tools/MyRPG/Apply HUD Bar Sprites")]
    public static void ApplyHudBarSpritesToExistingPrefab()
    {
        SetupHealthBarCatalog.BuildOrLoadCatalog();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform health = root.transform.Find(HealthRootName);
            int healthIndex = health != null ? health.GetSiblingIndex() : 0;
            if (health != null)
                Object.DestroyImmediate(health.gameObject);

            Transform exp = root.transform.Find(ExpRootName);
            int expIndex = exp != null ? exp.GetSiblingIndex() : Mathf.Min(1, root.transform.childCount);
            if (exp != null)
                Object.DestroyImmediate(exp.gameObject);

            BuildHealthBarHierarchy(root.transform);
            BuildExperienceBarHierarchy(root.transform);

            Transform newHealth = root.transform.Find(HealthRootName);
            if (newHealth != null)
                newHealth.SetSiblingIndex(Mathf.Clamp(healthIndex, 0, root.transform.childCount - 1));

            Transform newExp = root.transform.Find(ExpRootName);
            if (newExp != null)
                newExp.SetSiblingIndex(Mathf.Clamp(expIndex, 0, root.transform.childCount - 1));

            HealthBarSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<HealthBarSpriteCatalog>(CatalogPath);
            PlayerHealthBar healthBar = root.GetComponent<PlayerHealthBar>();
            if (healthBar != null)
            {
                SerializedObject healthBarObject = new SerializedObject(healthBar);
                healthBarObject.FindProperty("showBackTrack").boolValue = true;
                healthBarObject.FindProperty("hudLayoutVersion").intValue = 4;
                healthBarObject.FindProperty("spriteCatalog").objectReferenceValue = catalog;
                healthBarObject.ApplyModifiedPropertiesWithoutUndo();
            }

            PlayerExperienceBar experienceBar = root.GetComponent<PlayerExperienceBar>();
            if (experienceBar != null)
            {
                SerializedObject experienceBarObject = new SerializedObject(experienceBar);
                experienceBarObject.FindProperty("spriteCatalog").objectReferenceValue = catalog;
                experienceBarObject.FindProperty("barSize").vector2Value = HudBarVisualStyle.BarSize;
                experienceBarObject.FindProperty("capWidth").floatValue = HudBarVisualStyle.CapWidth;
                experienceBarObject.FindProperty("fillInset").floatValue = HudBarVisualStyle.FillInset;
                experienceBarObject.FindProperty("expHudLayoutVersion").intValue = 7;
                experienceBarObject.FindProperty("anchoredPosition").vector2Value = HudBarVisualStyle.ComputeExpBarAnchoredPosition();
                experienceBarObject.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("PlayerHudPrefabBuilder: HUD health/exp bars now use Kenney fill sprites.");
    }

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
        BuildOverlayHost<QuestTrackerUI>(root.transform, "QuestTrackerUI");
        BuildOverlayHost<DialogueUI>(root.transform, "DialogueUI");

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
            typeof(PlayerHUD),
            typeof(PlayerHealthBar),
            typeof(PlayerExperienceBar),
            typeof(LevelUpNotice),
            typeof(InventoryUIRuntimeSpawner),
            typeof(PlayerStatPanel),
            typeof(PlayerHotbar),
            typeof(PlayerMouseHint));

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
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GeneratedInventory/ui_inventory_panel_fantasy.png");
        Sprite rowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GeneratedInventory/ui_stat_row_leather.png");
        Sprite slotSprite = LoadHotbarSlotSprite();

        PlayerHealthBar healthBar = root.GetComponent<PlayerHealthBar>();
        SerializedObject healthBarObject = new SerializedObject(healthBar);
        healthBarObject.FindProperty("spriteCatalog").objectReferenceValue = catalog;
        healthBarObject.FindProperty("anchoredPosition").vector2Value = HudBarVisualStyle.HealthAnchoredPosition;
        healthBarObject.FindProperty("barSize").vector2Value = HudBarVisualStyle.BarSize;
        healthBarObject.FindProperty("capWidth").floatValue = HudBarVisualStyle.CapWidth;
        healthBarObject.FindProperty("fillInset").floatValue = HudBarVisualStyle.FillInset;
        healthBarObject.FindProperty("useTrackPlate").boolValue = false;
        healthBarObject.FindProperty("showBackTrack").boolValue = true;
        healthBarObject.FindProperty("hudLayoutVersion").intValue = 4;
        healthBarObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerExperienceBar experienceBar = root.GetComponent<PlayerExperienceBar>();
        SerializedObject experienceBarObject = new SerializedObject(experienceBar);
        experienceBarObject.FindProperty("spriteCatalog").objectReferenceValue = catalog;
        experienceBarObject.FindProperty("barSize").vector2Value = HudBarVisualStyle.BarSize;
        experienceBarObject.FindProperty("capWidth").floatValue = HudBarVisualStyle.CapWidth;
        experienceBarObject.FindProperty("fillInset").floatValue = HudBarVisualStyle.FillInset;
        experienceBarObject.FindProperty("expHudLayoutVersion").intValue = 7;
        experienceBarObject.FindProperty("anchoredPosition").vector2Value = HudBarVisualStyle.ComputeExpBarAnchoredPosition();
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
        statPanelObject.FindProperty("panelSize").vector2Value = new Vector2(400f, 616f);
        statPanelObject.FindProperty("padding").floatValue = 32f;
        statPanelObject.FindProperty("rowHeight").floatValue = 40f;
        statPanelObject.FindProperty("gapFromInventory").floatValue = 18f;
        statPanelObject.FindProperty("followInventoryVisibility").boolValue = true;
        statPanelObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerHotbar hotbar = root.GetComponent<PlayerHotbar>();
        Transform existingHotbar = root.transform.Find("PlayerHotbarRoot");
        if (existingHotbar == null)
            existingHotbar = BuildHotbarHierarchy(root.transform, slotSprite).transform;

        SerializedObject hotbarObject = new SerializedObject(hotbar);
        hotbarObject.FindProperty("hotbarRoot").objectReferenceValue = existingHotbar as RectTransform;
        hotbarObject.FindProperty("slotSprite").objectReferenceValue = slotSprite;
        hotbarObject.FindProperty("slotCount").intValue = Inventory.DefaultHotbarSize;
        hotbarObject.FindProperty("slotSize").vector2Value = new Vector2(96f, 96f);
        hotbarObject.FindProperty("slotSpacing").floatValue = 0f;
        hotbarObject.FindProperty("leftMargin").floatValue = 0f;
        hotbarObject.FindProperty("bottomMargin").floatValue = 20f;
        hotbarObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerHUD hud = root.GetComponent<PlayerHUD>();
        SerializedObject hudObject = new SerializedObject(hud);
        hudObject.FindProperty("healthBar").objectReferenceValue = healthBar;
        hudObject.FindProperty("experienceBar").objectReferenceValue = experienceBar;
        hudObject.FindProperty("levelUpNotice").objectReferenceValue = root.GetComponent<LevelUpNotice>();
        hudObject.FindProperty("inventorySpawner").objectReferenceValue = spawner;
        hudObject.FindProperty("statPanel").objectReferenceValue = statPanel;
        hudObject.FindProperty("hotbar").objectReferenceValue = hotbar;
        hudObject.FindProperty("rupeeHud").objectReferenceValue = root.GetComponent<PlayerRupeeHud>();
        hudObject.FindProperty("mouseHint").objectReferenceValue = root.GetComponent<PlayerMouseHint>();
        hudObject.FindProperty("pauseMenu").objectReferenceValue = root.GetComponentInChildren<GameplayPauseMenu>(true);
        hudObject.FindProperty("questTracker").objectReferenceValue = root.GetComponentInChildren<QuestTrackerUI>(true);
        hudObject.FindProperty("dialogueUI").objectReferenceValue = root.GetComponentInChildren<DialogueUI>(true);
        hudObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject BuildHotbarHierarchy(Transform parent, Sprite slotSprite)
    {
        const int count = Inventory.DefaultHotbarSize;
        const float slotSize = 96f;
        const float spacing = 0f;
        float width = count * slotSize + (count - 1) * spacing;

        GameObject root = CreateChild(parent, "PlayerHotbarRoot", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 20f);
        rootRect.sizeDelta = new Vector2(width, slotSize);

        GameObject grid = CreateChild(root.transform, "SlotGrid", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Stretch(grid.GetComponent<RectTransform>());
        HorizontalLayoutGroup layout = grid.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < count; i++)
            CreateHotbarSlot(grid.transform, i, slotSize, slotSprite);

        return root;
    }

    private static void ApplyHotbarLayout(Transform hotbarRoot, Sprite slotSprite)
    {
        const int count = Inventory.DefaultHotbarSize;
        const float slotSize = 96f;
        const float spacing = 0f;
        float width = count * slotSize + (count - 1) * spacing;

        RectTransform rootRect = hotbarRoot as RectTransform;
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 20f);
        rootRect.sizeDelta = new Vector2(width, slotSize);

        Transform grid = hotbarRoot.Find("SlotGrid");
        HorizontalLayoutGroup layout = grid != null ? grid.GetComponent<HorizontalLayoutGroup>() : null;
        if (layout != null)
            layout.spacing = spacing;

        Transform slotParent = grid != null ? grid : hotbarRoot;
        for (int i = 0; i < count; i++)
        {
            Transform slot = slotParent.Find("Slot_" + (i + 1).ToString("00"));
            if (slot == null)
            {
                CreateHotbarSlot(slotParent, i, slotSize, slotSprite);
                slot = slotParent.Find("Slot_" + (i + 1).ToString("00"));
            }

            if (slot == null)
                continue;

            RectTransform slotRect = slot as RectTransform;
            slotRect.sizeDelta = new Vector2(slotSize, slotSize);

            Image background = slot.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = slotSprite;
                background.type = slotSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                background.color = Color.white;
            }

            Transform icon = slot.Find("Icon");
            if (icon != null)
                (icon as RectTransform).sizeDelta = new Vector2(slotSize - 24f, slotSize - 24f);
        }
    }

    private static void CreateHotbarSlot(Transform parent, int index, float slotSize, Sprite slotSprite)
    {
        GameObject slot = CreateChild(parent, "Slot_" + (index + 1).ToString("00"), typeof(RectTransform), typeof(Image), typeof(PlayerHotbarSlotUI));
        RectTransform slotRect = slot.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(slotSize, slotSize);

        Image background = slot.GetComponent<Image>();
        background.sprite = slotSprite;
        background.type = slotSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = Color.white;
        background.raycastTarget = true;

        GameObject icon = CreateChild(slot.transform, "Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(slotSize - 20f, slotSize - 20f);
        Image iconImage = icon.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = false;
        iconImage.canvasRenderer.cullTransparentMesh = false;

        TextMeshProUGUI count = CreateTmpLabel(slot.transform, "Count", string.Empty, TextAlignmentOptions.BottomRight, 18f);
        RectTransform countRect = count.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = new Vector2(6f, 4f);
        countRect.offsetMax = new Vector2(-7f, -4f);
        count.color = Color.white;

        TextMeshProUGUI key = CreateTmpLabel(slot.transform, "Key", (index + 1).ToString(), TextAlignmentOptions.TopLeft, 16f);
        RectTransform keyRect = key.rectTransform;
        keyRect.anchorMin = Vector2.zero;
        keyRect.anchorMax = Vector2.one;
        keyRect.offsetMin = new Vector2(8f, 4f);
        keyRect.offsetMax = new Vector2(-6f, -5f);
        key.color = new Color(0.96f, 0.90f, 0.74f, 1f);
    }

    private static void BuildHealthBarHierarchy(Transform parent)
    {
        HealthBarSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<HealthBarSpriteCatalog>(CatalogPath);
        Vector2 barSize = HudBarVisualStyle.BarSize;
        GameObject root = CreateChild(parent, HealthRootName, typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = HudBarVisualStyle.HealthAnchoredPosition;
        rootRect.sizeDelta = HudBarVisualStyle.LabeledRootSize(barSize);

        GameObject barArea = CreateChild(root.transform, "BarArea", typeof(RectTransform));
        RectTransform barAreaRect = barArea.GetComponent<RectTransform>();
        barAreaRect.anchorMin = new Vector2(0f, 1f);
        barAreaRect.anchorMax = new Vector2(0f, 1f);
        barAreaRect.pivot = new Vector2(0f, 1f);
        barAreaRect.anchoredPosition = Vector2.zero;
        barAreaRect.sizeDelta = barSize;

        float barWidth = barSize.x;
        float innerWidth = Mathf.Max(0f, barWidth - HudBarVisualStyle.FillInset * 2f);
        GameObject back = CreateChild(barArea.transform, "Back", typeof(RectTransform));
        Stretch(back.GetComponent<RectTransform>());
        CreateColoredBarSegments(back.transform, barWidth, catalog, false, isBackTrack: true);

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
        CreateColoredBarSegments(fill.transform, innerWidth, catalog, true);

        TextMeshProUGUI hpText = CreateTmpLabel(root.transform, "HpText", "50 / 50", TextAlignmentOptions.Right, 16f);
        ConfigureLabelRect(
            hpText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, HudBarVisualStyle.LabelAnchoredY(barSize)),
            new Vector2(0f, HudBarVisualStyle.LabelHeight));
        hpText.color = HudBarVisualStyle.ValueTextColor;
    }

    private static void BuildExperienceBarHierarchy(Transform parent)
    {
        HealthBarSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<HealthBarSpriteCatalog>(CatalogPath);
        Vector2 barSize = HudBarVisualStyle.BarSize;
        Vector2 anchoredPosition = HudBarVisualStyle.ComputeExpBarAnchoredPosition();

        GameObject root = CreateChild(parent, ExpRootName, typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = HudBarVisualStyle.LabeledRootSize(barSize);

        GameObject barArea = CreateChild(root.transform, "BarArea", typeof(RectTransform));
        RectTransform barAreaRect = barArea.GetComponent<RectTransform>();
        barAreaRect.anchorMin = new Vector2(0f, 1f);
        barAreaRect.anchorMax = new Vector2(0f, 1f);
        barAreaRect.pivot = new Vector2(0f, 1f);
        barAreaRect.anchoredPosition = Vector2.zero;
        barAreaRect.sizeDelta = barSize;

        float innerWidth = Mathf.Max(0f, barSize.x - HudBarVisualStyle.FillInset * 2f);
        BuildBarTrackGroup(barArea.transform, "Back", innerWidth, true, catalog);

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
        CreateColoredBarSegments(fill.transform, innerWidth, catalog, false);

        float labelY = HudBarVisualStyle.LabelAnchoredY(barSize);
        TextMeshProUGUI levelText = CreateTmpLabel(root.transform, "LevelText", "Lv.1", TextAlignmentOptions.Left, 18f);
        ConfigureLabelRect(
            levelText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, labelY),
            new Vector2(0f, HudBarVisualStyle.LabelHeight));

        TextMeshProUGUI expText = CreateTmpLabel(root.transform, "ExpText", "0 / 100", TextAlignmentOptions.Right, 16f);
        ConfigureLabelRect(
            expText.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, labelY),
            new Vector2(0f, HudBarVisualStyle.LabelHeight));
        expText.color = HudBarVisualStyle.ValueTextColor;
    }

    private static void BuildBarTrackGroup(Transform parent, string groupName, float width, bool inset, HealthBarSpriteCatalog catalog)
    {
        GameObject group = CreateChild(parent, groupName, typeof(RectTransform));
        RectTransform groupRect = group.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0f, 0f);
        groupRect.anchorMax = new Vector2(0f, 1f);
        groupRect.pivot = new Vector2(0f, 0.5f);
        groupRect.anchoredPosition = new Vector2(inset ? HudBarVisualStyle.FillInset : 0f, 0f);
        groupRect.sizeDelta = new Vector2(width, 0f);
        CreateColoredBarSegments(group.transform, width, catalog, false, isBackTrack: true);
    }

    private static void CreateColoredBarSegments(Transform parent, float width, HealthBarSpriteCatalog catalog, bool greenFill, bool isBackTrack = false)
    {
        float cap = HudBarVisualStyle.CapWidth;
        float midWidth = Mathf.Max(0f, width - cap * 2f);
        Sprite left = GetBarSprite(catalog, "Left", greenFill, isBackTrack);
        Sprite mid = GetBarSprite(catalog, "Mid", greenFill, isBackTrack);
        Sprite right = GetBarSprite(catalog, "Right", greenFill, isBackTrack);
        Color tint = ResolveBarTint(left, greenFill, isBackTrack);

        CreateBarSegment(parent, "Left", 0f, cap, left, tint);
        CreateBarSegment(parent, "Mid", cap, midWidth, mid, tint);
        CreateBarSegment(parent, "Right", cap + midWidth, cap, right, tint);
    }

    private static Color ResolveBarTint(Sprite sprite, bool greenFill, bool isBackTrack)
    {
        if (isBackTrack)
            return sprite != null ? HudBarVisualStyle.BackBarTint : HudBarVisualStyle.BackBarFallback;

        if (sprite != null)
            return Color.white;

        return greenFill ? HudBarVisualStyle.GreenFillTint : HudBarVisualStyle.BlueFillTint;
    }

    private static Sprite GetBarSprite(HealthBarSpriteCatalog catalog, string segment, bool greenFill, bool isBackTrack)
    {
        if (catalog == null)
            return null;

        if (isBackTrack)
            return catalog.GetBack(segment);

        return greenFill ? catalog.GetGreen(segment) : catalog.GetBlue(segment);
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
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = SheikahUiStyle.Dim;
        backdropImage.raycastTarget = false;

        GameObject panel = SheikahUiStyle.CreateCard(root.transform, "Panel", new Vector2(620f, 150f), SheikahUiStyle.PanelSprite, 2.6f);
        Image fill = panel.transform.Find("Background")?.GetComponent<Image>();
        if (fill != null)
            fill.raycastTarget = false;

        Text title = CreateLegacyLabel(panel.transform, "Title", "升级！", 42, new Vector2(0f, 28f), new Vector2(580f, 56f));
        title.color = SheikahUiStyle.Orange;
        Text detail = CreateLegacyLabel(panel.transform, "Detail", string.Empty, 22, new Vector2(0f, -34f), new Vector2(580f, 48f));
        detail.color = SheikahUiStyle.Text;
    }

    private static void BuildPauseMenuHost(Transform parent)
    {
        GameObject host = CreateChild(parent, PauseMenuName, typeof(RectTransform), typeof(GameplayPauseMenu));
        Stretch(host.GetComponent<RectTransform>());
    }

    private static void BuildOverlayHost<T>(Transform parent, string name) where T : Component
    {
        GameObject host = CreateChild(parent, name, typeof(RectTransform), typeof(T));
        Stretch(host.GetComponent<RectTransform>());
    }

    private static void InstallPrefabInOpenScene(GameObject prefab)
    {
        if (prefab == null)
            return;

        Player existingHud = Player.Resolve();

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

        PlayerHotbar hotbar = instance.GetComponent<PlayerHotbar>();
        if (hotbar != null && existingHud != null)
        {
            SerializedObject hotbarObject = new SerializedObject(hotbar);
            hotbarObject.FindProperty("player").objectReferenceValue = existingHud;
            hotbarObject.FindProperty("inventory").objectReferenceValue = inventory;
            hotbarObject.ApplyModifiedPropertiesWithoutUndo();
        }

        GameplayPauseMenu pauseMenu = instance.GetComponentInChildren<GameplayPauseMenu>(true);
        if (pauseMenu != null && existingHud != null)
        {
            SerializedObject pauseObject = new SerializedObject(pauseMenu);
            pauseObject.FindProperty("player").objectReferenceValue = existingHud;
            pauseObject.FindProperty("inventory").objectReferenceValue = inventory;
            pauseObject.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerHUD hud = instance.GetComponent<PlayerHUD>();
        if (hud != null && existingHud != null)
        {
            SerializedObject hudObject = new SerializedObject(hud);
            hudObject.FindProperty("player").objectReferenceValue = existingHud;
            hudObject.ApplyModifiedPropertiesWithoutUndo();
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

    private static void CreateBarSegment(Transform parent, string name, float x, float width, Sprite sprite, Color tint)
    {
        GameObject segment = CreateChild(parent, name, typeof(RectTransform), typeof(Image));
        RectTransform rect = segment.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, 0f);

        Image image = segment.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sprite;
        image.type = name == "Mid" && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = tint;
        image.preserveAspect = false;
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
