using UnityEditor;
using UnityEngine;

public static class SetupEnemyHealthBarSprites
{
    private const string Folder = "Assets/Resources/UI/EnemyHealthBar";
    private const string UiPrefabFolder = "Assets/Prefabs/UI";
    private const string GoblinFrame = Folder + "/ui_goblin_hp_frame.png";
    private const string GoblinFill = Folder + "/ui_goblin_hp_fill.png";
    private const string DragonFrame = Folder + "/ui_dragon_hp_frame.png";
    private const string DragonFill = Folder + "/ui_dragon_hp_fill.png";
    private const string Back = Folder + "/ui_enemy_hp_back.png";
    private const string GoblinStylePath = Folder + "/GoblinHealthBarStyle.asset";
    private const string DragonStylePath = Folder + "/DragonHealthBarStyle.asset";
    private const string GoblinBarPrefabPath = UiPrefabFolder + "/GoblinHealthBar.prefab";
    private const string DragonBarPrefabPath = UiPrefabFolder + "/DragonHealthBar.prefab";

    [MenuItem("Tools/MyRPG/Setup Enemy Health Bar Sprites")]
    public static void SetupFromMenu()
    {
        ConfigureSprite(GoblinFrame, new Vector4(72f, 22f, 72f, 22f));
        ConfigureSprite(GoblinFill, new Vector4(48f, 8f, 48f, 8f));
        ConfigureSprite(DragonFrame, new Vector4(150f, 30f, 150f, 30f));
        ConfigureSprite(DragonFill, new Vector4(80f, 10f, 80f, 10f));
        ConfigureSprite(Back, new Vector4(48f, 10f, 48f, 10f));

        CreateOrUpdateStyle(
            GoblinStylePath,
            GoblinFrame,
            GoblinFill,
            Back,
            new Vector2(1.76f, 0.36f),
            new Vector3(0f, 2.22f, 0f),
            new Vector2(5f, 2f),
            1f);

        CreateOrUpdateStyle(
            DragonStylePath,
            DragonFrame,
            DragonFill,
            Back,
            new Vector2(2.95f, 0.70f),
            new Vector3(0f, 4.85f, 0f),
            new Vector2(12f, 4f),
            1f);

        EnemyHealthBarStyle goblinStyle = AssetDatabase.LoadAssetAtPath<EnemyHealthBarStyle>(GoblinStylePath);
        EnemyHealthBarStyle dragonStyle = AssetDatabase.LoadAssetAtPath<EnemyHealthBarStyle>(DragonStylePath);
        GameObject goblinBarPrefab = CreateOrUpdateBarPrefab(GoblinBarPrefabPath, "GoblinHealthBar", goblinStyle, 60);
        GameObject dragonBarPrefab = CreateOrUpdateBarPrefab(DragonBarPrefabPath, "DragonHealthBar", dragonStyle, 80);

        AssignBarToCharacterPrefab(
            "Assets/Prefabs/Goblin.prefab",
            goblinStyle,
            "UI/EnemyHealthBar/GoblinHealthBarStyle",
            goblinBarPrefab);
        AssignBarToCharacterPrefab(
            "Assets/Prefabs/DragonBoss.prefab",
            dragonStyle,
            "UI/EnemyHealthBar/DragonHealthBarStyle",
            dragonBarPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupEnemyHealthBarSprites] Goblin/Dragon health bar prefabs nested onto character prefabs.");
    }

    private static void ConfigureSprite(string assetPath, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[SetupEnemyHealthBarSprites] Missing texture: " + assetPath);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    private static void CreateOrUpdateStyle(
        string assetPath,
        string framePath,
        string fillPath,
        string backPath,
        Vector2 worldSize,
        Vector3 worldOffset,
        Vector2 fillInset,
        float framePixelsPerUnitMultiplier)
    {
        EnemyHealthBarStyle style = AssetDatabase.LoadAssetAtPath<EnemyHealthBarStyle>(assetPath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<EnemyHealthBarStyle>();
            AssetDatabase.CreateAsset(style, assetPath);
        }

        style.frame = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
        style.fill = AssetDatabase.LoadAssetAtPath<Sprite>(fillPath);
        style.back = AssetDatabase.LoadAssetAtPath<Sprite>(backPath);
        style.worldBarSize = worldSize;
        style.worldOffset = worldOffset;
        style.fillInsetPixels = fillInset;
        style.framePixelsPerUnitMultiplier = framePixelsPerUnitMultiplier;
        EditorUtility.SetDirty(style);
    }

    private static GameObject CreateOrUpdateBarPrefab(
        string prefabPath,
        string rootName,
        EnemyHealthBarStyle style,
        int sortingOrder)
    {
        if (!AssetDatabase.IsValidFolder(UiPrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        GameObject tempRoot = new GameObject("TempEnemyHealthBarBuilder");
        tempRoot.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            EnemyBarView view = EnemyBarView.Create(tempRoot.transform, rootName);
            ConfigureBarCanvas(view.Root.gameObject, sortingOrder);
            if (style != null)
            {
                view.ApplyStyle(style);
                float ppu = 100f;
                Vector2 pixelSize = new Vector2(
                    Mathf.Max(1f, style.worldBarSize.x * ppu),
                    Mathf.Max(1f, style.worldBarSize.y * ppu));
                view.LayoutContents(pixelSize, style.fillInsetPixels);
                view.Root.localScale = Vector3.one / ppu;
                view.Root.localPosition = style.worldOffset;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(view.Root.gameObject, prefabPath);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(tempRoot);
        }
    }

    private static void ConfigureBarCanvas(GameObject root, int sortingOrder)
    {
        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas == null)
            canvas = root.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        canvas.worldCamera = null;
    }

    private static void AssignBarToCharacterPrefab(
        string characterPrefabPath,
        EnemyHealthBarStyle styleAsset,
        string resourcePath,
        GameObject barPrefab)
    {
        GameObject character = PrefabUtility.LoadPrefabContents(characterPrefabPath);
        try
        {
            MonsterHealthBar bar = character.GetComponent<MonsterHealthBar>()
                ?? character.GetComponentInChildren<MonsterHealthBar>(true);
            if (bar == null)
            {
                Debug.LogWarning("[SetupEnemyHealthBarSprites] No MonsterHealthBar on " + characterPrefabPath);
                return;
            }

            RemoveExistingBarChild(character.transform);
            RectTransform nestedRoot = null;
            if (barPrefab != null)
            {
                GameObject nested = (GameObject)PrefabUtility.InstantiatePrefab(barPrefab, character.transform);
                nested.name = "MonsterHealthBar";
                nestedRoot = nested.GetComponent<RectTransform>();
                if (styleAsset != null && nestedRoot != null)
                {
                    float ppu = 100f;
                    nestedRoot.localPosition = styleAsset.worldOffset;
                    nestedRoot.localRotation = Quaternion.identity;
                    nestedRoot.localScale = Vector3.one / ppu;
                }
            }

            SerializedObject so = new SerializedObject(bar);
            so.FindProperty("style").objectReferenceValue = styleAsset;
            SerializedProperty pathProp = so.FindProperty("styleResourcePath");
            if (pathProp != null)
                pathProp.stringValue = resourcePath;
            SerializedProperty barRootProp = so.FindProperty("barRoot");
            if (barRootProp != null)
                barRootProp.objectReferenceValue = nestedRoot;
            SerializedProperty barPrefabProp = so.FindProperty("barPrefab");
            if (barPrefabProp != null)
                barPrefabProp.objectReferenceValue = barPrefab;
            if (styleAsset != null)
            {
                so.FindProperty("worldOffset").vector3Value = styleAsset.worldOffset;
                so.FindProperty("barSize").vector2Value = styleAsset.worldBarSize;
            }

            SerializedProperty hideFull = so.FindProperty("hideWhenFull");
            if (hideFull != null && bar is BossHealthBar)
                hideFull.boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(character, characterPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(character);
        }
    }

    private static void RemoveExistingBarChild(Transform character)
    {
        for (int i = character.childCount - 1; i >= 0; i--)
        {
            Transform child = character.GetChild(i);
            if (child == null)
                continue;

            bool isBar = child.name == "MonsterHealthBar"
                || child.name == "GoblinHealthBar"
                || child.name == "DragonHealthBar"
                || child.GetComponent<Canvas>() != null && EnemyBarView.HasExpectedParts(child as RectTransform);
            if (isBar)
                Object.DestroyImmediate(child.gameObject);
        }
    }
}
