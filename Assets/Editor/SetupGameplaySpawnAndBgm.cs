using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 在 SampleScene 中创建出生点与探索 BGM 区域。
/// </summary>
public static class SetupGameplaySpawnAndBgm
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private static readonly Vector3 DefaultVillageCenter = new Vector3(22f, 1f, -35f);
    private const float DefaultVillageRadius = 30f;
    private static readonly Vector3 DefaultForestCenter = new Vector3(-8f, 1f, 8f);
    private static readonly Vector2 DefaultForestHalfExtents = new Vector2(28f, 26f);

    [MenuItem("Tools/MyRPG/Setup Spawn And Village BGM")]
    public static void SetupFromMenu()
    {
        SetupFromMenuSilent();
        EditorUtility.DisplayDialog(
            "Gameplay Setup",
            "SampleScene updated:\n- GameSpawnPoint synced to Player\n- Village + Forest BGM zones",
            "OK");
    }

    internal static void SetupFromMenuSilent()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Player player = Object.FindObjectOfType<Player>();
        SetupSpawnPoint(player);
        SetupAreaZone(AreaBgmZone.AreaKind.Village, "VillageBgmZone", DefaultVillageCenter, DefaultVillageRadius, Vector2.zero);
        SetupAreaZone(AreaBgmZone.AreaKind.Forest, "ForestBgmZone", DefaultForestCenter, 0f, DefaultForestHalfExtents);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetupSpawnPoint(Player player)
    {
        GameObject spawnObject = GameObject.Find("GameSpawnPoint");
        if (spawnObject == null)
            spawnObject = new GameObject("GameSpawnPoint");

        GameSpawnPoint spawnPoint = spawnObject.GetComponent<GameSpawnPoint>();
        if (spawnPoint == null)
            spawnPoint = spawnObject.AddComponent<GameSpawnPoint>();

        SerializedObject serializedSpawn = new SerializedObject(spawnPoint);
        if (player != null)
        {
            serializedSpawn.FindProperty("spawnPosition").vector3Value = player.transform.position;
            serializedSpawn.FindProperty("spawnRotationY").floatValue = player.transform.eulerAngles.y;
        }

        serializedSpawn.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupAreaZone(
        AreaBgmZone.AreaKind kind,
        string objectName,
        Vector3 center,
        float radius,
        Vector2 halfExtents)
    {
        GameObject zoneObject = GameObject.Find(objectName);
        if (zoneObject == null)
        {
            zoneObject = new GameObject(objectName);
            EditorSceneManager.MoveGameObjectToScene(zoneObject, EditorSceneManager.GetActiveScene());
        }

        AreaBgmZone zone = zoneObject.GetComponent<AreaBgmZone>();
        if (zone == null)
            zone = zoneObject.AddComponent<AreaBgmZone>();

        zone.Kind = kind;
        zone.ZoneCenter = center;
        zone.AlwaysDrawGizmo = true;

        if (kind == AreaBgmZone.AreaKind.Forest)
            zone.ZoneHalfExtents = halfExtents;
        else
            zone.ZoneRadius = radius;

        Collider legacyCollider = zoneObject.GetComponent<Collider>();
        if (legacyCollider != null)
            Object.DestroyImmediate(legacyCollider);
    }
}
