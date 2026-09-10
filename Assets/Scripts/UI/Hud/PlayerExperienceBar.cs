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
    private const string BarAreaName = "BarArea";
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
    [SerializeField] private Vector2 anchoredPosition = HudBarVisualStyle.ComputeExpBarAnchoredPosition();
    [SerializeField] private Vector2 barSize = HudBarVisualStyle.BarSize;
    [SerializeField] private float capWidth = HudBarVisualStyle.CapWidth;
    [SerializeField] private float fillInset = HudBarVisualStyle.FillInset;
    [SerializeField] private float labelHeight = HudBarVisualStyle.LabelHeight;
    [SerializeField] private float labelBarGap = HudBarVisualStyle.LabelBarGap;
    [SerializeField] private float smoothSpeed = 12f;

    [Header("Colors")]
    [SerializeField] private Color labelColor = new Color(0.18f, 0.16f, 0.14f, 1f);
    [SerializeField] private Color expTextColor = HudBarVisualStyle.ValueTextColor;
    [SerializeField] private Color backBarTint = HudBarVisualStyle.BackBarTint;
    [SerializeField] private Color backBarFallback = HudBarVisualStyle.BackBarFallback;
    [SerializeField] private Color fillTint = HudBarVisualStyle.BlueFillTint;

    private const int ExpHudLayoutVersion = 7;
    [SerializeField] private int expHudLayoutVersion;

    private RectTransform rootRect;
    private RectTransform fillMaskRect;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI expText;
    private float displayedProgress01;
    private bool spritesReady;
    private bool prefabUi;

    internal void RebuildUi()
    {
        Transform existing = transform.Find(RootName);
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        rootRect = null;
        fillMaskRect = null;
        levelText = null;
        expText = null;
        EnsureUi();
    }

    internal void SyncLayoutFromHealthBar(PlayerHealthBar healthBar)
    {
        if (healthBar == null)
            return;

        barSize = healthBar.HudBarSize;
        capWidth = HudBarVisualStyle.CapWidth;
        fillInset = HudBarVisualStyle.FillInset;
        backBarTint = HudBarVisualStyle.BackBarTint;
        backBarFallback = HudBarVisualStyle.BackBarFallback;
        fillTint = HudBarVisualStyle.BlueFillTint;
        expHudLayoutVersion = ExpHudLayoutVersion;
        anchoredPosition = HudBarVisualStyle.ComputeExpBarAnchoredPosition(
            labelHeight,
            labelBarGap,
            healthBar.HudAnchoredPosition,
            healthBar.HudBarSize);

        if (rootRect != null)
        {
            ApplyLayout();
            RefreshBarSprites();
        }
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
        prefabUi = transform.Find(RootName) != null;
        ResolveCatalogReference();
        HealthBarSprites.BindCatalog(spriteCatalog);
    }

    private void Start()
    {
        ResolvePlayer();
        ResolveCatalogReference();
        HealthBarSprites.BindCatalog(spriteCatalog);

        PlayerHealthBar healthBar = GetComponent<PlayerHealthBar>();
        if (healthBar != null)
        {
            AdoptCatalog(healthBar.SpriteCatalog);
            if (!prefabUi)
                SyncLayoutFromHealthBar(healthBar);
        }

        expHudLayoutVersion = ExpHudLayoutVersion;
        if (!prefabUi && transform.Find(RootName) != null && NeedsLayoutRebuild())
            RebuildUi();
        else
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

    public void BindPlayer(Player target)
    {
        if (target != null)
            player = target;
    }

    private void ResolvePlayer()
    {
        if (player == null)
            player = Player.Resolve();
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
        if (prefabUi)
        {
            rootRect = transform.Find(RootName) as RectTransform;
            CacheParts();
            if (!HasExpectedParts())
            {
                Debug.LogError("PlayerExperienceBar: prefab UI is missing PlayerExperienceBarRoot hierarchy.", this);
                return;
            }

            ApplyLayout();
            RefreshBarSprites();
            return;
        }

        Transform existing = transform.Find(RootName);
        if (existing != null)
        {
            rootRect = existing as RectTransform;
            CacheParts();
            if (HasExpectedParts())
            {
                if (NeedsLayoutRebuild())
                {
                    if (Application.isPlaying)
                        Destroy(existing.gameObject);
                    else
                        DestroyImmediate(existing.gameObject);
                }
                else
                {
                    ApplyLayout();
                    RefreshBarSprites();
                    return;
                }
            }
            else
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

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

        GameObject barArea = new GameObject(BarAreaName, typeof(RectTransform));
        barArea.transform.SetParent(root.transform, false);

        CreateSegmentGroup(barArea.transform, BackName, BarFillKind.Back);

        GameObject fillMask = new GameObject(FillMaskName, typeof(RectTransform), typeof(RectMask2D));
        fillMask.transform.SetParent(barArea.transform, false);
        fillMask.transform.SetAsLastSibling();
        fillMaskRect = fillMask.GetComponent<RectTransform>();

        GameObject fill = new GameObject(FillName, typeof(RectTransform));
        fill.transform.SetParent(fillMask.transform, false);
        CreateSegmentGroup(fill.transform, null, BarFillKind.Blue);

        CacheParts();
        ApplyLayout();
        RefreshBarSprites();
    }

    private void CreateSegmentGroup(Transform parent, string groupName, BarFillKind fillKind)
    {
        Transform groupParent = parent;
        if (!string.IsNullOrEmpty(groupName))
        {
            GameObject group = new GameObject(groupName, typeof(RectTransform));
            group.transform.SetParent(parent, false);
            groupParent = group.transform;
        }

        CreateSegment(groupParent, LeftName, fillKind);
        CreateSegment(groupParent, MidName, fillKind);
        CreateSegment(groupParent, RightName, fillKind);
    }

    private void CreateSegment(Transform parent, string name, BarFillKind fillKind)
    {
        GameObject segment = new GameObject(name, typeof(RectTransform), typeof(Image));
        segment.transform.SetParent(parent, false);
        Image image = segment.GetComponent<Image>();
        image.raycastTarget = false;
        ApplySegmentSprite(image, name, fillKind);
    }

    private void RefreshBarSprites()
    {
        Transform barArea = rootRect != null ? rootRect.Find(BarAreaName) : null;
        ApplySpritesToGroup(barArea != null ? barArea.Find(BackName) : null, BarFillKind.Back);
        if (fillMaskRect != null)
            ApplySpritesToGroup(fillMaskRect.Find(FillName), BarFillKind.Blue);
    }

    private void ApplySpritesToGroup(Transform group, BarFillKind fillKind)
    {
        if (group == null)
            return;

        ApplySpriteToSegment(group.Find(LeftName), LeftName, fillKind);
        ApplySpriteToSegment(group.Find(MidName), MidName, fillKind);
        ApplySpriteToSegment(group.Find(RightName), RightName, fillKind);
    }

    private void ApplySpriteToSegment(Transform segment, string name, BarFillKind fillKind)
    {
        if (segment == null)
            return;

        Image image = segment.GetComponent<Image>();
        if (image == null)
            return;

        ApplySegmentSprite(image, name, fillKind);
    }

    private void ApplySegmentSprite(Image image, string name, BarFillKind fillKind)
    {
        if (image == null)
            return;

        if (fillKind == BarFillKind.Blue)
        {
            Sprite fillSprite = HealthBarSprites.GetBlue(name);
            HealthBarSprites.ApplyBarImage(image, fillSprite, name == MidName, fillTint);
            return;
        }

        HealthBarSprites.ApplyBackBarImage(image, HealthBarSprites.GetBack(name), name == MidName);
    }

    private bool NeedsLayoutRebuild()
    {
        if (rootRect == null)
            return false;

        if (expHudLayoutVersion != ExpHudLayoutVersion)
            return true;

        Transform barArea = rootRect.Find(BarAreaName);
        if (barArea == null)
            return true;

        Transform back = barArea.Find(BackName);
        if (back == null)
            return true;

        return back.Find(LeftName) == null ||
            back.Find(MidName) == null ||
            back.Find(RightName) == null;
    }

    private bool HasExpectedParts()
    {
        Transform barArea = rootRect != null ? rootRect.Find(BarAreaName) : null;
        return rootRect != null &&
            rootRect.Find("LevelText") != null &&
            rootRect.Find("ExpText") != null &&
            barArea != null &&
            barArea.Find(BackName + "/" + LeftName) != null &&
            barArea.Find(FillMaskName + "/" + FillName + "/" + LeftName) != null;
    }

    private enum BarFillKind
    {
        Back,
        Blue,
    }

    private void CacheParts()
    {
        if (rootRect == null)
            return;

        if (levelText == null)
            levelText = rootRect.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
        if (expText == null)
            expText = rootRect.Find("ExpText")?.GetComponent<TextMeshProUGUI>();

        Transform barArea = rootRect.Find(BarAreaName);
        if (fillMaskRect == null && barArea != null)
            fillMaskRect = barArea.Find(FillMaskName) as RectTransform;
    }

    private void ApplyLayout()
    {
        if (rootRect == null || levelText == null || expText == null)
            return;

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(barSize.x, barSize.y + labelBarGap + labelHeight);

        float labelY = -(barSize.y + labelBarGap);
        float innerWidth = Mathf.Max(0f, barSize.x - fillInset * 2f);

        RectTransform levelRect = levelText.rectTransform;
        levelRect.anchorMin = new Vector2(0f, 1f);
        levelRect.anchorMax = new Vector2(0.5f, 1f);
        levelRect.pivot = new Vector2(0f, 1f);
        levelRect.anchoredPosition = new Vector2(0f, labelY);
        levelRect.sizeDelta = new Vector2(0f, labelHeight);

        RectTransform expRect = expText.rectTransform;
        expRect.anchorMin = new Vector2(0.5f, 1f);
        expRect.anchorMax = new Vector2(1f, 1f);
        expRect.pivot = new Vector2(1f, 1f);
        expRect.anchoredPosition = new Vector2(0f, labelY);
        expRect.sizeDelta = new Vector2(0f, labelHeight);

        RectTransform barAreaRect = rootRect.Find(BarAreaName) as RectTransform;
        if (barAreaRect == null)
            return;

        barAreaRect.anchorMin = new Vector2(0f, 1f);
        barAreaRect.anchorMax = new Vector2(0f, 1f);
        barAreaRect.pivot = new Vector2(0f, 1f);
        barAreaRect.anchoredPosition = Vector2.zero;
        barAreaRect.sizeDelta = barSize;

        LayoutTrackGroup(barAreaRect.Find(BackName) as RectTransform, innerWidth);

        if (fillMaskRect != null)
        {
            fillMaskRect.anchorMin = new Vector2(0f, 0f);
            fillMaskRect.anchorMax = new Vector2(0f, 1f);
            fillMaskRect.pivot = new Vector2(0f, 0.5f);
            fillMaskRect.anchoredPosition = new Vector2(fillInset, 0f);

            RectTransform fillGroup = fillMaskRect.Find(FillName) as RectTransform;
            if (fillGroup != null)
            {
                StretchToParent(fillGroup);
                LayoutBarSegments(fillGroup, innerWidth);
            }

            UpdateFill(displayedProgress01);
        }
    }

    private void LayoutTrackGroup(RectTransform groupRect, float width)
    {
        if (groupRect == null)
            return;

        groupRect.anchorMin = new Vector2(0f, 0f);
        groupRect.anchorMax = new Vector2(0f, 1f);
        groupRect.pivot = new Vector2(0f, 0.5f);
        groupRect.anchoredPosition = new Vector2(fillInset, 0f);
        groupRect.sizeDelta = new Vector2(width, 0f);
        LayoutBarSegments(groupRect, width);
    }

    private void LayoutBarSegments(RectTransform groupRect, float width)
    {
        if (groupRect == null)
            return;

        LayoutBarSegment(groupRect.Find(LeftName) as RectTransform, 0f, 0f, capWidth);
        LayoutBarSegment(groupRect.Find(MidName) as RectTransform, capWidth, 0f, Mathf.Max(0f, width - capWidth * 2f));
        LayoutBarSegment(groupRect.Find(RightName) as RectTransform, width - capWidth, 0f, capWidth);
    }

    private static void LayoutBarSegment(RectTransform rect, float x, float y, float width)
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
        fillMaskRect.sizeDelta = new Vector2(innerWidth * Mathf.Clamp01(progress01), 0f);
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
