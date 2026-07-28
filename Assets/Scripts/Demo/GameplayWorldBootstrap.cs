using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 确保 SampleScene 存在出生点与探索 BGM 区域。
/// </summary>
static class GameplayWorldBootstrap
{
    private const string GameplaySceneName = "SampleScene";
    private static readonly Vector3 VillageCenter = new Vector3(22f, 1f, -35f);
    private const float VillageRadius = 30f;
    private static readonly Vector3 ForestCenter = new Vector3(-8f, 1f, 8f);
    private static readonly Vector2 ForestHalfExtents = new Vector2(28f, 26f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != GameplaySceneName)
            return;

        EnsureSpawnPoint();
        EnsureAreaZone(AreaBgmZone.AreaKind.Village, "VillageBgmZone", VillageCenter, VillageRadius, Vector2.zero);
        EnsureAreaZone(AreaBgmZone.AreaKind.Forest, "ForestBgmZone", ForestCenter, 0f, ForestHalfExtents);
    }

    private static void EnsureSpawnPoint()
    {
        if (GameSpawnPoint.FindInScene() != null)
            return;

        Player player = Player.ActiveInstance != null
            ? Player.ActiveInstance
            : Object.FindObjectOfType<Player>();

        GameObject host = new GameObject("GameSpawnPoint");
        GameSpawnPoint spawnPoint = host.AddComponent<GameSpawnPoint>();

        if (player != null)
            spawnPoint.SetSpawn(player.transform.position, player.transform.eulerAngles.y);
    }

    private static void EnsureAreaZone(
        AreaBgmZone.AreaKind kind,
        string objectName,
        Vector3 center,
        float radius,
        Vector2 halfExtents)
    {
        AreaBgmZone zone = FindAreaZone(kind, objectName);
        bool created = false;
        if (zone == null)
        {
            GameObject host = new GameObject(objectName);
            zone = host.AddComponent<AreaBgmZone>();
            zone.Kind = kind;
            created = true;
        }

        if (kind == AreaBgmZone.AreaKind.Forest)
        {
            if (created || zone.ZoneHalfExtents.x <= 0.5f || zone.ZoneHalfExtents.y <= 0.5f)
            {
                zone.ZoneCenter = center;
                zone.ZoneHalfExtents = halfExtents;
            }

            return;
        }

        if (created || zone.ZoneRadius <= 0f)
        {
            zone.ZoneCenter = center;
            zone.ZoneRadius = radius;
        }
    }

    private static AreaBgmZone FindAreaZone(AreaBgmZone.AreaKind kind, string objectName)
    {
        GameObject named = GameObject.Find(objectName);
        if (named != null)
        {
            AreaBgmZone zone = named.GetComponent<AreaBgmZone>();
            if (zone != null)
                return zone;
        }

        AreaBgmZone[] zones = Object.FindObjectsOfType<AreaBgmZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].Kind == kind)
                return zones[i];
        }

        return null;
    }
}
