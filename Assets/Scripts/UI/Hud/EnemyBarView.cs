using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 怪物头顶血条：底槽 + 遮罩填充 + 外框。哥布林/巨龙用各自的风格图，不跟玩家 Kenney 条混用。
/// </summary>
public sealed class EnemyBarView
{
    public const string BackName = "Back";
    public const string FillMaskName = "FillMask";
    public const string FillName = "Fill";
    public const string FrameName = "Frame";

    public RectTransform Root { get; private set; }
    public RectTransform FillMask { get; private set; }

    public bool IsReady =>
        Root != null &&
        FillMask != null &&
        fillImage != null &&
        frameImage != null;

    private Image backImage;
    private Image fillImage;
    private Image frameImage;
    private Vector2 fillInsetPixels;
    private float fullFillWidth;

    public static bool HasExpectedParts(RectTransform root)
    {
        if (root == null)
            return false;

        return root.Find(FrameName) != null &&
            root.Find(FillMaskName + "/" + FillName) != null &&
            root.Find(BackName) != null;
    }

    public static EnemyBarView Create(Transform parent, string rootName)
    {
        GameObject rootObject = new GameObject(rootName, typeof(RectTransform), typeof(Canvas));
        rootObject.transform.SetParent(parent, false);

        EnemyBarView view = new EnemyBarView
        {
            Root = rootObject.GetComponent<RectTransform>()
        };
        view.BuildChildren();
        view.CacheParts();
        return view;
    }

    public static EnemyBarView Bind(RectTransform root)
    {
        EnemyBarView view = new EnemyBarView { Root = root };
        view.CacheParts();
        return view;
    }

    public void ApplyStyle(EnemyHealthBarStyle style)
    {
        if (style == null)
            return;

        fillInsetPixels = style.fillInsetPixels;
        ApplyImage(backImage, style.back, sliced: false, 1f);
        ApplyImage(fillImage, style.fill, sliced: false, 1f);
        // Frame art already includes end-caps. Slicing stretches those caps to the full
        // bar height and makes the bone/horn ornaments look like giant orbs.
        ApplyImage(frameImage, style.frame, sliced: false, 1f);
        if (fillImage != null)
            fillImage.color = Color.white;
    }

    public void LayoutContents(Vector2 pixelSize, Vector2 fillInset)
    {
        if (Root == null)
            return;

        fillInsetPixels = fillInset;
        Root.sizeDelta = pixelSize;

        float insetX = Mathf.Clamp(fillInset.x, 0f, pixelSize.x * 0.45f);
        float insetY = Mathf.Clamp(fillInset.y, 0f, pixelSize.y * 0.45f);
        float innerWidth = Mathf.Max(0f, pixelSize.x - insetX * 2f);
        float innerHeight = Mathf.Max(1f, pixelSize.y - insetY * 2f);

        LayoutTrack(Root.Find(BackName) as RectTransform, insetX, innerWidth, innerHeight);
        LayoutLeftBar(FillMask, insetX, innerWidth, innerHeight);

        RectTransform fillRect = FillMask != null ? FillMask.Find(FillName) as RectTransform : null;
        if (fillRect != null)
            StretchToParent(fillRect);

        if (Root.Find(FrameName) is RectTransform frameRect)
            StretchToParent(frameRect);

        CaptureFillWidth();
    }

    public void CaptureFillWidth()
    {
        if (FillMask == null)
            return;

        float width = Mathf.Abs(FillMask.rect.width);
        if (width <= 0.01f)
            width = Mathf.Abs(FillMask.sizeDelta.x);
        fullFillWidth = Mathf.Max(0.01f, width);
    }

    public void SetFill01(float health01)
    {
        if (FillMask == null)
            return;

        if (fullFillWidth <= 0.01f)
            CaptureFillWidth();

        FillMask.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            fullFillWidth * Mathf.Clamp01(health01));
    }

    private void BuildChildren()
    {
        CreateImageChild(Root, BackName);
        GameObject fillMask = new GameObject(FillMaskName, typeof(RectTransform), typeof(RectMask2D));
        fillMask.transform.SetParent(Root, false);
        FillMask = fillMask.GetComponent<RectTransform>();
        CreateImageChild(FillMask, FillName);
        CreateImageChild(Root, FrameName).transform.SetAsLastSibling();
    }

    private void CacheParts()
    {
        if (Root == null)
            return;

        Transform fillMask = Root.Find(FillMaskName);
        FillMask = fillMask as RectTransform;
        backImage = GetImage(Root, BackName);
        fillImage = fillMask != null ? GetImage(fillMask, FillName) : null;
        frameImage = GetImage(Root, FrameName);
    }

    private static GameObject CreateImageChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = false;
        return child;
    }

    private static Image GetImage(Transform parent, string name)
    {
        Transform child = parent != null ? parent.Find(name) : null;
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static void ApplyImage(Image image, Sprite sprite, bool sliced, float pixelsPerUnitMultiplier = 1f)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, pixelsPerUnitMultiplier);
        image.color = Color.white;
        image.raycastTarget = false;
        image.enabled = true;
        image.material = null;
        image.preserveAspect = false;
    }

    private static void LayoutTrack(RectTransform rect, float insetX, float innerWidth, float innerHeight)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(insetX, 0f);
        rect.sizeDelta = new Vector2(innerWidth, innerHeight);
    }

    private static void LayoutLeftBar(RectTransform rect, float insetX, float innerWidth, float innerHeight)
    {
        LayoutTrack(rect, insetX, innerWidth, innerHeight);
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
