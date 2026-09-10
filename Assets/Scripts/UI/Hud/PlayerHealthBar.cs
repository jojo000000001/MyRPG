using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player HUD health bar built from the Kenney RPG UI bar sprites.
/// </summary>
public sealed class PlayerHealthBar : MonoBehaviour
{
    private const string RootName = "PlayerHealthBarRoot";
    private const string BarAreaName = "BarArea";
    private const string HpTextName = "HpText";
    private const string TrackPlateName = "TrackPlate";
    private const string BackName = "Back";

    [Header("Target")]
    [SerializeField] private Player player;
    [SerializeField] private HealthBarSpriteCatalog spriteCatalog;

    private const int HudLayoutVersion = 4;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = HudBarVisualStyle.HealthAnchoredPosition;
    [SerializeField] private Vector2 barSize = HudBarVisualStyle.BarSize;
    [SerializeField] private float capWidth = HudBarVisualStyle.CapWidth;
    [SerializeField] private float fillInset = HudBarVisualStyle.FillInset;
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private bool useTrackPlate;
    [SerializeField] private bool showBackTrack;
    [SerializeField] private int hudLayoutVersion;

    [Header("Display")]
    [SerializeField] private float smoothSpeed = 14f;
    [SerializeField] private Color hpTextColor = HudBarVisualStyle.ValueTextColor;

    internal HealthBarSpriteCatalog SpriteCatalog => spriteCatalog;
    internal Vector2 HudAnchoredPosition => anchoredPosition;
    internal Vector2 HudBarSize => barSize;

    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private GraphicRaycaster graphicRaycaster;
    private RectTransform rootRect;
    private KenneyBarView barView;
    private TextMeshProUGUI hpText;
    private float displayedHealth01 = 1f;
    private bool uiBuilt;
    private bool spritesReady;
    private bool prefabUi;

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

        if (!prefabUi)
        {
            ApplyHudVisualDefaults();
            ClearLegacyRoots();
            uiBuilt = false;
        }

        EnsureUi();
        UpdateImmediate();
        ApplySprites();

        if (player != null)
            player.StatsChanged += HandleStatsChanged;
    }

    internal void ApplyHudVisualDefaults()
    {
        anchoredPosition = HudBarVisualStyle.HealthAnchoredPosition;
        barSize = HudBarVisualStyle.BarSize;
        capWidth = HudBarVisualStyle.CapWidth;
        fillInset = HudBarVisualStyle.FillInset;
        useTrackPlate = false;
        showBackTrack = true;
        hudLayoutVersion = HudLayoutVersion;
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
        UpdateHpText();
    }

    private void EnsureUi()
    {
        if (prefabUi)
        {
            EnsureCanvasReady();
            if (rootRect == null)
                rootRect = transform.Find(RootName) as RectTransform;

            if (rootRect == null || !HasExpectedBarParts())
            {
                Debug.LogError("PlayerHealthBar: prefab UI is missing PlayerHealthBarRoot hierarchy.", this);
                return;
            }

            CacheBarParts();
            ApplyLayout();
            ApplySprites();
            uiBuilt = true;
            return;
        }

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

        if (rootRect != null && HasExpectedBarParts() && NeedsLayoutRebuild())
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
            if (!IsLegacyHudBarRoot(child.name))
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        rootRect = null;
        barView = null;
        hpText = null;
        uiBuilt = false;
    }

    private static bool IsLegacyHudBarRoot(string childName)
    {
        return childName.StartsWith("PlayerHealthBarRoot") ||
            childName.StartsWith("PlayerExperienceBarRoot");
    }

    private bool HasExpectedBarParts()
    {
        if (rootRect == null || rootRect.Find(HpTextName) == null)
            return false;

        return KenneyBarView.HasExpectedParts(GetBarAreaRect(), showBackTrack);
    }

    private RectTransform GetBarAreaRect()
    {
        if (rootRect == null)
            return null;

        Transform barArea = rootRect.Find(BarAreaName);
        return barArea as RectTransform ?? rootRect;
    }

    private void DestroyExistingRoot()
    {
        GameObject oldRoot = rootRect.gameObject;
        rootRect = null;
        barView = null;
        hpText = null;
        uiBuilt = false;

        if (Application.isPlaying)
            Destroy(oldRoot);
        else
            DestroyImmediate(oldRoot);
    }

    private bool NeedsLayoutRebuild()
    {
        if (rootRect == null)
            return false;

        if (rootRect.sizeDelta != HudBarVisualStyle.LabeledRootSize(barSize))
            return true;

        if (hudLayoutVersion != HudLayoutVersion)
            return true;

        if (rootRect.Find(BarAreaName) == null || rootRect.Find(HpTextName) == null)
            return true;

        Transform backParent = rootRect.Find(BarAreaName) ?? rootRect;
        if (!showBackTrack && backParent.Find(BackName) != null)
            return true;

        return !useTrackPlate && backParent.Find(TrackPlateName) != null;
    }

    private void CreateBar()
    {
        GameObject rootObject = new GameObject(RootName, typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        rootRect = rootObject.GetComponent<RectTransform>();
        barView = KenneyBarView.Create(rootRect, BarAreaName, showBackTrack, useTrackPlate);
        hpText = CreateHpLabel(rootRect);
    }

    private TextMeshProUGUI CreateHpLabel(Transform parent)
    {
        GameObject labelObject = new GameObject(HpTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Right;
        ChineseUITmpFont.Apply(label, 16f, FontStyles.Bold);
        label.color = hpTextColor;
        label.raycastTarget = false;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(20, 12, 8, 220);
        return label;
    }

    private void CacheBarParts()
    {
        if (rootRect == null)
            return;

        barView = KenneyBarView.Bind(GetBarAreaRect(), showBackTrack);
        if (hpText == null)
            hpText = rootRect.Find(HpTextName)?.GetComponent<TextMeshProUGUI>();
    }

    private void ApplyLayout()
    {
        if (rootRect == null)
            return;

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = HudBarVisualStyle.LabeledRootSize(barSize);

        RectTransform barArea = GetBarAreaRect();
        if (barArea != null && barArea != rootRect)
        {
            barArea.anchorMin = new Vector2(0f, 1f);
            barArea.anchorMax = new Vector2(0f, 1f);
            barArea.pivot = new Vector2(0f, 1f);
            barArea.anchoredPosition = Vector2.zero;
            barArea.sizeDelta = barSize;
        }

        barView?.LayoutContents(barSize, capWidth, fillInset);
        LayoutHpText();
    }

    private void LayoutHpText()
    {
        if (hpText == null)
            hpText = rootRect != null ? rootRect.Find(HpTextName)?.GetComponent<TextMeshProUGUI>() : null;
        if (hpText == null)
            return;

        RectTransform rect = hpText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(0f, HudBarVisualStyle.LabelAnchoredY(barSize));
        rect.sizeDelta = new Vector2(0f, HudBarVisualStyle.LabelHeight);
        hpText.alignment = TextAlignmentOptions.Right;
        hpText.color = hpTextColor;
    }

    private void ApplySprites()
    {
        barView?.ApplySprites(displayedHealth01 >= 0.5f);
    }

    private void UpdateImmediate()
    {
        displayedHealth01 = GetHealth01();
        UpdateFill(displayedHealth01);
        UpdateHpText();
    }

    private void UpdateHpText()
    {
        if (hpText == null)
            return;

        int current = player != null ? player.CurrentHp : 0;
        int max = player != null ? player.MaxHp : 0;
        int shield = player != null ? player.CurrentShieldDurability : 0;
        int shieldMax = player != null ? player.MaxShieldDurability : 0;
        hpText.text = current + " / " + max + "  盾 " + shield + "/" + shieldMax;
    }

    private float GetHealth01()
    {
        if (player == null || player.MaxHp <= 0)
            return 0f;

        return Mathf.Clamp01((float)player.CurrentHp / player.MaxHp);
    }

    private void UpdateFill(float health01)
    {
        barView?.SetFill01(health01, barSize.x, fillInset);
    }
}
