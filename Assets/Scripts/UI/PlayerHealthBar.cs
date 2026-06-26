using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player HUD health bar built from the Kenney RPG UI bar sprites.
/// </summary>
public sealed class PlayerHealthBar : MonoBehaviour
{
    private const string RootName = "PlayerHealthBarRoot";
    private const string BackName = "Back";
    private const string FillMaskName = "FillMask";
    private const string FillName = "Fill";
    private const string LeftName = "Left";
    private const string MidName = "Mid";
    private const string RightName = "Right";
    private const string ResourceRoot = "UI/HealthBar/";

    [Header("Target")]
    [SerializeField] private Player player;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -24f);
    [SerializeField] private Vector2 barSize = new Vector2(280f, 28f);
    [SerializeField] private float capWidth = 14f;
    [SerializeField] private int sortingOrder = 100;

    [Header("Display")]
    [SerializeField] private float smoothSpeed = 14f;

    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private GraphicRaycaster graphicRaycaster;
    private RectTransform rootRect;
    private RectTransform fillMaskRect;
    private RectTransform backMidRect;
    private RectTransform fillMidRect;
    private Image backLeftImage;
    private Image backMidImage;
    private Image backRightImage;
    private Image fillLeftImage;
    private Image fillMidImage;
    private Image fillRightImage;
    private float displayedHealth01 = 1f;
    private bool usingGreenFill;

    private void Start()
    {
        EnsureUi();
        UpdateImmediate();
        PlayerExperienceBar.EnsureForHud(this);
        LevelUpNotice.EnsureForHud(this);
    }

    private void OnValidate()
    {
        barSize.x = Mathf.Max(1f, barSize.x);
        barSize.y = Mathf.Max(1f, barSize.y);
        capWidth = Mathf.Clamp(capWidth, 1f, barSize.x * 0.45f);
        smoothSpeed = Mathf.Max(0f, smoothSpeed);
    }

    private void LateUpdate()
    {
        EnsureUi();

        float target01 = GetHealth01();
        float t = smoothSpeed <= 0f ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        displayedHealth01 = Mathf.Lerp(displayedHealth01, target01, t);

        UpdateFill(displayedHealth01);
    }

    private void EnsureUi()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler == null)
            canvasScaler = gameObject.AddComponent<CanvasScaler>();

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        graphicRaycaster = GetComponent<GraphicRaycaster>();
        if (graphicRaycaster == null)
            graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

        if (rootRect == null)
        {
            Transform existingRoot = transform.Find(RootName);
            rootRect = existingRoot as RectTransform;
        }

        if (rootRect != null && !HasExpectedBarParts())
            DestroyExistingRoot();

        if (rootRect == null)
            CreateBar();

        CacheBarParts();
        ApplyLayout();
        ApplySprites();
    }

    private bool HasExpectedBarParts()
    {
        return rootRect.Find(BackName + "/" + LeftName) != null &&
            rootRect.Find(BackName + "/" + MidName) != null &&
            rootRect.Find(BackName + "/" + RightName) != null &&
            rootRect.Find(FillMaskName + "/" + FillName + "/" + LeftName) != null &&
            rootRect.Find(FillMaskName + "/" + FillName + "/" + MidName) != null &&
            rootRect.Find(FillMaskName + "/" + FillName + "/" + RightName) != null;
    }

    private void DestroyExistingRoot()
    {
        GameObject oldRoot = rootRect.gameObject;
        rootRect = null;
        fillMaskRect = null;

        if (Application.isPlaying)
            Destroy(oldRoot);
        else
            DestroyImmediate(oldRoot);
    }

    private void CreateBar()
    {
        GameObject root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(transform, false);
        rootRect = root.GetComponent<RectTransform>();

        GameObject back = new GameObject(BackName, typeof(RectTransform));
        back.transform.SetParent(root.transform, false);
        CreateSegment(back.transform, LeftName);
        CreateSegment(back.transform, MidName);
        CreateSegment(back.transform, RightName);

        GameObject fillMask = new GameObject(FillMaskName, typeof(RectTransform), typeof(RectMask2D));
        fillMask.transform.SetParent(root.transform, false);
        fillMaskRect = fillMask.GetComponent<RectTransform>();

        GameObject fill = new GameObject(FillName, typeof(RectTransform));
        fill.transform.SetParent(fillMask.transform, false);
        CreateSegment(fill.transform, LeftName);
        CreateSegment(fill.transform, MidName);
        CreateSegment(fill.transform, RightName);
    }

    private static Image CreateSegment(Transform parent, string name)
    {
        GameObject segment = new GameObject(name, typeof(RectTransform), typeof(Image));
        segment.transform.SetParent(parent, false);
        Image image = segment.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void CacheBarParts()
    {
        if (rootRect == null)
            return;

        Transform back = rootRect.Find(BackName);
        Transform fillMask = rootRect.Find(FillMaskName);
        Transform fill = fillMask != null ? fillMask.Find(FillName) : null;

        fillMaskRect = fillMask as RectTransform;

        backLeftImage = GetSegment(back, LeftName);
        backMidImage = GetSegment(back, MidName);
        backRightImage = GetSegment(back, RightName);
        fillLeftImage = GetSegment(fill, LeftName);
        fillMidImage = GetSegment(fill, MidName);
        fillRightImage = GetSegment(fill, RightName);

        backMidRect = backMidImage != null ? backMidImage.rectTransform : null;
        fillMidRect = fillMidImage != null ? fillMidImage.rectTransform : null;
    }

    private static Image GetSegment(Transform parent, string name)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private void ApplyLayout()
    {
        if (rootRect == null)
            return;

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = barSize;

        LayoutGroup(rootRect.Find(BackName) as RectTransform, barSize.x);

        if (fillMaskRect != null)
        {
            fillMaskRect.anchorMin = new Vector2(0f, 0f);
            fillMaskRect.anchorMax = new Vector2(0f, 1f);
            fillMaskRect.pivot = new Vector2(0f, 0.5f);
            fillMaskRect.anchoredPosition = Vector2.zero;
            fillMaskRect.sizeDelta = new Vector2(barSize.x, 0f);
        }

        LayoutGroup(fillMaskRect != null ? fillMaskRect.Find(FillName) as RectTransform : null, barSize.x);
    }

    private void LayoutGroup(RectTransform groupRect, float width)
    {
        if (groupRect == null)
            return;

        StretchToParent(groupRect);
        LayoutSegment(groupRect.Find(LeftName) as RectTransform, 0f, 0f, capWidth);
        LayoutSegment(groupRect.Find(MidName) as RectTransform, capWidth, 0f, Mathf.Max(0f, width - capWidth * 2f));
        LayoutSegment(groupRect.Find(RightName) as RectTransform, width - capWidth, 0f, capWidth);
    }

    private void LayoutSegment(RectTransform rect, float x, float y, float width)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 0f);
    }

    private void ApplySprites()
    {
        ApplyImage(backLeftImage, "barBack_horizontalLeft");
        ApplyImage(backMidImage, "barBack_horizontalMid");
        ApplyImage(backRightImage, "barBack_horizontalRight");
        ApplyFillSprites(displayedHealth01 >= 0.999f);
    }

    private void ApplyFillSprites(bool useGreen)
    {
        if (useGreen == usingGreenFill && fillLeftImage != null && fillLeftImage.sprite != null)
            return;

        string colorPrefix = useGreen ? "barGreen" : "barRed";
        ApplyImage(fillLeftImage, colorPrefix + "_horizontalLeft", true);
        ApplyImage(fillMidImage, colorPrefix + "_horizontalMid", true);
        ApplyImage(fillRightImage, colorPrefix + "_horizontalRight", true);
        usingGreenFill = useGreen;
    }

    private static void ApplyImage(Image image, string resourceName)
    {
        ApplyImage(image, resourceName, false);
    }

    private static void ApplyImage(Image image, string resourceName, bool forceSprite)
    {
        if (image == null)
            return;

        if (forceSprite || image.sprite == null)
            image.sprite = LoadSprite(resourceName);

        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static Sprite LoadSprite(string resourceName)
    {
        Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + resourceName);
        if (texture == null)
            return null;

        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        return Sprite.Create(texture, rect, pivot, 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
    }

    private void UpdateImmediate()
    {
        displayedHealth01 = GetHealth01();
        UpdateFill(displayedHealth01);
    }

    private float GetHealth01()
    {
        if (player == null || player.MaxHp <= 0)
            return 0f;

        return Mathf.Clamp01((float)player.CurrentHp / player.MaxHp);
    }

    private void UpdateFill(float health01)
    {
        if (fillMaskRect == null)
            return;

        float clampedHealth = Mathf.Clamp01(health01);
        ApplyFillSprites(clampedHealth >= 0.5f);
        fillMaskRect.sizeDelta = new Vector2(barSize.x * clampedHealth, 0f);
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
