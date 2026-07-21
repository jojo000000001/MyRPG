using UnityEditor;
using UnityEngine;

public static class SetupHealthBarCatalog
{
    private const string KenneyRoot = "Assets/Art/UI/kenney_ui-pack-rpg-expansion/PNG/";
    private const string CatalogPath = "Assets/Resources/HealthBarSpriteCatalog.asset";

    [MenuItem("Tools/MyRPG/Setup Health Bar Catalog")]
    public static void SetupFromMenu()
    {
        int configured = KenneyUISpriteSetup.SetupAll();
        HealthBarSpriteCatalog catalog = BuildOrLoadCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SetupHealthBarCatalog] Kenney textures configured: {configured}. Catalog: {CatalogPath}");
    }

    public static HealthBarSpriteCatalog BuildOrLoadCatalog()
    {
        HealthBarSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<HealthBarSpriteCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<HealthBarSpriteCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.backLeft = LoadKenneySprite("barBack_horizontalLeft");
        catalog.backMid = LoadKenneySprite("barBack_horizontalMid");
        catalog.backRight = LoadKenneySprite("barBack_horizontalRight");

        catalog.greenLeft = LoadKenneySprite("barGreen_horizontalLeft");
        catalog.greenMid = LoadKenneySprite("barGreen_horizontalMid");
        catalog.greenRight = LoadKenneySprite("barGreen_horizontalRight");

        catalog.redLeft = LoadKenneySprite("barRed_horizontalLeft");
        catalog.redMid = LoadKenneySprite("barRed_horizontalMid");
        catalog.redRight = LoadKenneySprite("barRed_horizontalRight");

        catalog.blueLeft = LoadKenneySprite("barBlue_horizontalLeft");
        catalog.blueMid = LoadKenneySprite("barBlue_horizontalBlue");
        catalog.blueRight = LoadKenneySprite("barBlue_horizontalRight");

        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static Sprite LoadKenneySprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyRoot}{fileName}.png");
    }
}
