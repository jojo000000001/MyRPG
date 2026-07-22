using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左上角经验条：显示等级、当前/升级所需经验与进度条。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerExperienceBar : MonoBehaviour
{
    private const string RootName = "PlayerExperienceBarRoot";

    [Header("Target")]
    [SerializeField] private Player player;
    [SerializeField] private HealthBarSpriteCatalog spriteCatalog;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -60f);
    [SerializeField] private Vector2 barSize = new Vector2(280f, 18f);
    [SerializeField] private float capWidth = 12f;
    [SerializeField] private float fillInset = 2f;
    [SerializeField] private float labelHeight = 20f;
    [SerializeField] private float labelBarGap = 4f;
    [SerializeField] private float smoothSpeed = 12f;

    [Header("Colors")]
    [SerializeField] private Color labelColor = new Color(0.95f, 0.92f, 0.78f, 1f);
    [SerializeField] private Color expTextColor = new Color(0.78f, 0.86f, 0.95f, 1f);

    private RectTransform rootRect;
    private RectTransform fillMaskRect;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI expText;
    private float displayedProgress01;
    private bool spritesReady;

    public static void EnsureForHud(PlayerHealthBar healthBar)
    {
        if (healthBar == null)
            return;

        PlayerExperienceBar experienceBar = healthBar.GetComponent<PlayerExperienceBar>();
        if (experienceBar == null)
            experienceBar = healthBar.gameObject.AddComponent<PlayerExperienceBar>();

        experienceBar.AdoptCatalog(healthBar.SpriteCatalog);
    }

    internal void AdoptCatalog(HealthBarSpriteCatalog sharedCatalog)
    {
        if (sharedCatalog != null)
            spriteCatalog = sharedCatalog;

        ResolveCatalogReference();
        HealthBarSprites.BindCatalog(spriteCatalog);

        if (rootRect != null)
            RefreshBarSprites();
    }

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
        EnsureUi();
        UpdateImmediate();

        if (player != null)
            player.StatsChanged += HandleStatsChanged;
    }

    private void OnDestroy()
    {
        if (player != null)
            player.StatsChanged -= HandleStatsChanged;
    }

    private void HandleStatsChanged()
    {
        UpdateImmediate();
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

    private void LateUpdate()
    {
        ResolvePlayer();
        if (player == null)
            return;

        if (rootRect == null)
            EnsureUi();

        if (rootRect == null)
            return;

        float target01 = player.ExperienceProgress01;
        float t = smoothSpeed <= 0f ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        displayedProgress01 = Mathf.Lerp(displayedProgress01, target01, t);

        if (!spritesReady && HealthBarSprites.HasCatalog)
        {
            RefreshBarSprites();
            spritesReady = true;
        }

        UpdateFill(displayedProgress01);
        UpdateLabels();
    }

    private void EnsureUi()
    {
        Transform existing = transform.Find(RootName);
        if (existing != null)
        {
            rootRect = existing as RectTransform;
            CacheParts();
            if (HasExpectedParts())
            {
                EnsureTrackPlate();
                ApplyLayout();
                RefreshBarSprites();
                return;
            }

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);

            rootRect = null;
            fillMaskRect = null;
            levelText = null;
            expText = null;
        }

        GameObject root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(transform, false);
        rootRect = root.GetComponent<RectTransform>();

        GameObject levelObject = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        levelObject.transform.SetParent(root.transform, false);
        levelText = levelObject.GetComponent<TextMeshProUGUI>();
        ConfigureLabel(levelText, TextAlignmentOptions.Left, 18f);
        levelText.color = labelColor;

        GameObject expObject = new GameObject("ExpText", typeof(RectTransform), typeof(TextMeshProUGUI));
        expObject.transform.SetParent(root.transform, false);
        expText = expObject.GetComponent<TextMeshProUGUI>();
        ConfigureLabel(expText, TextAlignmentOptions.Right, 16f);
        expText.color = expTextColor;

        CreateTrackPlate(root.transform);
        CreateBar(root.transform, "Back", BarFillKind.None);
        GameObject fillMask = new GameObject("FillMask", typeof(RectTransform), typeof(RectMask2D));
        fillMask.transform.SetParent(root.transform, false);
        fillMaskRect = fillMask.GetComponent<RectTransform>();
        CreateBar(fillMask.transform, "Fill", BarFillKind.Blue);

        CacheParts();
        ApplyLayout();
        RefreshBarSprites();
    }

    private void RefreshBarSprites()
    {
        ApplySpritesToGroup(rootRect != null ? rootRect.Find("Back") : null, BarFillKind.None);
        if (fillMaskRect != null)
            ApplySpritesToGroup(fillMaskRect.Find("Fill"), BarFillKind.Blue);
    }

    private void ApplySpritesToGroup(Transform group, BarFillKind fillKind)
    {
        if (group == null)
            return;

        ApplySpriteToSegment(group.Find("Left"), "Left", fillKind);
        ApplySpriteToSegment(group.Find("Mid"), "Mid", fillKind);
        ApplySpriteToSegment(group.Find("Right"), "Right", fillKind);
    }

    private void ApplySpriteToSegment(Transform segment, string name, BarFillKind fillKind)
    {
        if (segment == null)
            return;

        Image image = segment.GetComponent<Image>();
        if (image == null)
            return;

        Sprite sprite = fillKind switch
        {
            BarFillKind.Blue => HealthBarSprites.GetBlue(name),
            _ => HealthBarSprites.GetBack(name),
        };

        if (fillKind == BarFillKind.Blue)
        {
            HealthBarSprites.ApplyBarImage(
                image,
                sprite,
                name == "Mid",
                new Color(0.35f, 0.65f, 1f, 1f));
            return;
        }

        HealthBarSprites.ApplyBackBarImage(image, sprite, name == "Mid");
    }

    private bool HasExpectedParts()
    {
        return rootRect != null &&
            rootRect.Find("LevelText") != null &&
            rootRect.Find("ExpText") != null &&
            rootRect.Find("Back") != null &&
            rootRect.Find("FillMask") != null;
    }

    private enum BarFillKind
    {
        None,
        Blue,
    }

    private void CreateBar(Transform parent, string groupName, BarFillKind fillKind)
    {
        GameObject group = new GameObject(groupName, typeof(RectTransform));
        group.transform.SetParent(parent, false);

        CreateSegment(group.transform, "Left", fillKind);
        CreateSegment(group.transform, "Mid", fillKind);
        CreateSegment(group.transform, "Right", fillKind);
    }

    private void CreateSegment(Transform parent, string name, BarFillKind fillKind)
    {
        GameObject segment = new GameObject(name, typeof(RectTransform), typeof(Image));
        segment.transform.SetParent(parent, false);
        Image image = segment.GetComponent<Image>();
        image.raycastTarget = false;

        Sprite sprite = fillKind switch
        {
            BarFillKind.Blue => HealthBarSprites.GetBlue(name),
            _ => HealthBarSprites.GetBack(name),
        };

        if (fillKind == BarFillKind.Blue)
        {
            HealthBarSprites.ApplyBarImage(
                image,
                sprite,
                name == "Mid",
                new Color(0.35f, 0.65f, 1f, 1f));
            return;
        }

        HealthBarSprites.ApplyBackBarImage(image, sprite, name == "Mid");
    }

    private static void CreateTrackPlate(Transform parent)
    {
        GameObject plate = new GameObject("TrackPlate", typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(parent, false);
        plate.transform.SetAsFirstSibling();
        Image plateImage = plate.GetComponent<Image>();
        plateImage.raycastTarget = false;
        plateImage.color = HealthBarSprites.TrackPlateColor;
    }

    private void EnsureTrackPlate()
    {
        if (rootRect == null)
            return;

        if (rootRect.Find("TrackPlate") != null)
            return;

        CreateTrackPlate(rootRect);
    }

    private void CacheParts()
    {
        if (rootRect == null)
            return;

        if (levelText == null)
            levelText = rootRect.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
        if (expText == null)
            expText = rootRect.Find("ExpText")?.GetComponent<TextMeshProUGUI>();
        if (fillMaskRect == null)
            fillMaskRect = rootRect.Find("FillMask") as RectTransform;
    }

    private void ApplyLayout()
    {
        if (rootRect == null || levelText == null || expText == null)
            return;

        EnsureTrackPlate();

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(barSize.x, labelHeight + labelBarGap + barSize.y);

        float barTop = -(labelHeight + labelBarGap);

        RectTransform levelRect = levelText.rectTransform;
        levelRect.anchorMin = new Vector2(0f, 1f);
        levelRect.anchorMax = new Vector2(0.5f, 1f);
        levelRect.pivot = new Vector2(0f, 1f);
        levelRect.anchoredPosition = new Vector2(0f, 0f);
        levelRect.sizeDelta = new Vector2(0f, labelHeight);

        RectTransform expRect = expText.rectTransform;
        expRect.anchorMin = new Vector2(0.5f, 1f);
        expRect.anchorMax = new Vector2(1f, 1f);
        expRect.pivot = new Vector2(1f, 1f);
        expRect.anchoredPosition = new Vector2(0f, 0f);
        expRect.sizeDelta = new Vector2(0f, labelHeight);

        LayoutBarGroupTop(rootRect.Find("TrackPlate") as RectTransform, barSize, barTop);
        LayoutBarGroupTop(rootRect.Find("Back") as RectTransform, barSize, barTop);
        if (fillMaskRect != null)
        {
            fillMaskRect.anchorMin = new Vector2(0f, 1f);
            fillMaskRect.anchorMax = new Vector2(0f, 1f);
            fillMaskRect.pivot = new Vector2(0f, 1f);
            fillMaskRect.anchoredPosition = new Vector2(fillInset, barTop);
            fillMaskRect.sizeDelta = new Vector2(Mathf.Max(0f, barSize.x - fillInset * 2f), barSize.y);
            LayoutBarGroupTop(
                fillMaskRect.Find("Fill") as RectTransform,
                new Vector2(Mathf.Max(0f, barSize.x - fillInset * 2f), barSize.y),
                0f);
        }
    }

    private void LayoutBarGroupTop(RectTransform groupRect, Vector2 size, float y)
    {
        if (groupRect == null)
            return;

        groupRect.anchorMin = new Vector2(0f, 1f);
        groupRect.anchorMax = new Vector2(0f, 1f);
        groupRect.pivot = new Vector2(0f, 1f);
        groupRect.anchoredPosition = new Vector2(0f, y);
        groupRect.sizeDelta = size;

        LayoutSegment(groupRect.Find("Left") as RectTransform, 0f, capWidth, size.y);
        LayoutSegment(groupRect.Find("Mid") as RectTransform, capWidth, Mathf.Max(0f, size.x - capWidth * 2f), size.y);
        LayoutSegment(groupRect.Find("Right") as RectTransform, size.x - capWidth, capWidth, size.y);
    }

    private static void LayoutSegment(RectTransform rect, float x, float width, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void UpdateImmediate()
    {
        displayedProgress01 = player != null ? player.ExperienceProgress01 : 0f;
        UpdateFill(displayedProgress01);
        UpdateLabels();
    }

    private void UpdateFill(float progress01)
    {
        if (fillMaskRect == null)
            return;

        float innerWidth = Mathf.Max(0f, barSize.x - fillInset * 2f);
        fillMaskRect.sizeDelta = new Vector2(innerWidth * Mathf.Clamp01(progress01), barSize.y);
    }

    private void UpdateLabels()
    {
        if (player == null)
            return;

        if (levelText != null)
            levelText.text = "Lv." + player.Level;

        if (expText != null)
            expText.text = player.Experience + " / " + player.ExperienceToNextLevel;
    }

    private void ConfigureLabel(TextMeshProUGUI label, TextAlignmentOptions alignment, float fontSize)
    {
        label.alignment = alignment;
        ChineseUITmpFont.Apply(label, fontSize, FontStyles.Bold);
        label.color = labelColor;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(20, 12, 8, 220);
    }

    private void OnValidate()
    {
        barSize.x = Mathf.Max(120f, barSize.x);
        barSize.y = Mathf.Max(10f, barSize.y);
        capWidth = Mathf.Clamp(capWidth, 1f, barSize.x * 0.45f);
    }
}
