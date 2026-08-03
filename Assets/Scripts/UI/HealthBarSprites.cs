using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kenney RPG UI bar sprites for player HUD health and experience bars.
/// </summary>
public static class HealthBarSprites
{
    private const string ResourceRoot = "UI/HealthBar/";
    private const string CatalogResourcePath = "HealthBarSpriteCatalog";
    private static HealthBarSpriteCatalog catalog;

    public static bool HasCatalog =>
        GetBack("Mid") != null ||
        GetGreen("Mid") != null ||
        GetBlue("Mid") != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCatalog()
    {
        catalog = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PreloadCatalog()
    {
        EnsureCatalog();
    }

    public static void BindCatalog(HealthBarSpriteCatalog explicitCatalog)
    {
        if (explicitCatalog != null)
            catalog = explicitCatalog;
    }

    public static Sprite GetBack(string segment)
    {
        HealthBarSpriteCatalog data = EnsureCatalog();
        if (data != null)
        {
            Sprite sprite = data.GetBack(segment);
            if (sprite != null)
                return sprite;
        }

        return LoadLegacy($"barBack_horizontal{SegmentSuffix(segment)}");
    }

    public static Sprite GetGreen(string segment)
    {
        HealthBarSpriteCatalog data = EnsureCatalog();
        if (data != null)
        {
            Sprite sprite = data.GetGreen(segment);
            if (sprite != null)
                return sprite;
        }

        return LoadLegacy($"barGreen_horizontal{SegmentSuffix(segment)}");
    }

    public static Sprite GetRed(string segment)
    {
        HealthBarSpriteCatalog data = EnsureCatalog();
        if (data != null)
        {
            Sprite sprite = data.GetRed(segment);
            if (sprite != null)
                return sprite;
        }

        return LoadLegacy($"barRed_horizontal{SegmentSuffix(segment)}");
    }

    public static Sprite GetBlue(string segment)
    {
        HealthBarSpriteCatalog data = EnsureCatalog();
        if (data != null)
        {
            Sprite sprite = data.GetBlue(segment);
            if (sprite != null)
                return sprite;
        }

        string legacyName = segment == "Mid" ? "barBlue_horizontalBlue" : $"barBlue_horizontal{SegmentSuffix(segment)}";
        return LoadLegacy(legacyName);
    }

    private static HealthBarSpriteCatalog EnsureCatalog()
    {
        if (catalog != null)
            return catalog;

        catalog = Resources.Load<HealthBarSpriteCatalog>(CatalogResourcePath);
        if (catalog == null)
        {
            HealthBarSpriteCatalog[] all = Resources.FindObjectsOfTypeAll<HealthBarSpriteCatalog>();
            if (all != null && all.Length > 0)
                catalog = all[0];
        }

        return catalog;
    }

    private static string SegmentSuffix(string segment)
    {
        return segment switch
        {
            "Left" => "Left",
            "Right" => "Right",
            _ => "Mid",
        };
    }

    private static Sprite LoadLegacy(string resourceName)
    {
        string path = ResourceRoot + resourceName;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
    }

    public static void ApplyBarImage(Image image, Sprite sprite, bool isMidSegment, Color fallbackColor)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.type = isMidSegment && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = false;
        image.color = sprite != null ? Color.white : fallbackColor;
        image.raycastTarget = false;
        image.enabled = true;
        image.material = null;
    }

    public static void ApplyBarImage(Image image, Sprite sprite, bool isMidSegment)
    {
        ApplyBackBarImage(image, sprite, isMidSegment);
    }

    /// <summary>
    /// Kenney barBack sprites are mostly transparent; keep a dark fallback when sprites fail to load.
    /// </summary>
    public static void ApplyBackBarImage(Image image, Sprite sprite, bool isMidSegment)
    {
        ApplyBarImage(image, sprite, isMidSegment, HudBarVisualStyle.BackBarFallback);
        if (sprite != null && image != null)
            image.color = HudBarVisualStyle.BackBarTint;
    }

    public static readonly Color TrackPlateColor = new Color(0.10f, 0.14f, 0.22f, 0.35f);
}

/// <summary>
/// 左上角 HUD 血条/经验条共用的尺寸与底色。
/// </summary>
public static class HudBarVisualStyle
{
    public static readonly Vector2 BarSize = new Vector2(280f, 18f);
    public const float CapWidth = 12f;
    public const float FillInset = 2f;
    public const float BarStackGap = 8f;

    public static readonly Vector2 HealthAnchoredPosition = new Vector2(24f, -24f);

    public static readonly Color BackBarTint = new Color(0.72f, 0.80f, 0.92f, 0.42f);
    public static readonly Color BackBarFallback = new Color(0.16f, 0.22f, 0.32f, 0.82f);

    public static readonly Color GreenFillTint = new Color(0.38f, 0.88f, 0.44f, 1f);
    public static readonly Color RedFillTint = new Color(0.98f, 0.38f, 0.30f, 1f);
    public static readonly Color BlueFillTint = new Color(0.42f, 0.72f, 1f, 1f);

    public static Vector2 ComputeExpBarAnchoredPosition(
        float labelHeight,
        float labelBarGap,
        Vector2? healthAnchoredPosition = null,
        Vector2? healthBarSize = null)
    {
        Vector2 healthPos = healthAnchoredPosition ?? HealthAnchoredPosition;
        float healthHeight = (healthBarSize ?? BarSize).y;
        float healthBottom = healthPos.y - healthHeight;
        return new Vector2(healthPos.x, healthBottom - BarStackGap);
    }
}
