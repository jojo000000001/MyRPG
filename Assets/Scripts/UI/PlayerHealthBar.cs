using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家左上角 HUD 血条：运行时自动创建屏幕空间 Canvas，并跟随绑定的 Player 血量平滑更新。
/// </summary>
public sealed class PlayerHealthBar : MonoBehaviour
{
    private const string RootName = "PlayerHealthBarRoot";
    private const string FillAreaName = "FillArea";
    private const string FillName = "Fill";

    [Header("Target")]
    [SerializeField] private Player player;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -24f);
    [SerializeField] private Vector2 barSize = new Vector2(280f, 28f);
    [SerializeField] private float borderPixels = 3f;
    [SerializeField] private int sortingOrder = 100;

    [Header("Display")]
    [SerializeField] private float smoothSpeed = 14f;

    [Header("Color")]
    [SerializeField] private Color frameColor = new Color(0.03f, 0.025f, 0.02f, 0.92f);
    [SerializeField] private Color backgroundColor = new Color(0.18f, 0.035f, 0.03f, 0.88f);
    [SerializeField] private Color highHealthColor = new Color(0.22f, 0.82f, 0.30f, 0.96f);
    [SerializeField] private Color lowHealthColor = new Color(0.92f, 0.12f, 0.08f, 0.96f);

    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private GraphicRaycaster graphicRaycaster;
    private RectTransform rootRect;
    private RectTransform fillAreaRect;
    private RectTransform fillRect;
    private Image rootImage;
    private Image fillAreaImage;
    private Image fillImage;
    private float displayedHealth01 = 1f;

    private void Start()
    {
        EnsureUi();
        UpdateImmediate();
    }

    private void OnValidate()
    {
        barSize.x = Mathf.Max(1f, barSize.x);
        barSize.y = Mathf.Max(1f, barSize.y);
        borderPixels = Mathf.Max(0f, borderPixels);
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
            if (rootRect != null)
                rootImage = rootRect.GetComponent<Image>();
        }

        if (rootRect == null)
            CreateBar();

        CacheBarParts();
        ApplyLayout();
    }

    private void CreateBar()
    {
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(transform, false);
        rootRect = root.GetComponent<RectTransform>();
        rootImage = root.GetComponent<Image>();

        GameObject fillArea = new GameObject(FillAreaName, typeof(RectTransform), typeof(Image));
        fillArea.transform.SetParent(root.transform, false);
        fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaImage = fillArea.GetComponent<Image>();

        GameObject fill = new GameObject(FillName, typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fillRect = fill.GetComponent<RectTransform>();
        fillImage = fill.GetComponent<Image>();

        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
    }

    private void CacheBarParts()
    {
        if (rootRect == null)
            return;

        rootImage = rootRect.GetComponent<Image>();

        if (fillAreaRect == null)
        {
            Transform fillArea = rootRect.Find(FillAreaName);
            fillAreaRect = fillArea as RectTransform;
        }

        if (fillAreaRect != null)
        {
            fillAreaImage = fillAreaRect.GetComponent<Image>();

            if (fillRect == null)
            {
                Transform fill = fillAreaRect.Find(FillName);
                fillRect = fill as RectTransform;
            }
        }

        if (fillRect != null)
            fillImage = fillRect.GetComponent<Image>();
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

        if (rootImage != null)
            rootImage.color = frameColor;

        if (fillAreaRect != null)
        {
            StretchToParent(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(borderPixels, borderPixels);
            fillAreaRect.offsetMax = new Vector2(-borderPixels, -borderPixels);
        }

        if (fillAreaImage != null)
            fillAreaImage.color = backgroundColor;
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
        if (fillAreaRect == null || fillRect == null || fillImage == null)
            return;

        float width = Mathf.Max(0f, fillAreaRect.rect.width);
        float clampedHealth = Mathf.Clamp01(health01);
        fillRect.sizeDelta = new Vector2(width * clampedHealth, 0f);
        fillImage.color = Color.Lerp(lowHealthColor, highHealthColor, clampedHealth);
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


