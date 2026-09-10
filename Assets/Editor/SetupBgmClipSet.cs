using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 创建并绑定 xDeviruchi BGM 资源到 Resources/BgmClipSet.asset。
/// </summary>
public static class SetupBgmClipSet
{
    private const string ClipSetAssetPath = "Assets/Resources/BgmClipSet.asset";
    private const string ResourcesBgmDir = "Assets/Resources/BGM";
    private const string OggRoot =
        "Assets/xDeviruchi - 16 bit Fantasy & Adventure (2025)/xDeviruchi - 16 bit Fantasy & Adventure (2025)/Loopable + one shots/ogg";

    [MenuItem("Tools/MyRPG/Setup BGM Clip Set")]
    public static void SetupFromMenu()
    {
        BgmClipSet clipSet = EnsureClipSetAsset();
        if (clipSet == null)
            return;

        clipSet.title = LoadClip("02 - Title Theme.ogg");
        clipSet.town = LoadClip("03 - Definitely Our Town.ogg");
        clipSet.forest = LoadClip("04 - Silent Forest.ogg");
        clipSet.battle1 = LoadClip("05 - Battle 1.ogg");
        clipSet.battle2 = LoadClip("09 - Battle 2.ogg");

        EnsureResourcesBgmCopy("02 - Title Theme.ogg", "Title.ogg");
        EnsureResourcesBgmCopy("03 - Definitely Our Town.ogg", "Town.ogg");
        EnsureResourcesBgmCopy("04 - Silent Forest.ogg", "Forest.ogg");
        EnsureResourcesBgmCopy("05 - Battle 1.ogg", "Battle1.ogg");
        EnsureResourcesBgmCopy("09 - Battle 2.ogg", "Battle2.ogg");

        ConfigureBgmImport(ResourcesBgmDir + "/Title.ogg");
        ConfigureBgmImport(ResourcesBgmDir + "/Town.ogg");
        ConfigureBgmImport(ResourcesBgmDir + "/Forest.ogg");
        ConfigureBgmImport(ResourcesBgmDir + "/Battle1.ogg");
        ConfigureBgmImport(ResourcesBgmDir + "/Battle2.ogg");
        ConfigureBgmImport($"{OggRoot}/02 - Title Theme.ogg");
        ConfigureBgmImport($"{OggRoot}/03 - Definitely Our Town.ogg");
        ConfigureBgmImport($"{OggRoot}/04 - Silent Forest.ogg");
        ConfigureBgmImport($"{OggRoot}/05 - Battle 1.ogg");
        ConfigureBgmImport($"{OggRoot}/09 - Battle 2.ogg");

        EditorUtility.SetDirty(clipSet);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "BGM Clip Set",
            "BgmClipSet.asset updated:\n- Title Theme\n- Definitely Our Town\n- Silent Forest\n- Battle 1\n- Battle 2",
            "OK");
    }

    private static BgmClipSet EnsureClipSetAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<BgmClipSet>(ClipSetAssetPath);
        if (existing != null)
            return existing;

        string directory = Path.GetDirectoryName(ClipSetAssetPath);
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var created = ScriptableObject.CreateInstance<BgmClipSet>();
        AssetDatabase.CreateAsset(created, ClipSetAssetPath);
        return created;
    }

    private static AudioClip LoadClip(string fileName)
    {
        string assetPath = $"{OggRoot}/{fileName}";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (clip == null)
            Debug.LogWarning($"SetupBgmClipSet: missing clip at {assetPath}");

        return clip;
    }

    private static void EnsureResourcesBgmCopy(string sourceFileName, string targetFileName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (!AssetDatabase.IsValidFolder(ResourcesBgmDir))
            AssetDatabase.CreateFolder("Assets/Resources", "BGM");

        string sourcePath = $"{OggRoot}/{sourceFileName}";
        string targetPath = $"{ResourcesBgmDir}/{targetFileName}";
        if (AssetDatabase.LoadAssetAtPath<Object>(targetPath) != null)
            return;

        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            Debug.LogWarning($"SetupBgmClipSet: failed to copy {sourcePath} -> {targetPath}");
    }

    private static void ConfigureBgmImport(string assetPath)
    {
        AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
        if (importer == null)
            return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = true;
        importer.SaveAndReimport();
    }
}
