using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家 HUD 右下角半透明操作提示：鼠标攻击/闪避，空格跳跃，Shift 加速，I 背包。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerMouseHint : MonoBehaviour
{
    public const string RootName = "PlayerMouseHintRoot";
    private const string MouseResourcePath = "UI/Hud/ui_mouse_hint";
    private const string ShiftResourcePath = "UI/Hud/ui_key_shift";
    private const string SpaceResourcePath = "UI/Hud/ui_key_space";
    private const string InventoryResourcePath = "UI/Hud/ui_key_i";

    private static readonly Color CaptionColor = new Color(0.92f, 0.86f, 0.70f, 0.72f);
    private static readonly Color LabelColor = new Color(0.98f, 0.93f, 0.78f, 0.88f);
    private static readonly Color OutlineColor = new Color(0.07f, 0.05f, 0.03f, 0.82f);
    private static readonly Color IconTint = new Color(1f, 1f, 1f, 0.86f);

    [Header("Sprites")]
    [SerializeField] private Sprite mouseSprite;
    [SerializeField] private Sprite shiftSprite;
    [SerializeField] private Sprite spaceSprite;
    [SerializeField] private Sprite inventorySprite;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-16f, 18f);
    [SerializeField] private Vector2 rootSize = new Vector2(292f, 260f);
    [SerializeField] private Vector2 mouseSize = new Vector2(92f, 128f);
    [SerializeField] private Vector2 shiftSize = new Vector2(78f, 36f);
    [SerializeField] private Vector2 spaceSize = new Vector2(128f, 36f);
    [SerializeField] private Vector2 inventorySize = new Vector2(42f, 42f);

    private RectTransform rootRect;
    private CanvasGroup canvasGroup;
    private Image mouseImage;
    private Image shiftImage;
    private Image spaceImage;
    private Image inventoryImage;
    private TextMeshProUGUI attackCaption;
    private TextMeshProUGUI attackLabel;
    private TextMeshProUGUI dodgeCaption;
    private TextMeshProUGUI dodgeLabel;
    private TextMeshProUGUI shiftLabel;
    private TextMeshProUGUI spaceLabel;
    private TextMeshProUGUI inventoryLabel;

    private void Awake()
    {
        EnsureUi();
    }

    private void OnEnable()
    {
        EnsureUi();
    }

    public void EnsureUi()
    {
        Transform existing = transform.Find(RootName);
        GameObject root = existing != null ? existing.gameObject : new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
        if (existing == null)
            root.transform.SetParent(transform, false);

        rootRect = root.GetComponent<RectTransform>();
        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = root.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        mouseImage = GetOrCreateImage(root.transform, "Mouse");
        shiftImage = GetOrCreateImage(root.transform, "KeyShift");
        spaceImage = GetOrCreateImage(root.transform, "KeySpace");
        inventoryImage = GetOrCreateImage(root.transform, "KeyInventory");

        attackCaption = GetOrCreateLabel(root.transform, "AttackCaption", "左键", 15f, CaptionColor, TextAlignmentOptions.Center);
        attackLabel = GetOrCreateLabel(root.transform, "AttackLabel", "攻击", 22f, LabelColor, TextAlignmentOptions.Center);
        dodgeCaption = GetOrCreateLabel(root.transform, "DodgeCaption", "右键", 15f, CaptionColor, TextAlignmentOptions.Center);
        dodgeLabel = GetOrCreateLabel(root.transform, "DodgeLabel", "闪避", 22f, LabelColor, TextAlignmentOptions.Center);
        shiftLabel = GetOrCreateLabel(root.transform, "LabelShift", "加速", 16f, LabelColor, TextAlignmentOptions.Center);
        spaceLabel = GetOrCreateLabel(root.transform, "LabelSpace", "跳跃", 16f, LabelColor, TextAlignmentOptions.Center);
        inventoryLabel = GetOrCreateLabel(root.transform, "LabelInventory", "背包", 16f, LabelColor, TextAlignmentOptions.Center);

        ApplyLayout();
        ApplySprites();
    }

    private void ApplyLayout()
    {
        if (rootRect == null)
            return;

        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = rootSize;
        rootRect.localScale = Vector3.one;

        PlaceImage(mouseImage, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 68f), mouseSize);
        PlaceImage(shiftImage, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 28f), shiftSize);
        PlaceImage(spaceImage, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 28f), spaceSize);
        PlaceImage(inventoryImage, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(234f, 26f), inventorySize);

        PlaceLabel(attackCaption, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-48f, -8f), new Vector2(80f, 20f));
        PlaceLabel(attackLabel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-48f, -26f), new Vector2(80f, 28f));
        PlaceLabel(dodgeCaption, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(48f, -8f), new Vector2(80f, 20f));
        PlaceLabel(dodgeLabel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(48f, -26f), new Vector2(80f, 28f));
        PlaceLabel(shiftLabel, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(49f, 6f), new Vector2(78f, 20f));
        PlaceLabel(spaceLabel, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 6f), new Vector2(128f, 20f));
        PlaceLabel(inventoryLabel, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(255f, 6f), new Vector2(50f, 20f));
    }

    private void ApplySprites()
    {
        ApplySprite(mouseImage, ref mouseSprite, MouseResourcePath);
        ApplySprite(shiftImage, ref shiftSprite, ShiftResourcePath);
        ApplySprite(spaceImage, ref spaceSprite, SpaceResourcePath);
        ApplySprite(inventoryImage, ref inventorySprite, InventoryResourcePath);
    }

    private static void ApplySprite(Image image, ref Sprite sprite, string resourcePath)
    {
        if (image == null)
            return;

        if (sprite == null)
            sprite = Resources.Load<Sprite>(resourcePath);

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = IconTint;
        image.raycastTarget = false;
        image.preserveAspect = true;
    }

    private static Image GetOrCreateImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null)
            go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static TextMeshProUGUI GetOrCreateLabel(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        if (existing == null)
            go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.outlineWidth = 0.26f;
        label.outlineColor = OutlineColor;
        ChineseUITmpFont.Apply(label, fontSize, FontStyles.Bold);
        return label;
    }

    private static void PlaceImage(Image image, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        if (image == null)
            return;

        PlaceRect(image.rectTransform, anchor, pivot, anchoredPosition, size);
    }

    private static void PlaceLabel(TextMeshProUGUI label, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        if (label == null)
            return;

        PlaceRect(label.rectTransform, anchor, pivot, anchoredPosition, size);
    }

    private static void PlaceRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }
}
