using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 希卡石板风格：深蓝灰底板 + 橙金框，与商店 UI 共用素材。
/// </summary>
public static class SheikahUiStyle
{
    public static readonly Color Fill = new Color(0.20f, 0.25f, 0.32f, 0.78f);
    public static readonly Color Orange = new Color(1f, 0.55f, 0.14f, 1f);
    public static readonly Color Text = new Color(0.95f, 0.93f, 0.86f, 1f);
    public static readonly Color Muted = new Color(0.82f, 0.84f, 0.78f, 1f);
    public static readonly Color Inactive = new Color(0.10f, 0.14f, 0.18f, 0.55f);
    public static readonly Color Button = new Color(0.78f, 0.42f, 0.08f, 1f);
    public static readonly Color Dim = new Color(0.02f, 0.05f, 0.08f, 0.55f);
    public static readonly Color Danger = new Color(0.72f, 0.22f, 0.12f, 0.92f);

    public const string CatalogResourcePath = "SheikahUiSprites";
    public const string EditorPanelPath = "Assets/Art/UI/GeneratedShop/ui_shop_panel_sheikah.png";
    public const string EditorSlotPath = "Assets/Art/UI/GeneratedShop/ui_shop_slot_sheikah.png";

    private static Sprite registeredPanel;
    private static Sprite registeredSlot;
    private static SheikahUiCatalog cachedCatalog;

    public static Sprite PanelSprite => ResolveSprite(ref registeredPanel, catalog => catalog != null ? catalog.panelSprite : null, EditorPanelPath, "ui_shop_panel_sheikah");
    public static Sprite SlotSprite => ResolveSprite(ref registeredSlot, catalog => catalog != null ? catalog.slotSprite : null, EditorSlotPath, "ui_shop_slot_sheikah");

    public static void Register(Sprite panelSprite, Sprite slotSprite)
    {
        if (panelSprite != null)
            registeredPanel = panelSprite;
        if (slotSprite != null)
            registeredSlot = slotSprite;
    }

    public static GameObject CreateCard(Transform parent, string name, Vector2 size, Sprite panelSprite = null, float pixelsPerUnitMultiplier = 2.4f)
    {
        GameObject card = CreateChild(parent, name);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image fill = CreateChild(card.transform, "Background", typeof(Image)).GetComponent<Image>();
        Stretch(fill.rectTransform);
        fill.sprite = null;
        fill.type = Image.Type.Simple;
        fill.color = Fill;
        fill.raycastTarget = true;

        Image frame = CreateChild(card.transform, "Frame", typeof(Image)).GetComponent<Image>();
        Stretch(frame.rectTransform);
        ApplySliced(frame, panelSprite != null ? panelSprite : PanelSprite, Color.white, pixelsPerUnitMultiplier);
        frame.raycastTarget = false;

        return card;
    }

    public static void EnsureCardChrome(Transform card, float pixelsPerUnitMultiplier = 2.4f)
    {
        if (card == null)
            return;

        Image rootImage = card.GetComponent<Image>();
        if (rootImage != null)
            rootImage.enabled = false;

        Transform background = card.Find("Background");
        if (background == null)
        {
            Image fill = CreateChild(card, "Background", typeof(Image)).GetComponent<Image>();
            Stretch(fill.rectTransform);
            fill.sprite = null;
            fill.type = Image.Type.Simple;
            fill.color = Fill;
            fill.raycastTarget = true;
            fill.transform.SetAsFirstSibling();
        }

        Transform frame = card.Find("Frame");
        if (frame == null)
        {
            Image frameImage = CreateChild(card, "Frame", typeof(Image)).GetComponent<Image>();
            Stretch(frameImage.rectTransform);
            ApplySliced(frameImage, PanelSprite, Color.white, pixelsPerUnitMultiplier);
            frameImage.raycastTarget = false;
            frameImage.transform.SetSiblingIndex(1);
        }
    }

    public static void PlaceCentered(RectTransform rect, float anchorY, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    public static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 size,
        Color tint,
        Color labelColor,
        Sprite slotSprite = null,
        float pixelsPerUnitMultiplier = 5.5f)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(Image), typeof(Button));
        buttonObject.GetComponent<RectTransform>().sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        ApplySliced(image, slotSprite != null ? slotSprite : SlotSprite, tint, pixelsPerUnitMultiplier);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 22f, TextAlignmentOptions.Center, FontStyles.Bold, labelColor);
        Stretch(text.rectTransform);
        return button;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style,
        Color color)
    {
        GameObject textObject = CreateChild(parent, name, typeof(TextMeshProUGUI));
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        ChineseUITmpFont.Apply(label, fontSize, style);
        return label;
    }

    public static void ApplySliced(Image image, Sprite sprite, Color color, float pixelsPerUnitMultiplier)
    {
        if (image == null)
            return;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        }
        else if (image.sprite != null)
        {
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        }

        image.color = color;
    }

    public static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
            legacy.text = label;
    }

    public static void SetButtonLabelColor(Button button, Color color)
    {
        if (button == null)
            return;

        TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.color = color;
            return;
        }

        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
            legacy.color = color;
    }

    public static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        if (components != null && components.Length > 0)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != typeof(RectTransform))
                    child.AddComponent(components[i]);
            }
        }

        child.transform.SetParent(parent, false);
        return child;
    }

    public static void Stretch(RectTransform rect, float horizontalPadding = 0f, float verticalPadding = 0f)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        rect.localScale = Vector3.one;
    }

    private static Sprite ResolveSprite(
        ref Sprite registered,
        System.Func<SheikahUiCatalog, Sprite> fromCatalog,
        string editorPath,
        string spriteName)
    {
        if (registered != null)
            return registered;

        SheikahUiCatalog catalog = LoadCatalog();
        Sprite catalogSprite = fromCatalog(catalog);
        if (catalogSprite != null)
        {
            registered = catalogSprite;
            return registered;
        }

        Sprite resourceSprite = Resources.Load<Sprite>(spriteName);
        if (resourceSprite != null)
        {
            registered = resourceSprite;
            return registered;
        }

#if UNITY_EDITOR
        Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorPath);
        if (editorSprite != null)
        {
            registered = editorSprite;
            return registered;
        }
#endif

        registered = FindLoadedSprite(spriteName);
        return registered;
    }

    private static SheikahUiCatalog LoadCatalog()
    {
        if (cachedCatalog == null)
        {
            cachedCatalog = Resources.Load<SheikahUiCatalog>(CatalogResourcePath);
            if (cachedCatalog == null)
                cachedCatalog = Resources.Load<SheikahUiCatalog>("SheikahUiCatalog");
        }
        return cachedCatalog;
    }

    private static Sprite FindLoadedSprite(string spriteName)
    {
        Image[] images = Object.FindObjectsOfType<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Sprite sprite = images[i] != null ? images[i].sprite : null;
            if (sprite != null && sprite.name == spriteName)
                return sprite;
        }

        return null;
    }
}
