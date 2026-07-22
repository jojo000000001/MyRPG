using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player HUD health bar built from the Kenney RPG UI bar sprites.
/// </summary>
public sealed class PlayerHealthBar : MonoBehaviour
{
    private const string RootName = "PlayerHealthBarRoot";
    private const string TrackPlateName = "TrackPlate";
    private const string BackName = "Back";
    private const string FillMaskName = "FillMask";
    private const string FillName = "Fill";
    private const string LeftName = "Left";
    private const string MidName = "Mid";
    private const string RightName = "Right";

    [Header("Target")]
    [SerializeField] private Player player;
    [SerializeField] private HealthBarSpriteCatalog spriteCatalog;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -24f);
    [SerializeField] private Vector2 barSize = new Vector2(280f, 28f);
    [SerializeField] private float capWidth = 14f;
    [SerializeField] private float fillInset = 5f;
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
    private bool uiBuilt;
    private bool spritesReady;

    internal HealthBarSpriteCatalog SpriteCatalog => spriteCatalog;

    private void Awake()
    {
        ResolveCatalogReference();
        HealthBarSprites.BindCatalog(spriteCatalog);
    }

    private void Start()
    {
        ResolvePlayer();
        ResolveCatalogReference();
        HealthBarSprites.BindCatalog(spriteCatalog);
        ClearLegacyRoots();
        uiBuilt = false;
        EnsureUi();
        UpdateImmediate();
        ApplySprites();
        PlayerExperienceBar.EnsureForHud(this);
        LevelUpNotice.EnsureForHud(this);
        GameplayPauseMenu.EnsureForHud(transform);

        if (player != null)
            player.StatsChanged += HandleStatsChanged;
    }

    private void OnEnable()
    {
        if (!uiBuilt)
            return;

        CacheBarParts();
        ApplySprites();
        UpdateImmediate();
    }

    private void OnDestroy()
    {
        if (player != null)
            player.StatsChanged -= HandleStatsChanged;
    }

    private void HandleStatsChanged()
    {
        if (!uiBuilt || rootRect == null || !HasExpectedBarParts())
        {
            uiBuilt = false;
            EnsureUi();
        }

        UpdateImmediate();
        ApplySprites();
    }

    private void ResolvePlayer()
    {
        if (player == null)
            player = FindObjectOfType<Player>();
    }

    private void ResolveCatalogReference()
    {
        if (spriteCatalog != null)
            return;

        spriteCatalog = Resources.Load<HealthBarSpriteCatalog>("HealthBarSpriteCatalog");
    }

    private void OnValidate()
    {
        barSize.x = Mathf.Max(1f, barSize.x);
        barSize.y = Mathf.Max(1f, barSize.y);
        capWidth = Mathf.Clamp(capWidth, 1f, barSize.x * 0.45f);
        fillInset = Mathf.Clamp(fillInset, 0f, barSize.y * 0.45f);
        smoothSpeed = Mathf.Max(0f, smoothSpeed);
    }

    private void LateUpdate()
    {
        ResolvePlayer();
        if (player == null)
            return;

        if (!uiBuilt || rootRect == null || !HasExpectedBarParts())
            EnsureUi();

        float target01 = GetHealth01();
        float t = smoothSpeed <= 0f ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        displayedHealth01 = Mathf.Lerp(displayedHealth01, target01, t);

        if (!spritesReady && HealthBarSprites.HasCatalog)
        {
            ApplySprites();
            spritesReady = true;
        }

        UpdateFill(displayedHealth01);
    }

    private void EnsureUi()
    {
        if (uiBuilt && rootRect != null && HasExpectedBarParts())
            return;

        EnsureCanvasReady();

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
            ClearLegacyRoots();

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
        uiBuilt = rootRect != null && HasExpectedBarParts();

        if (!HealthBarSprites.HasCatalog)
            Debug.LogWarning("PlayerHealthBar: Kenney bar sprites are missing. Run Tools/MyRPG/Setup Health Bar Catalog.", this);
    }

    private void EnsureCanvasReady()
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null && rectTransform.localScale.sqrMagnitude < 0.0001f)
            rectTransform.localScale = Vector3.one;

        Canvas rootCanvas = GetComponent<Canvas>();
        if (rootCanvas != null)
            rootCanvas.enabled = true;
    }

    private void ClearLegacyRoots()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != RootName)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        rootRect = null;
        fillMaskRect = null;
        uiBuilt = false;
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
        uiBuilt = false;

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

        GameObject plate = new GameObject(TrackPlateName, typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(root.transform, false);
        plate.transform.SetAsFirstSibling();
        Image plateImage = plate.GetComponent<Image>();
        plateImage.raycastTarget = false;
        plateImage.color = HealthBarSprites.TrackPlateColor;

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

        RectTransform plateRect = rootRect.Find(TrackPlateName) as RectTransform;
        if (plateRect != null)
            StretchToParent(plateRect);

        LayoutGroup(rootRect.Find(BackName) as RectTransform, barSize.x);

        if (fillMaskRect != null)
        {
            fillMaskRect.anchorMin = new Vector2(0f, 0f);
            fillMaskRect.anchorMax = new Vector2(0f, 1f);
            fillMaskRect.pivot = new Vector2(0f, 0.5f);
            fillMaskRect.anchoredPosition = new Vector2(fillInset, 0f);
            fillMaskRect.sizeDelta = new Vector2(Mathf.Max(0f, barSize.x - fillInset * 2f), 0f);
        }

        float innerWidth = Mathf.Max(0f, barSize.x - fillInset * 2f);
        LayoutGroup(fillMaskRect != null ? fillMaskRect.Find(FillName) as RectTransform : null, innerWidth);
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
        ApplyBackSprites();
        ApplyFillSprites(displayedHealth01 >= 0.5f);
    }

    private void ApplyBackSprites()
    {
        HealthBarSprites.ApplyBackBarImage(backLeftImage, HealthBarSprites.GetBack("Left"), false);
        HealthBarSprites.ApplyBackBarImage(backMidImage, HealthBarSprites.GetBack("Mid"), true);
        HealthBarSprites.ApplyBackBarImage(backRightImage, HealthBarSprites.GetBack("Right"), false);
    }

    private void ApplyFillSprites(bool useGreen)
    {
        Color fallback = useGreen
            ? new Color(0.22f, 0.82f, 0.30f, 0.96f)
            : new Color(0.95f, 0.12f, 0.08f, 0.96f);

        if (useGreen == usingGreenFill && fillLeftImage != null && fillLeftImage.sprite != null)
            return;

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
        float innerWidth = Mathf.Max(0f, barSize.x - fillInset * 2f);
        fillMaskRect.sizeDelta = new Vector2(innerWidth * clampedHealth, 0f);
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
