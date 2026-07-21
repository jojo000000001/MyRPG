using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RemoveConsumableIconBackground
{
    private const string IconFolder = "Assets/Art/Items/Consumables";
    private const int DarkThreshold = 32;

    private static readonly string[] BatchIconFiles =
    {
        "consumable_greater_hp_potion.png",
        "consumable_roast_meat.png",
        "consumable_healing_herb.png",
        "consumable_fire_bomb.png",
        "consumable_frost_potion.png",
        "consumable_golden_apple.png",
        "consumable_strength_tonic.png",
        "consumable_swiftness_elixir.png",
        "consumable_magic_scroll.png",
        "consumable_honey_cookie.png",
    };

    [MenuItem("Tools/Items/Remove Batch Consumable Icon Backgrounds")]
    public static void RemoveBatchBackgrounds()
    {
        int changed = 0;
        for (int i = 0; i < BatchIconFiles.Length; i++)
        {
            string assetPath = $"{IconFolder}/{BatchIconFiles[i]}";
            if (RemoveBackgroundAtPath(assetPath))
                changed++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"Removed icon backgrounds for {changed}/{BatchIconFiles.Length} batch consumables.");
    }

    private static bool RemoveBackgroundAtPath(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            Debug.LogWarning($"Missing icon: {assetPath}");
            return false;
        }

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        bool wasReadable = importer.isReadable;
        if (!wasReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (source == null)
            return false;

        Color32[] pixels = source.GetPixels32();
        int width = source.width;
        int height = source.height;
        bool[] remove = BuildRemovalMask(pixels, width, height);

        int removedCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (!remove[i])
                continue;

            pixels[i].a = 0;
            removedCount++;
        }

        if (removedCount <= 0)
            return false;

        Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        output.SetPixels32(pixels);
        output.Apply();

        File.WriteAllBytes(assetPath, output.EncodeToPNG());
        Object.DestroyImmediate(output);

        if (!wasReadable)
        {
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        else
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        return true;
    }

    private static bool[] BuildRemovalMask(Color32[] pixels, int width, int height)
    {
        bool[] remove = new bool[pixels.Length];
        bool[] visited = new bool[pixels.Length];
        Queue<int> queue = new Queue<int>();

        EnqueueSeedIfDark(pixels, remove, visited, queue, 0);
        EnqueueSeedIfDark(pixels, remove, visited, queue, width - 1);
        EnqueueSeedIfDark(pixels, remove, visited, queue, (height - 1) * width);
        EnqueueSeedIfDark(pixels, remove, visited, queue, (height - 1) * width + width - 1);

        for (int x = 0; x < width; x++)
        {
            EnqueueSeedIfDark(pixels, remove, visited, queue, x);
            EnqueueSeedIfDark(pixels, remove, visited, queue, (height - 1) * width + x);
        }

        for (int y = 0; y < height; y++)
        {
            EnqueueSeedIfDark(pixels, remove, visited, queue, y * width);
            EnqueueSeedIfDark(pixels, remove, visited, queue, y * width + width - 1);
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            if (remove[index])
                continue;

            remove[index] = true;

            int x = index % width;
            int y = index / width;

            TryEnqueueNeighbor(pixels, remove, visited, queue, index - 1, x > 0);
            TryEnqueueNeighbor(pixels, remove, visited, queue, index + 1, x < width - 1);
            TryEnqueueNeighbor(pixels, remove, visited, queue, index - width, y > 0);
            TryEnqueueNeighbor(pixels, remove, visited, queue, index + width, y < height - 1);
        }

        return remove;
    }

    private static void EnqueueSeedIfDark(
        Color32[] pixels,
        bool[] remove,
        bool[] visited,
        Queue<int> queue,
        int index)
    {
        if (index < 0 || index >= pixels.Length || visited[index])
            return;

        visited[index] = true;
        if (!IsDarkPixel(pixels[index]))
            return;

        queue.Enqueue(index);
    }

    private static void TryEnqueueNeighbor(
        Color32[] pixels,
        bool[] remove,
        bool[] visited,
        Queue<int> queue,
        int index,
        bool inBounds)
    {
        if (!inBounds || index < 0 || index >= pixels.Length || visited[index])
            return;

        visited[index] = true;
        if (!IsDarkPixel(pixels[index]))
            return;

        queue.Enqueue(index);
    }

    private static bool IsDarkPixel(Color32 pixel)
    {
        return pixel.r <= DarkThreshold
            && pixel.g <= DarkThreshold
            && pixel.b <= DarkThreshold;
    }
}
