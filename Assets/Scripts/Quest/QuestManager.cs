using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时任务中心：接取、追踪进度、完成。狗骑士先讨伐入侵者，再清剿林地，最后讨伐巨龙。
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
    private const int InvasionRupeeReward = 40;
    private const float SecondQuestDialogueDelay = 0.7f;
    private const float SwordHintDialogueDelay = 0.65f;
    private const float ThirdQuestDialogueDelay = 0.12f;
    private const float DragonLookZoomInSeconds = 2.75f;
    private const float DragonLookHoldSeconds = 1.25f;
    private const float DragonLookZoomOutSeconds = 1.45f;
    private const float VictoryDelaySeconds = 0.85f;
    private const float ForestEdgeInset = 1.5f;
    private const string KnightSpeakerName = "犬骑士";
    private const string ForestEdgeGoblinName = "ForestEdgeGoblin";
    private const int ForestShortSwordItemId = 2;

    private static readonly string[] RupeeShopDialogue =
    {
        "另外这只入侵者身上搜出了 " + InvasionRupeeReward + " 卢比。",
        "卢比可以到我这儿的商店购买物品，缺补给就回来找我。"
    };

    private static readonly string[] SecondQuestDialogue =
    {
        "干得漂亮，旅人。这一只只是先锋。",
        "林地里还盘着一窝哥布林，不能留它们继续作乱。",
        "帮我把它们全部击败！"
    };

    private static readonly string[] SwordHintDialogue =
    {
        "这只只是林地外围的哨兵。",
        "你背包里有一把剑。按 I 打开背包，点那把剑装备上。",
        "用它去清剿剩下的哥布林！"
    };

    private static readonly string[] DragonQuestDialogue =
    {
        "林地已经清净了……但真正的威胁还在那边。",
        "树桩上的巨龙盘踞不去。去击败它，这片土地才能安宁！",
        "巨龙很强大，可以购买一些武器和药剂之后再去战斗。"
    };

    public static QuestManager Instance { get; private set; }

    private Transform questGiver;
    private QuestDefinition huntGoblin;
    private Status huntGoblinStatus = Status.Inactive;
    private int huntGoblinProgress;
    private Monster invadingGoblin;
    private Coroutine invasionRoutine;
    private Coroutine secondQuestRoutine;
    private Coroutine swordHintRoutine;
    private Coroutine thirdQuestRoutine;
    private Coroutine victoryRoutine;

    private QuestDefinition huntAllGoblins;
    private Status huntAllStatus = Status.Inactive;
    private int huntAllProgress;
    private readonly List<Goblin> trackedCampGoblins = new List<Goblin>();
    private readonly HashSet<int> countedCampGoblinIds = new HashSet<int>();
    private Monster forestEdgeGoblin;
    private Inventory watchedInventory;
    private bool swordHintShown;
    private int forestGoblinKillCount;
    private int startingWeaponId;

    private QuestDefinition slayDragon;
    private Status slayDragonStatus = Status.Inactive;
    private int slayDragonProgress;
    private Monster trackedDragon;

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
        if (questId == QuestIds.DefeatAllGoblins)
            return huntAllStatus;
        if (questId == QuestIds.SlayDragon)
            return slayDragonStatus;

        return Status.Inactive;
    }

    public bool HasActiveQuest =>
        huntGoblinStatus == Status.Active
        || huntAllStatus == Status.Active
        || slayDragonStatus == Status.Active;

    public bool IsForestClearQuestActive => huntAllStatus == Status.Active;

    public bool BlocksDemoVictory
    {
        get
        {
            if (huntGoblinStatus == Status.Inactive
                && huntAllStatus == Status.Inactive
                && slayDragonStatus == Status.Inactive)
                return false;

            return slayDragonStatus != Status.Completed;
        }
    }

    /// <summary>
    /// 狗骑士对话结束后调用：接取任务并刷出一只朝玩家冲来的哥布林。
    /// </summary>
    public void StartHuntInvadingGoblin(Transform giver)
    {
        if (huntGoblinStatus != Status.Inactive)
            return;

        questGiver = giver;
        CacheStartingWeapon();
        BindInventoryWatch();
        huntGoblin = QuestDefinition.HuntInvadingGoblin();
        huntGoblinStatus = Status.Active;
        huntGoblinProgress = 0;

        QuestTrackerUI.Ensure().ShowQuest(huntGoblin, huntGoblinProgress, huntGoblin.requiredCount);

        if (invasionRoutine != null)
            StopCoroutine(invasionRoutine);

        invasionRoutine = StartCoroutine(SpawnInvadingGoblinSoon(giver));
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
        UnsubscribeForestEdgeGoblin();
        UnsubscribeCampGoblins();
        UnsubscribeDragon();
        UnbindInventoryWatch();

        if (swordHintRoutine != null)
            StopCoroutine(swordHintRoutine);
        if (thirdQuestRoutine != null)
            StopCoroutine(thirdQuestRoutine);
        if (victoryRoutine != null)
            StopCoroutine(victoryRoutine);
    }

    private IEnumerator SpawnInvadingGoblinSoon(Transform giver)
    {
        yield return new WaitForSeconds(InvasionSpawnDelay);
        SpawnInvadingGoblin(giver);
        yield return null;
        MarkInvaderGuaranteedWeaponDrop();
        invasionRoutine = null;
    }

    private void SpawnInvadingGoblin(Transform giver)
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

        Vector3 spawnPosition = ResolveInvasionSpawnPosition(giver, spawner);
        Vector3 lookPoint = ResolvePlayerPosition(giver);
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
        invadingGoblin.SetRupeeRewardOnDeath(InvasionRupeeReward);
        MarkInvaderGuaranteedWeaponDrop();

        Goblin goblin = invadingGoblin as Goblin;
        Transform playerTransform = Player.Instance != null
            ? Player.Instance.transform
            : null;
        if (goblin != null && playerTransform != null)
            goblin.ForceEngage(playerTransform, InvasionLoseRadius, InvasionLeashRadius);
    }

    private void MarkInvaderGuaranteedWeaponDrop()
    {
        Goblin goblin = invadingGoblin as Goblin;
        if (goblin != null)
            goblin.ForceGuaranteedBestWeaponDrop();
    }

    private void OnInvadingGoblinDied()
    {
        UnsubscribeInvader();

        if (huntGoblinStatus != Status.Active || huntGoblin == null)
            return;

        huntGoblinProgress = huntGoblin.requiredCount;
        huntGoblinStatus = Status.Completed;
        QuestTrackerUI.Ensure().ShowProgress(huntGoblin, huntGoblinProgress, huntGoblin.requiredCount, completed: true);

        if (secondQuestRoutine != null)
            StopCoroutine(secondQuestRoutine);

        secondQuestRoutine = StartCoroutine(OfferSecondQuestSoon());
    }

    private IEnumerator OfferSecondQuestSoon()
    {
        yield return new WaitForSeconds(SecondQuestDialogueDelay);
        secondQuestRoutine = null;

        if (huntAllStatus != Status.Inactive)
            yield break;

        DialogueUI.Ensure().Show(KnightSpeakerName, SecondQuestDialogue, PublishSecondQuestThenRupeeHint);
    }

    private void PublishSecondQuestThenRupeeHint()
    {
        if (huntAllStatus != Status.Inactive)
            return;

        StartDefeatAllGoblins();

        if (huntAllStatus != Status.Active)
            return;

        DialogueUI.Ensure().Show(KnightSpeakerName, RupeeShopDialogue, null, true);
    }

    /// <summary>
    /// 接取清剿林地。offerNextQuest 为 false 时不播下一段过场，读档还原用。
    /// </summary>
    private void StartDefeatAllGoblins(bool offerNextQuest = true)
    {
        if (huntAllStatus != Status.Inactive)
            return;

        SpawnForestEdgeGoblin();
        CollectLivingCampGoblins();
        BindInventoryWatch();
        int remaining = trackedCampGoblins.Count;
        if (remaining <= 0)
        {
            huntAllGoblins = QuestDefinition.DefeatAllGoblins(1);
            huntAllStatus = Status.Completed;
            huntAllProgress = 1;
            QuestTrackerUI.Ensure().ShowProgress(huntAllGoblins, huntAllProgress, huntAllGoblins.requiredCount, completed: true);
            NotifyDragonToDescend();
            BgmManager.NotifyForestClearQuestChanged();
            if (offerNextQuest)
                BeginThirdQuestOffer();
            return;
        }

        huntAllGoblins = QuestDefinition.DefeatAllGoblins(remaining);
        huntAllStatus = Status.Active;
        huntAllProgress = 0;
        QuestTrackerUI.Ensure().ShowQuest(huntAllGoblins, huntAllProgress, huntAllGoblins.requiredCount);
        BgmManager.NotifyForestClearQuestChanged();
    }

    private void CollectLivingCampGoblins(bool resetKillCount = true)
    {
        UnsubscribeCampGoblins();
        countedCampGoblinIds.Clear();
        if (resetKillCount)
            forestGoblinKillCount = 0;

        Goblin[] goblins = FindObjectsOfType<Goblin>();
        for (int i = 0; i < goblins.Length; i++)
        {
            Goblin goblin = goblins[i];
            if (goblin == null || goblin.IsDead)
                continue;

            if (goblin.name == "InvadingGoblin")
                continue;

            trackedCampGoblins.Add(goblin);
            goblin.Died += OnCampGoblinDied;
        }
    }

    private void OnCampGoblinDied()
    {
        Goblin killed = TakeNewlyDeadCampGoblin();
        if (killed != null)
            forestGoblinKillCount++;

        TryOfferSwordHint();

        if (huntAllStatus != Status.Active || huntAllGoblins == null)
            return;

        huntAllProgress = Mathf.Min(huntAllGoblins.requiredCount, huntAllProgress + 1);
        bool completed = huntAllProgress >= huntAllGoblins.requiredCount;
        QuestTrackerUI.Ensure().ShowProgress(
            huntAllGoblins,
            huntAllProgress,
            huntAllGoblins.requiredCount,
            completed);

        if (!completed)
            return;

        huntAllStatus = Status.Completed;
        UnsubscribeCampGoblins();
        NotifyDragonToDescend();
        BgmManager.NotifyForestClearQuestChanged();
        BeginThirdQuestOffer();
    }

    private void SpawnForestEdgeGoblin()
    {
        if (forestEdgeGoblin != null && !forestEdgeGoblin.IsDead)
            return;

        GoblinSpawner spawner = FindObjectOfType<GoblinSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("QuestManager: missing goblin spawner, cannot place forest-edge goblin.", this);
            return;
        }

        Vector3 spawnPosition = ResolveForestEdgeSpawnPosition(spawner);
        Vector3 lookPoint = ResolvePlayerPosition(questGiver);
        Vector3 facing = lookPoint - spawnPosition;
        facing.y = 0f;
        Quaternion rotation = facing.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(facing)
            : Quaternion.identity;

        GameObject instance = spawner.SpawnOne(spawnPosition, rotation, registerWithDemo: true);
        if (instance == null)
            return;

        instance.name = ForestEdgeGoblinName;
        forestEdgeGoblin = instance.GetComponent<Monster>();
    }

    private void TryOfferSwordHint()
    {
        if (swordHintShown)
            return;

        if (huntAllStatus != Status.Active && huntAllStatus != Status.Completed)
            return;

        if (forestGoblinKillCount <= 0)
            return;

        if (!IsUsingStartingWeapon() || !HasForestShortSwordInInventory())
            return;

        swordHintShown = true;
        if (swordHintRoutine != null)
            StopCoroutine(swordHintRoutine);

        swordHintRoutine = StartCoroutine(ShowSwordHintSoon());
    }

    private IEnumerator ShowSwordHintSoon()
    {
        yield return new WaitForSeconds(SwordHintDialogueDelay);
        while (DialogueUI.IsOpen || ShopUI.IsOpen || GameplayPauseMenu.IsOpen)
            yield return null;

        swordHintRoutine = null;
        if (!IsUsingStartingWeapon() || !HasForestShortSwordInInventory())
        {
            swordHintShown = false;
            yield break;
        }

        DialogueUI.Ensure().Show(KnightSpeakerName, SwordHintDialogue, null, true);
    }

    private void CacheStartingWeapon()
    {
        startingWeaponId = ReadEquippedWeaponId();
    }

    private bool IsUsingStartingWeapon()
    {
        return ReadEquippedWeaponId() == startingWeaponId;
    }

    private static bool HasForestShortSwordInInventory()
    {
        Player player = Player.Resolve();
        Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
        return inventory != null && inventory.CountOf(ForestShortSwordItemId) > 0;
    }

    private static int ReadEquippedWeaponId()
    {
        Player player = Player.Resolve();
        ItemSO weapon = player != null ? player.EquippedWeapon : null;
        return weapon != null ? weapon.id : 0;
    }

    private Goblin TakeNewlyDeadCampGoblin()
    {
        for (int i = 0; i < trackedCampGoblins.Count; i++)
        {
            Goblin goblin = trackedCampGoblins[i];
            if (goblin == null || !goblin.IsDead)
                continue;

            if (!countedCampGoblinIds.Add(goblin.GetInstanceID()))
                continue;

            return goblin;
        }

        return null;
    }

    private void BindInventoryWatch()
    {
        Player player = Player.Resolve();
        Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
        if (inventory == watchedInventory)
            return;

        UnbindInventoryWatch();
        watchedInventory = inventory;
        if (watchedInventory != null)
            watchedInventory.Changed += OnWatchedInventoryChanged;
    }

    private void UnbindInventoryWatch()
    {
        if (watchedInventory == null)
            return;

        watchedInventory.Changed -= OnWatchedInventoryChanged;
        watchedInventory = null;
    }

    private void OnWatchedInventoryChanged()
    {
        TryOfferSwordHint();
    }

    private void UnsubscribeForestEdgeGoblin()
    {
        forestEdgeGoblin = null;
    }

    private static Vector3 ResolveForestEdgeSpawnPosition(GoblinSpawner camp)
    {
        AreaBgmZone forest = FindForestZone();
        Vector3 villageAnchor = ResolveVillageAnchor();
        Vector3 spawn;

        if (forest != null)
        {
            spawn = forest.GetPerimeterPointToward(villageAnchor);
            Vector3 inward = forest.ZoneCenter - spawn;
            inward.y = 0f;
            if (inward.sqrMagnitude > 0.0001f)
                spawn += inward.normalized * ForestEdgeInset;
        }
        else
        {
            Vector3 campPosition = camp != null ? camp.transform.position : villageAnchor;
            Vector3 toCamp = campPosition - villageAnchor;
            toCamp.y = 0f;
            float distance = toCamp.magnitude;
            spawn = distance > 0.01f
                ? villageAnchor + toCamp / distance * (distance * 0.55f)
                : villageAnchor;
        }

        Vector3 rayOrigin = spawn + Vector3.up * 4f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            spawn.y = hit.point.y;
        else
            spawn.y = villageAnchor.y;

        return spawn;
    }

    private static AreaBgmZone FindForestZone()
    {
        AreaBgmZone[] zones = FindObjectsOfType<AreaBgmZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].Kind == AreaBgmZone.AreaKind.Forest)
                return zones[i];
        }

        return null;
    }

    private static Vector3 ResolveVillageAnchor()
    {
        AreaBgmZone[] zones = FindObjectsOfType<AreaBgmZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].Kind == AreaBgmZone.AreaKind.Village)
                return zones[i].ZoneCenter;
        }

        return ResolvePlayerPosition(null);
    }

    private void BeginThirdQuestOffer()
    {
        if (slayDragonStatus != Status.Inactive)
            return;

        if (thirdQuestRoutine != null)
            StopCoroutine(thirdQuestRoutine);

        thirdQuestRoutine = StartCoroutine(OfferDragonQuestSoon());
    }

    private static void NotifyDragonToDescend()
    {
        DragonBoss dragon = FindObjectOfType<DragonBoss>();
        if (dragon != null)
            dragon.BeginDescent();
    }

    private IEnumerator OfferDragonQuestSoon()
    {
        thirdQuestRoutine = null;

        if (slayDragonStatus != Status.Inactive)
            yield break;

        DragonBoss dragon = FindObjectOfType<DragonBoss>();
        if (dragon != null && !dragon.IsDead)
        {
            Player player = Player.Resolve();
            if (player != null)
                player.SetConversationLocked(true);

            LookCameraAtDragon(dragon);
            yield return new WaitForSeconds(DragonLookZoomInSeconds + DragonLookHoldSeconds);
        }
        else
        {
            yield return new WaitForSeconds(ThirdQuestDialogueDelay);
        }

        if (slayDragonStatus != Status.Inactive)
            yield break;

        DialogueUI.Ensure().Show(KnightSpeakerName, DragonQuestDialogue, StartSlayDragon, true);
    }

    private static void LookCameraAtDragon(DragonBoss dragon)
    {
        if (dragon == null)
            return;

        ThirdPersonCameraRig rig = FindObjectOfType<ThirdPersonCameraRig>();
        if (rig == null)
            return;

        rig.LookAtFollow(
            dragon.transform,
            dragon.CinematicLookOffset,
            DragonLookZoomInSeconds,
            DragonLookHoldSeconds,
            DragonLookZoomOutSeconds,
            12f,
            16.5f,
            0.2f);
    }

    private void StartSlayDragon()
    {
        if (slayDragonStatus != Status.Inactive)
            return;

        slayDragon = QuestDefinition.SlayDragon();
        DragonBoss dragon = FindObjectOfType<DragonBoss>();
        if (dragon == null || dragon.IsDead)
        {
            slayDragonStatus = Status.Completed;
            slayDragonProgress = slayDragon.requiredCount;
            QuestTrackerUI.Ensure().ShowProgress(slayDragon, slayDragonProgress, slayDragon.requiredCount, completed: true);
            BeginVictorySoon(VictoryDelaySeconds);
            return;
        }

        slayDragonStatus = Status.Active;
        slayDragonProgress = 0;
        QuestTrackerUI.Ensure().ShowQuest(slayDragon, slayDragonProgress, slayDragon.requiredCount);

        trackedDragon = dragon;
        trackedDragon.Died += OnDragonDied;
        dragon.BeginDescent();
    }

    private void OnDragonDied()
    {
        float victoryDelay = ResolveVictoryDelay(trackedDragon);
        UnsubscribeDragon();

        if (slayDragonStatus != Status.Active || slayDragon == null)
            return;

        slayDragonProgress = slayDragon.requiredCount;
        slayDragonStatus = Status.Completed;
        QuestTrackerUI.Ensure().ShowProgress(slayDragon, slayDragonProgress, slayDragon.requiredCount, completed: true);
        BeginVictorySoon(victoryDelay);
    }

    private static float ResolveVictoryDelay(Monster dragon)
    {
        DragonBoss boss = dragon as DragonBoss;
        if (boss != null)
            return boss.DeathPresentationSeconds;

        return VictoryDelaySeconds;
    }

    private void BeginVictorySoon(float delaySeconds)
    {
        if (victoryRoutine != null)
            StopCoroutine(victoryRoutine);

        victoryRoutine = StartCoroutine(ShowVictorySoon(delaySeconds));
    }

    private IEnumerator ShowVictorySoon(float delaySeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, delaySeconds));
        victoryRoutine = null;

        if (DemoGameManager.Instance != null)
            DemoGameManager.Instance.NotifyBossDefeated();
    }

    private void UnsubscribeDragon()
    {
        if (trackedDragon == null)
            return;

        trackedDragon.Died -= OnDragonDied;
        trackedDragon = null;
    }

    private void UnsubscribeInvader()
    {
        if (invadingGoblin == null)
            return;

        invadingGoblin.Died -= OnInvadingGoblinDied;
        invadingGoblin = null;
    }

    private void UnsubscribeCampGoblins()
    {
        for (int i = 0; i < trackedCampGoblins.Count; i++)
        {
            Goblin goblin = trackedCampGoblins[i];
            if (goblin != null)
                goblin.Died -= OnCampGoblinDied;
        }

        trackedCampGoblins.Clear();
        countedCampGoblinIds.Clear();
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
        Player player = Player.Instance;
        if (player != null)
            return player.transform.position;

        return fallback != null ? fallback.position : Vector3.zero;
    }

    /// <summary>采集三条主线任务进度，以及犬骑士是否已经打过招呼。</summary>
    public QuestSaveData CaptureSaveData()
    {
        DogKnightNpc knight = FindObjectOfType<DogKnightNpc>();
        return new QuestSaveData
        {
            huntGoblinStatus = (int)huntGoblinStatus,
            huntGoblinProgress = huntGoblinProgress,
            huntAllStatus = (int)huntAllStatus,
            huntAllProgress = huntAllProgress,
            huntAllRequiredCount = huntAllGoblins != null ? huntAllGoblins.requiredCount : 0,
            slayDragonStatus = (int)slayDragonStatus,
            slayDragonProgress = slayDragonProgress,
            swordHintShown = swordHintShown,
            startingWeaponId = startingWeaponId,
            forestGoblinKillCount = forestGoblinKillCount,
            knightGreeted = knight != null && knight.HasGreeted,
        };
    }

    /// <summary>
    /// 还原任务状态。世界单位应已先按存档处理过；这里补刷入侵者/林边哨兵并接上追踪。
    /// 不重放任务过场对话。
    /// </summary>
    public void ApplySaveData(QuestSaveData data, WorldSaveData world)
    {
        if (data == null)
            return;

        DogKnightNpc knight = FindObjectOfType<DogKnightNpc>();
        if (knight != null)
        {
            knight.RestoreGreeting(data.knightGreeted);
            questGiver = knight.transform;
        }

        swordHintShown = data.swordHintShown;
        startingWeaponId = data.startingWeaponId;
        forestGoblinKillCount = Mathf.Max(0, data.forestGoblinKillCount);
        huntGoblinProgress = Mathf.Max(0, data.huntGoblinProgress);
        huntAllProgress = Mathf.Max(0, data.huntAllProgress);
        slayDragonProgress = Mathf.Max(0, data.slayDragonProgress);
        huntGoblinStatus = ClampStatus(data.huntGoblinStatus);
        huntAllStatus = ClampStatus(data.huntAllStatus);
        slayDragonStatus = ClampStatus(data.slayDragonStatus);

        if (data.knightGreeted || huntGoblinStatus != Status.Inactive)
            BindInventoryWatch();

        RestoreHuntInvader(world);
        RestoreForestQuest(data, world);
        RestoreDragonQuest();
        RefreshTrackerFromState();
    }

    private static Status ClampStatus(int value)
    {
        if (value <= (int)Status.Inactive)
            return Status.Inactive;
        if (value >= (int)Status.Completed)
            return Status.Completed;
        return Status.Active;
    }

    /// <summary>任务进行中则补刷入侵哥布林，再套存档里的血量和位置。</summary>
    private void RestoreHuntInvader(WorldSaveData world)
    {
        if (huntGoblinStatus == Status.Inactive)
            return;

        huntGoblin = QuestDefinition.HuntInvadingGoblin();
        if (huntGoblinStatus != Status.Active)
            return;

        SpawnInvadingGoblin(questGiver);
        ApplySavedMonster(invadingGoblin, WorldSaveState.FindActor(world, WorldSaveState.InvadingGoblinId));
        MarkInvaderGuaranteedWeaponDrop();
    }

    /// <summary>
    /// 还原清剿林地。未接取但第一任务已完成时直接接上，不重放骑士对话。
    /// </summary>
    private void RestoreForestQuest(QuestSaveData data, WorldSaveData world)
    {
        if (huntAllStatus == Status.Inactive)
        {
            if (huntGoblinStatus == Status.Completed)
                StartDefeatAllGoblins(offerNextQuest: false);
            return;
        }

        int required = Mathf.Max(1, data.huntAllRequiredCount);
        huntAllGoblins = QuestDefinition.DefeatAllGoblins(required);
        huntAllProgress = Mathf.Clamp(huntAllProgress, 0, required);

        if (huntAllStatus == Status.Completed)
        {
            BgmManager.NotifyForestClearQuestChanged();
            return;
        }

        if (huntAllStatus == Status.Active)
        {
            WorldActorSaveData edgeSave = WorldSaveState.FindActor(world, WorldSaveState.ForestEdgeGoblinId);
            if (edgeSave == null || edgeSave.alive)
            {
                SpawnForestEdgeGoblin();
                ApplySavedMonster(forestEdgeGoblin, edgeSave);
            }
        }

        CollectLivingCampGoblins(resetKillCount: false);
        BgmManager.NotifyForestClearQuestChanged();
    }

    /// <summary>还原讨伐巨龙。跳过落地过场，避免每次读档都播镜头。</summary>
    private void RestoreDragonQuest()
    {
        if (slayDragonStatus == Status.Inactive)
        {
            if (huntAllStatus == Status.Completed)
                RestoreSlayDragonSilent();
            return;
        }

        RestoreSlayDragonSilent();
    }

    /// <summary>按存档接上巨龙任务并订阅死亡。龙已死则视为完成，必要时再弹胜利。</summary>
    private void RestoreSlayDragonSilent()
    {
        if (slayDragonStatus == Status.Inactive)
            slayDragonStatus = Status.Active;

        slayDragon = QuestDefinition.SlayDragon();
        DragonBoss dragon = FindObjectOfType<DragonBoss>();
        if (dragon == null || dragon.IsDead)
        {
            slayDragonStatus = Status.Completed;
            slayDragonProgress = slayDragon.requiredCount;
            if (DemoGameManager.Instance == null || !DemoGameManager.Instance.HasEnded)
                BeginVictorySoon(VictoryDelaySeconds);
            return;
        }

        if (slayDragonStatus == Status.Completed)
            return;

        slayDragonStatus = Status.Active;
        slayDragonProgress = 0;
        trackedDragon = dragon;
        trackedDragon.Died += OnDragonDied;
        dragon.BeginDescent();
    }

    /// <summary>按当前任务状态刷新右上角追踪条。</summary>
    private void RefreshTrackerFromState()
    {
        QuestTrackerUI tracker = QuestTrackerUI.Ensure();
        if (slayDragonStatus == Status.Active && slayDragon != null)
        {
            tracker.ShowQuest(slayDragon, slayDragonProgress, slayDragon.requiredCount);
            return;
        }

        if (slayDragonStatus == Status.Completed && slayDragon != null)
        {
            tracker.ShowProgress(slayDragon, slayDragonProgress, slayDragon.requiredCount, completed: true);
            return;
        }

        if (huntAllStatus == Status.Active && huntAllGoblins != null)
        {
            tracker.ShowQuest(huntAllGoblins, huntAllProgress, huntAllGoblins.requiredCount);
            return;
        }

        if (huntAllStatus == Status.Completed && huntAllGoblins != null)
        {
            tracker.ShowProgress(huntAllGoblins, huntAllProgress, huntAllGoblins.requiredCount, completed: true);
            return;
        }

        if (huntGoblinStatus == Status.Active && huntGoblin != null)
        {
            tracker.ShowQuest(huntGoblin, huntGoblinProgress, huntGoblin.requiredCount);
            return;
        }

        if (huntGoblinStatus == Status.Completed && huntGoblin != null)
        {
            tracker.ShowProgress(huntGoblin, huntGoblinProgress, huntGoblin.requiredCount, completed: true);
            return;
        }

        tracker.Hide();
    }

    /// <summary>给刚补刷的单位套存档血量和坐标；记录为已死亡则静默移除。</summary>
    private static void ApplySavedMonster(Monster monster, WorldActorSaveData saved)
    {
        if (monster == null || saved == null)
            return;

        if (!saved.alive)
        {
            monster.RemoveForSaveRestore();
            return;
        }

        monster.SetCurrentHp(saved.hp);
        CharacterController controller = monster.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        monster.transform.SetPositionAndRotation(
            new Vector3(saved.x, saved.y, saved.z),
            Quaternion.Euler(0f, saved.rotY, 0f));

        if (controller != null)
            controller.enabled = true;
    }
}
