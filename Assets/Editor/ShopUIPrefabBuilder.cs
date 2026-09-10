using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店预制体：左侧长商品格，右侧说明，格子底透明。
/// </summary>
public static class ShopUIPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/ShopUIRoot.prefab";
    private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerHUD.prefab";
    private const string PanelSpritePath = "Assets/Art/UI/GeneratedShop/ui_shop_panel_sheikah.png";
    private const string SlotSpritePath = "Assets/Art/UI/GeneratedShop/ui_shop_slot_sheikah.png";

    private const int Columns = 1;
    private const int Rows = 6;
    private static readonly Vector2 PanelSize = new Vector2(1200f, 680f);
    private static readonly Vector2 SlotSize = new Vector2(528f, 76f);
    private static readonly Vector2 SlotSpacing = new Vector2(0f, 8f);

    private static readonly Color Orange = new Color(1f, 0.55f, 0.14f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.93f, 0.86f, 1f);
    private static readonly Color MutedColor = new Color(0.82f, 0.84f, 0.78f, 1f);
    private static readonly Color RupeeColor = new Color(0.38f, 0.86f, 0.42f, 1f);
    private static readonly Color InactiveTab = new Color(0.10f, 0.14f, 0.18f, 0.55f);
    private static readonly Color ButtonColor = new Color(0.78f, 0.42f, 0.08f, 1f);

    [MenuItem("Tools/MyRPG/Rebuild Shop UI Prefab")]
    public static void RebuildShopPrefab()
    {
        GameObject prefab = BuildPrefab();
        InstallSpawnerOnPlayerHud(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static GameObject BuildPrefab()
    {
        EnsureFolder("Assets/Prefabs/UI");

        Sprite panelSprite = LoadSprite(PanelSpritePath);
        Sprite slotSprite = LoadSprite(SlotSpritePath);

        GameObject root = new GameObject("ShopUIRoot", typeof(RectTransform), typeof(ShopUI));
        Stretch(root.GetComponent<RectTransform>());

        SerializedObject ui = new SerializedObject(root.GetComponent<ShopUI>());
        ui.FindProperty("panelSprite").objectReferenceValue = panelSprite;
        ui.FindProperty("slotSprite").objectReferenceValue = slotSprite;
        ui.FindProperty("columns").intValue = Columns;
        ui.FindProperty("rows").intValue = Rows;
        ui.FindProperty("panelSize").vector2Value = PanelSize;
        ui.FindProperty("slotSize").vector2Value = SlotSize;
        ui.FindProperty("slotSpacing").vector2Value = SlotSpacing;
        ui.ApplyModifiedPropertiesWithoutUndo();

        GameObject panel = CreateChild(root, "ShopPanel", typeof(RectTransform));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = PanelSize;

        Image fill = CreateChild(panel, "Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Stretch(fill.rectTransform);
        fill.sprite = null;
        fill.type = Image.Type.Simple;
        fill.color = new Color(0.20f, 0.25f, 0.32f, 0.78f);
        fill.raycastTarget = true;

        Image frame = CreateChild(panel, "Frame", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Stretch(frame.rectTransform);
        ConfigureImage(frame, panelSprite, Image.Type.Sliced, Color.white, 1.2f);
        frame.raycastTarget = false;

        TextMeshProUGUI title = CreateText(panel, "Title", "商店", 36f, TextAlignmentOptions.Left, FontStyles.Bold, Orange);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(48f, -28f);
        titleRect.sizeDelta = new Vector2(280f, 44f);

        TextMeshProUGUI rupees = CreateText(panel, "Rupees", "◆  0 卢比", 24f, TextAlignmentOptions.Right, FontStyles.Bold, RupeeColor);
        RectTransform rupeeRect = rupees.rectTransform;
        rupeeRect.anchorMin = new Vector2(1f, 1f);
        rupeeRect.anchorMax = new Vector2(1f, 1f);
        rupeeRect.pivot = new Vector2(1f, 1f);
        rupeeRect.anchoredPosition = new Vector2(-48f, -28f);
        rupeeRect.sizeDelta = new Vector2(280f, 40f);

        GameObject list = CreateChild(panel, "ItemList", typeof(RectTransform));
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0.52f, 1f);
        listRect.offsetMin = new Vector2(40f, 40f);
        listRect.offsetMax = new Vector2(-10f, -88f);

        Button buyTab = CreateTextButton(list, "BuyTab", "购买", slotSprite, new Vector2(120f, 40f), Orange, 5f);
        Place(buyTab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(120f, 40f));
        buyTab.GetComponentInChildren<TextMeshProUGUI>().color = Color.black;

        Button sellTab = CreateTextButton(list, "SellTab", "出售", slotSprite, new Vector2(120f, 40f), InactiveTab, 5f);
        Place(sellTab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(132f, 0f), new Vector2(120f, 40f));

        CreateSlotGrid(list, slotSprite);
        CreatePreview(panel, slotSprite);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    public static void InstallSpawnerOnPlayerHud(GameObject shopPrefab)
    {
        if (shopPrefab == null)
            return;

        GameObject hudRoot = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            Transform nested = hudRoot.transform.Find("ShopUIRoot");
            if (nested != null)
                Object.DestroyImmediate(nested.gameObject);

            Transform oldHost = hudRoot.transform.Find("ShopUI");
            if (oldHost != null)
                Object.DestroyImmediate(oldHost.gameObject);

            ShopUIRuntimeSpawner spawner = hudRoot.GetComponent<ShopUIRuntimeSpawner>();
            if (spawner == null)
                spawner = hudRoot.AddComponent<ShopUIRuntimeSpawner>();

            SerializedObject spawnerObject = new SerializedObject(spawner);
            spawnerObject.FindProperty("shopUIPrefab").objectReferenceValue = shopPrefab.GetComponent<ShopUI>();
            spawnerObject.FindProperty("spawnOnAwake").boolValue = true;
            spawnerObject.ApplyModifiedPropertiesWithoutUndo();

            PlayerHUD hud = hudRoot.GetComponent<PlayerHUD>();
            SerializedObject hudObject = new SerializedObject(hud);
            hudObject.FindProperty("shopUI").objectReferenceValue = null;
            SerializedProperty spawnerProperty = hudObject.FindProperty("shopSpawner");
            if (spawnerProperty != null)
                spawnerProperty.objectReferenceValue = spawner;
            hudObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(hudRoot, HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hudRoot);
        }
    }

    private static void CreateSlotGrid(GameObject parent, Sprite slotSprite)
    {
        GameObject grid = CreateChild(parent, "SlotGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 0f);
        gridRect.anchorMax = new Vector2(1f, 1f);
        gridRect.offsetMin = Vector2.zero;
        gridRect.offsetMax = new Vector2(0f, -52f);

        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = SlotSize;
        layout.spacing = SlotSpacing;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Columns;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Vertical;

        int slotCount = Columns * Rows;
        for (int i = 0; i < slotCount; i++)
            CreateSlot(grid, i, slotSprite);
    }

    private static void CreateSlot(GameObject parent, int index, Sprite slotSprite)
    {
        GameObject slot = CreateChild(parent, "Slot_" + (index + 1).ToString("00"), typeof(RectTransform), typeof(Image), typeof(ShopSlotUI));
        ConfigureImage(slot.GetComponent<Image>(), slotSprite, Image.Type.Sliced, Color.white, 5.5f);

        Image mark = CreateChild(slot, "SelectedMark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransform markRect = mark.rectTransform;
        markRect.anchorMin = new Vector2(0f, 0.18f);
        markRect.anchorMax = new Vector2(0f, 0.82f);
        markRect.pivot = new Vector2(0f, 0.5f);
        markRect.anchoredPosition = new Vector2(6f, 0f);
        markRect.sizeDelta = new Vector2(6f, 0f);
        mark.color = Orange;
        mark.raycastTarget = false;
        mark.enabled = false;

        Image icon = CreateChild(slot, "Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(18f, 0f);
        iconRect.sizeDelta = new Vector2(52f, 52f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI name = CreateText(slot, "Name", string.Empty, 22f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, TextColor);
        RectTransform nameRect = name.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(80f, 8f);
        nameRect.offsetMax = new Vector2(-108f, -8f);

        TextMeshProUGUI count = CreateText(slot, "Count", string.Empty, 20f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, RupeeColor);
        RectTransform countRect = count.rectTransform;
        countRect.anchorMin = new Vector2(1f, 0f);
        countRect.anchorMax = new Vector2(1f, 1f);
        countRect.pivot = new Vector2(1f, 0.5f);
        countRect.anchoredPosition = new Vector2(-18f, 0f);
        countRect.sizeDelta = new Vector2(96f, 0f);
        count.raycastTarget = false;
    }

    private static void CreatePreview(GameObject panel, Sprite slotSprite)
    {
        GameObject preview = CreateChild(panel, "Preview", typeof(RectTransform), typeof(Image));
        RectTransform previewRect = preview.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.52f, 0f);
        previewRect.anchorMax = new Vector2(1f, 1f);
        previewRect.offsetMin = new Vector2(10f, 40f);
        previewRect.offsetMax = new Vector2(-40f, -88f);
        ConfigureImage(preview.GetComponent<Image>(), slotSprite, Image.Type.Sliced, Color.white, 1.8f);

        Image icon = CreateChild(preview, "PreviewIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -28f);
        iconRect.sizeDelta = new Vector2(168f, 168f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = new Color(1f, 1f, 1f, 0.12f);

        TextMeshProUGUI name = CreateText(preview, "PreviewName", "选中货物", 30f, TextAlignmentOptions.Center, FontStyles.Bold, TextColor);
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -204f), new Vector2(-32f, 40f));

        TextMeshProUGUI type = CreateText(preview, "PreviewType", "消耗品", 20f, TextAlignmentOptions.Center, FontStyles.Normal, MutedColor);
        Place(type.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -242f), new Vector2(-32f, 28f));

        TextMeshProUGUI detail = CreateText(preview, "DetailText", "选中左侧货物后，这里显示说明。", 20f, TextAlignmentOptions.TopLeft, FontStyles.Normal, TextColor);
        RectTransform detailRect = detail.rectTransform;
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(28f, 168f);
        detailRect.offsetMax = new Vector2(-28f, -278f);
        detail.color = MutedColor;
        detail.enableWordWrapping = true;

        TextMeshProUGUI price = CreateText(preview, "Price", "◆  0", 28f, TextAlignmentOptions.Center, FontStyles.Bold, RupeeColor);
        Place(price.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(-24f, 36f));

        Button qtyMinus = CreateTextButton(preview, "QtyMinus", "−", slotSprite, new Vector2(44f, 40f), ButtonColor, 6f);
        Place(qtyMinus.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-70f, 72f), new Vector2(44f, 40f));
        Button qtyPlus = CreateTextButton(preview, "QtyPlus", "+", slotSprite, new Vector2(44f, 40f), ButtonColor, 6f);
        Place(qtyPlus.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(70f, 72f), new Vector2(44f, 40f));

        TextMeshProUGUI quantity = CreateText(preview, "Quantity", "1", 24f, TextAlignmentOptions.Center, FontStyles.Bold, TextColor);
        Place(quantity.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(56f, 40f));

        Button action = CreateTextButton(preview, "ActionButton", "购买", slotSprite, new Vector2(168f, 44f), Orange, 5.5f);
        Place(action.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 18f), new Vector2(168f, 44f));
        action.GetComponentInChildren<TextMeshProUGUI>().color = Color.black;

        Button back = CreateTextButton(preview, "BackButton", "返回", slotSprite, new Vector2(168f, 44f), InactiveTab, 5.5f);
        Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 18f), new Vector2(168f, 44f));
    }

    private static Button CreateTextButton(GameObject parent, string name, string label, Sprite sprite, Vector2 size, Color color, float pixelsPerUnitMultiplier)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.GetComponent<RectTransform>().sizeDelta = size;
        ConfigureImage(buttonObject.GetComponent<Image>(), sprite, Image.Type.Sliced, color, pixelsPerUnitMultiplier);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        TextMeshProUGUI text = CreateText(buttonObject, "Label", label, 22f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private static TextMeshProUGUI CreateText(
        GameObject parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style,
        Color color)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        ChineseUITmpFont.Apply(label, fontSize, style);
        return label;
    }

    private static GameObject CreateChild(GameObject parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static void ConfigureImage(Image image, Sprite sprite, Image.Type type, Color color, float pixelsPerUnitMultiplier)
    {
        image.sprite = sprite;
        image.type = sprite != null ? type : Image.Type.Simple;
        image.fillCenter = true;
        image.color = color;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
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
}
