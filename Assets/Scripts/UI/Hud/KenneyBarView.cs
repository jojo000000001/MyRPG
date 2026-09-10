using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kenney 三段血条（可选底槽 + 绿/红填充）。玩家 HUD 与怪物头顶共用同一套结构。
/// </summary>
public sealed class KenneyBarView
{
    public const string TrackPlateName = "TrackPlate";
    public const string BackName = "Back";
    public const string FillMaskName = "FillMask";
    public const string FillName = "Fill";
    public const string LeftName = "Left";
    public const string MidName = "Mid";
    public const string RightName = "Right";

    public RectTransform Root { get; private set; }
    public RectTransform FillMask { get; private set; }
    public bool ShowBackTrack { get; private set; }

    public bool IsReady =>
        Root != null &&
        FillMask != null &&
        fillLeftImage != null &&
        fillMidImage != null &&
        fillRightImage != null;

    private Image backLeftImage;
    private Image backMidImage;
    private Image backRightImage;
    private Image fillLeftImage;
    private Image fillMidImage;
    private Image fillRightImage;
    private bool usingGreenFill = true;
    private bool fillSpritesBound;

    public static bool HasExpectedParts(RectTransform root, bool showBackTrack)
    {
        if (root == null)
            return false;

        bool fillOk = root.Find(FillMaskName + "/" + FillName + "/" + LeftName) != null &&
            root.Find(FillMaskName + "/" + FillName + "/" + MidName) != null &&
            root.Find(FillMaskName + "/" + FillName + "/" + RightName) != null;

        if (!showBackTrack)
            return fillOk;

        return fillOk &&
            root.Find(BackName + "/" + LeftName) != null &&
            root.Find(BackName + "/" + MidName) != null &&
            root.Find(BackName + "/" + RightName) != null;
    }

    public static KenneyBarView Create(Transform parent, string rootName, bool showBackTrack, bool useTrackPlate)
    {
        GameObject rootObject = new GameObject(rootName, typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);

        KenneyBarView view = new KenneyBarView
        {
            Root = rootObject.GetComponent<RectTransform>(),
            ShowBackTrack = showBackTrack
        };
        view.BuildChildren(useTrackPlate);
        view.CacheParts();
        return view;
    }

    public static KenneyBarView Bind(RectTransform root, bool showBackTrack)
    {
        KenneyBarView view = new KenneyBarView
        {
            Root = root,
            ShowBackTrack = showBackTrack
        };
        view.CacheParts();
        return view;
    }

    public void LayoutContents(Vector2 barSize, float capWidth, float fillInset)
    {
        if (Root == null)
            return;

        float clampedCap = Mathf.Clamp(capWidth, 1f, barSize.x * 0.45f);
        Root.sizeDelta = barSize;

        if (Root.Find(TrackPlateName) is RectTransform plateRect)
            StretchToParent(plateRect);

        if (ShowBackTrack)
            LayoutGroup(Root.Find(BackName) as RectTransform, barSize.x, clampedCap);

        if (FillMask != null)
        {
            FillMask.anchorMin = new Vector2(0f, 0f);
            FillMask.anchorMax = new Vector2(0f, 1f);
            FillMask.pivot = new Vector2(0f, 0.5f);
            FillMask.anchoredPosition = new Vector2(fillInset, 0f);
            FillMask.sizeDelta = new Vector2(Mathf.Max(0f, barSize.x - fillInset * 2f), 0f);
        }

        float innerWidth = Mathf.Max(0f, barSize.x - fillInset * 2f);
        LayoutGroup(FillMask != null ? FillMask.Find(FillName) as RectTransform : null, innerWidth, clampedCap);
    }

    public void ApplySprites(bool useGreenFill)
    {
        ApplyBackSprites();
        ApplyFillSprites(useGreenFill, force: true);
    }

    public void SetFill01(float health01, float barWidth, float fillInset)
    {
        if (FillMask == null)
            return;

        float clampedHealth = Mathf.Clamp01(health01);
        ApplyFillSprites(clampedHealth >= 0.5f, force: false);
        float innerWidth = Mathf.Max(0f, barWidth - fillInset * 2f);
        FillMask.sizeDelta = new Vector2(innerWidth * clampedHealth, 0f);
    }

    private void BuildChildren(bool useTrackPlate)
    {
        if (useTrackPlate)
        {
            GameObject plate = new GameObject(TrackPlateName, typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(Root, false);
            plate.transform.SetAsFirstSibling();
            Image plateImage = plate.GetComponent<Image>();
            plateImage.raycastTarget = false;
            plateImage.color = HealthBarSprites.TrackPlateColor;
        }

        if (ShowBackTrack)
        {
            GameObject back = new GameObject(BackName, typeof(RectTransform));
            back.transform.SetParent(Root, false);
            CreateSegment(back.transform, LeftName);
            CreateSegment(back.transform, MidName);
            CreateSegment(back.transform, RightName);
        }

        GameObject fillMask = new GameObject(FillMaskName, typeof(RectTransform), typeof(RectMask2D));
        fillMask.transform.SetParent(Root, false);
        FillMask = fillMask.GetComponent<RectTransform>();

        GameObject fill = new GameObject(FillName, typeof(RectTransform));
        fill.transform.SetParent(fillMask.transform, false);
        CreateSegment(fill.transform, LeftName);
        CreateSegment(fill.transform, MidName);
        CreateSegment(fill.transform, RightName);
    }

    private void CacheParts()
    {
        if (Root == null)
            return;

        Transform back = Root.Find(BackName);
        Transform fillMask = Root.Find(FillMaskName);
        Transform fill = fillMask != null ? fillMask.Find(FillName) : null;

        FillMask = fillMask as RectTransform;
        backLeftImage = GetSegment(back, LeftName);
        backMidImage = GetSegment(back, MidName);
        backRightImage = GetSegment(back, RightName);
        fillLeftImage = GetSegment(fill, LeftName);
        fillMidImage = GetSegment(fill, MidName);
        fillRightImage = GetSegment(fill, RightName);
    }

    private void ApplyBackSprites()
    {
        if (!ShowBackTrack)
            return;

        HealthBarSprites.ApplyBackBarImage(backLeftImage, HealthBarSprites.GetBack("Left"), false);
        HealthBarSprites.ApplyBackBarImage(backMidImage, HealthBarSprites.GetBack("Mid"), true);
        HealthBarSprites.ApplyBackBarImage(backRightImage, HealthBarSprites.GetBack("Right"), false);
    }

    private void ApplyFillSprites(bool useGreen, bool force)
    {
        if (!force && useGreen == usingGreenFill && fillSpritesBound)
            return;

        Color fallback = useGreen ? HudBarVisualStyle.GreenFillTint : HudBarVisualStyle.RedFillTint;

        if (useGreen)
        {
            HealthBarSprites.ApplyBarImage(fillLeftImage, HealthBarSprites.GetGreen("Left"), false, fallback);
            HealthBarSprites.ApplyBarImage(fillMidImage, HealthBarSprites.GetGreen("Mid"), true, fallback);
            HealthBarSprites.ApplyBarImage(fillRightImage, HealthBarSprites.GetGreen("Right"), false, fallback);
        }
        else
        {
            HealthBarSprites.ApplyBarImage(fillLeftImage, HealthBarSprites.GetRed("Left"), false, fallback);
            HealthBarSprites.ApplyBarImage(fillMidImage, HealthBarSprites.GetRed("Mid"), true, fallback);
            HealthBarSprites.ApplyBarImage(fillRightImage, HealthBarSprites.GetRed("Right"), false, fallback);
        }

        usingGreenFill = useGreen;
        fillSpritesBound = fillLeftImage != null && fillLeftImage.sprite != null;
    }

    private static Image CreateSegment(Transform parent, string name)
    {
        GameObject segment = new GameObject(name, typeof(RectTransform), typeof(Image));
        segment.transform.SetParent(parent, false);
        Image image = segment.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static Image GetSegment(Transform parent, string name)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static void LayoutGroup(RectTransform groupRect, float width, float capWidth)
    {
        if (groupRect == null)
            return;

        StretchToParent(groupRect);
        LayoutSegment(groupRect.Find(LeftName) as RectTransform, 0f, 0f, capWidth);
        LayoutSegment(groupRect.Find(MidName) as RectTransform, capWidth, 0f, Mathf.Max(0f, width - capWidth * 2f));
        LayoutSegment(groupRect.Find(RightName) as RectTransform, width - capWidth, 0f, capWidth);
    }

    private static void LayoutSegment(RectTransform rect, float x, float y, float width)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 0f);
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
