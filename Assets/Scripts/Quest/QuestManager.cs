using System.Collections;
using UnityEngine;

/// <summary>
/// 运行时任务中心：接取、追踪进度、完成。第一弹为狗骑士发布的讨伐入侵哥布林。
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestManager : MonoBehaviour
{
    public enum Status
    {
        Inactive,
        Active,
        Completed
    }

    private const float InvasionSpawnDistance = 24f;
    private const float InvasionSpawnDelay = 0.35f;
    private const float InvasionLoseRadius = 40f;
    private const float InvasionLeashRadius = 50f;
    private const int InvasionMaxHp = 20;

    public static QuestManager Instance { get; private set; }

    private QuestDefinition huntGoblin;
    private Status huntGoblinStatus = Status.Inactive;
    private int huntGoblinProgress;
    private Monster invadingGoblin;
    private Coroutine invasionRoutine;

    public static QuestManager Ensure()
    {
        if (Instance != null)
            return Instance;

        GameObject host = new GameObject("QuestManager");
        return host.AddComponent<QuestManager>();
    }

    public Status GetStatus(string questId)
    {
        if (questId == QuestIds.HuntInvadingGoblin)
            return huntGoblinStatus;

        return Status.Inactive;
    }

    /// <summary>
    /// 狗骑士对话结束后调用：接取任务并刷出一只朝玩家冲来的哥布林。
    /// </summary>
    public void StartHuntInvadingGoblin(Transform questGiver)
    {
        if (huntGoblinStatus != Status.Inactive)
            return;

        huntGoblin = QuestDefinition.HuntInvadingGoblin();
        huntGoblinStatus = Status.Active;
        huntGoblinProgress = 0;

        QuestTrackerUI.Ensure().ShowQuest(huntGoblin, huntGoblinProgress, huntGoblin.requiredCount);

        if (invasionRoutine != null)
            StopCoroutine(invasionRoutine);

        invasionRoutine = StartCoroutine(SpawnInvadingGoblinSoon(questGiver));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeInvader();
    }

    private IEnumerator SpawnInvadingGoblinSoon(Transform questGiver)
    {
        yield return new WaitForSeconds(InvasionSpawnDelay);
        invasionRoutine = null;
        SpawnInvadingGoblin(questGiver);
    }

    private void SpawnInvadingGoblin(Transform questGiver)
    {
        if (huntGoblinStatus != Status.Active)
            return;

        GoblinSpawner spawner = FindObjectOfType<GoblinSpawner>();
        GameObject prefab = spawner != null ? spawner.GoblinPrefab : null;
        if (prefab == null)
        {
            Debug.LogWarning("QuestManager: missing goblin prefab, cannot start invasion.", this);
            return;
        }

        Vector3 spawnPosition = ResolveInvasionSpawnPosition(questGiver, spawner);
        Vector3 lookPoint = ResolvePlayerPosition(questGiver);
        Vector3 facing = lookPoint - spawnPosition;
        facing.y = 0f;
        Quaternion rotation = facing.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(facing)
            : Quaternion.identity;

        GameObject instance = spawner.SpawnOne(spawnPosition, rotation, registerWithDemo: false);
        if (instance == null)
            return;

        instance.name = "InvadingGoblin";
        invadingGoblin = instance.GetComponent<Monster>();
        if (invadingGoblin == null)
            return;

        invadingGoblin.Died += OnInvadingGoblinDied;
        invadingGoblin.SetMaxHp(InvasionMaxHp);

        Goblin goblin = invadingGoblin as Goblin;
        Transform playerTransform = Player.ActiveInstance != null
            ? Player.ActiveInstance.transform
            : null;
        if (goblin != null && playerTransform != null)
            goblin.ForceEngage(playerTransform, InvasionLoseRadius, InvasionLeashRadius);
    }

    private void OnInvadingGoblinDied()
    {
        UnsubscribeInvader();

        if (huntGoblinStatus != Status.Active || huntGoblin == null)
            return;

        huntGoblinProgress = huntGoblin.requiredCount;
        huntGoblinStatus = Status.Completed;
        QuestTrackerUI.Ensure().ShowProgress(huntGoblin, huntGoblinProgress, huntGoblin.requiredCount, completed: true);
    }

    private void UnsubscribeInvader()
    {
        if (invadingGoblin == null)
            return;

        invadingGoblin.Died -= OnInvadingGoblinDied;
        invadingGoblin = null;
    }

    private static Vector3 ResolveInvasionSpawnPosition(Transform questGiver, GoblinSpawner camp)
    {
        Vector3 playerPosition = ResolvePlayerPosition(questGiver);
        Vector3 origin = questGiver != null ? questGiver.position : playerPosition;
        Vector3 fromWilds = camp != null ? camp.transform.position - origin : Vector3.zero;
        fromWilds.y = 0f;

        if (fromWilds.sqrMagnitude < 0.01f)
        {
            fromWilds = questGiver != null ? -questGiver.forward : Vector3.forward;
            fromWilds.y = 0f;
        }

        Vector3 spawn = playerPosition + fromWilds.normalized * InvasionSpawnDistance;
        spawn.y = origin.y;
        return spawn;
    }

    private static Vector3 ResolvePlayerPosition(Transform fallback)
    {
        Player player = Player.ActiveInstance;
        if (player != null)
            return player.transform.position;

        return fallback != null ? fallback.position : Vector3.zero;
    }
}
