using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 根据 Prefabs/Weapon 下的两块盾牌预制体生成 ItemSO。
/// </summary>
public static class CreateShieldItems
{
    private const string ItemFolder = "Assets/DataSO";
    private const string IconFolder = "Assets/Art/Items/Shields";
    private const string PolyartPrefabPath = "Assets/Prefabs/Weapon/Shield05Polyart Variant.prefab";
    private const string PbrPrefabPath = "Assets/Prefabs/Weapon/Shield05PBR Variant.prefab";

    [MenuItem("Tools/Items/Create Shield ItemSOs")]
    public static void CreateAll()
    {
        EnsureFolder(ItemFolder);
        EnsureFolder(IconFolder);

        GameObject polyartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PolyartPrefabPath);
        GameObject pbrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PbrPrefabPath);
        if (polyartPrefab == null || pbrPrefab == null)
        {
            Debug.LogError("CreateShieldItems: missing shield prefabs in Prefabs/Weapon.");
            return;
        }

        Sprite polyartIcon = RenderPrefabIcon(
            polyartPrefab,
            IconFolder + "/ui_shield_wood.png",
            new Vector3(-6f, 16f, 0f),
            new Color(1f, 0.95f, 0.82f),
            1.15f);
        Sprite pbrIcon = RenderPrefabIcon(
            pbrPrefab,
            IconFolder + "/ui_shield_steel.png",
            new Vector3(-10f, -18f, 0f),
            new Color(0.82f, 0.9f, 1f),
            1.4f);

        ItemSO wood = CreateOrLoadItem(ItemFolder + "/Shield_Wood.asset");
        wood.id = 6;
        wood.name = "轻木鸢盾";
        wood.itemType = ItemType.Shield;
        wood.description = "轻便的鸢形木盾，能提供基础格挡耐久。";
        wood.propertyList = new System.Collections.Generic.List<ItemProperty>
        {
            new ItemProperty { PropertyType = ItemPropertyType.ShieldDurability, Value = 80 }
        };
        wood.icon = polyartIcon;
        wood.prefab = polyartPrefab;
        EditorUtility.SetDirty(wood);

        ItemSO steel = CreateOrLoadItem(ItemFolder + "/Shield_Steel.asset");
        steel.id = 7;
        steel.name = "精钢鸢盾";
        steel.itemType = ItemType.Shield;
        steel.description = "精铁打造的鸢形盾，格挡耐久更高。";
        steel.propertyList = new System.Collections.Generic.List<ItemProperty>
        {
            new ItemProperty { PropertyType = ItemPropertyType.ShieldDurability, Value = 120 }
        };
        steel.icon = pbrIcon;
        steel.prefab = pbrPrefab;
        EditorUtility.SetDirty(steel);

        BuildItemCatalog.Rebuild();
        Debug.Log("CreateShieldItems: created Shield_Wood (id 6) and Shield_Steel (id 7).");
    }

    private static ItemSO CreateOrLoadItem(string path)
    {
        ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
        if (item != null)
            return item;

        item = ScriptableObject.CreateInstance<ItemSO>();
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    private static Sprite RenderPrefabIcon(
        GameObject prefab,
        string pngPath,
        Vector3 euler,
        Color keyLightColor,
        float keyLightIntensity)
    {
        Texture2D texture = CapturePrefabPreview(prefab, 256, euler, keyLightColor, keyLightIntensity);
        if (texture == null)
        {
            texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0.62f, 0.52f, 0.32f, 1f);
            texture.SetPixels(pixels);
            texture.Apply();
        }

        File.WriteAllBytes(pngPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(pngPath);

        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
    }

    private static Texture2D CapturePrefabPreview(
        GameObject prefab,
        int size,
        Vector3 euler,
        Color keyLightColor,
        float keyLightIntensity)
    {
        const int previewLayer = 31;
        Color previousAmbient = RenderSettings.ambientLight;
        GameObject instance = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture renderTexture = null;

        try
        {
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.52f);
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = new Vector3(8000f, 8000f, 8000f);
            instance.transform.rotation = Quaternion.Euler(euler);

            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                transforms[i].gameObject.layer = previewLayer;

            Bounds bounds = CalculateBounds(instance);
            renderTexture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8
            };

            cameraObject = new GameObject("ShieldIconCam");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * 1.12f;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.cullingMask = 1 << previewLayer;
            camera.targetTexture = renderTexture;

            lightObject = new GameObject("ShieldIconLight");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = keyLightIntensity;
            light.color = keyLightColor;
            light.cullingMask = 1 << previewLayer;
            light.transform.rotation = Quaternion.Euler(32f, 25f, 0f);

            Vector3 direction = new Vector3(0.18f, 0.12f, -1f).normalized;
            camera.transform.position = bounds.center - direction * 4f;
            camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            return result;
        }
        finally
        {
            RenderSettings.ambientLight = previousAmbient;
            if (renderTexture != null)
                Object.DestroyImmediate(renderTexture);
            if (cameraObject != null)
                Object.DestroyImmediate(cameraObject);
            if (lightObject != null)
                Object.DestroyImmediate(lightObject);
            if (instance != null)
                Object.DestroyImmediate(instance);
        }
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string leaf = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
