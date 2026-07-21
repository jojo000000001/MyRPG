using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kenney RPG UI bar sprites for player HUD health and experience bars.
/// </summary>
public static class HealthBarSprites
{
    private const string ResourceRoot = "UI/HealthBar/";
    private static HealthBarSpriteCatalog catalog;

    public static bool HasCatalog => EnsureCatalog() != null && EnsureCatalog().HasBackSprites;

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

        catalog = Resources.Load<HealthBarSpriteCatalog>("HealthBarSpriteCatalog");
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

    public static void ApplyBarImage(Image image, Sprite sprite, bool isMidSegment)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.type = isMidSegment && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
    }
}
