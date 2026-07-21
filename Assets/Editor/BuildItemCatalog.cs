using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildItemCatalog
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string CatalogPath = "Assets/Resources/ItemCatalog.asset";
    private const string LegacyCatalogPath = "Assets/Resources/ItemDatabase.asset";

    [MenuItem("Tools/Save/Rebuild Item Catalog")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        string[] guids = AssetDatabase.FindAssets("t:ItemSO");
        var items = new List<ItemSO>(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
            if (item != null)
                items.Add(item);
        }

        items.Sort((a, b) => a.id.CompareTo(b.id));

        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        if (catalog == null)
            catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(LegacyCatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.SetItems(items);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"ItemCatalog rebuilt with {items.Count} items at {CatalogPath}");
    }
}
