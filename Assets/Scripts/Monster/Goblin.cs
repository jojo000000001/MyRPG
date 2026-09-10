using System.Collections;
using UnityEngine;

/// <summary>
/// 敌人的有限状态机：负责巡逻、追击、攻击、返回出生点和死亡流程。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Goblin : Monster, IPoolable
{
    // 目标检测相关参数：控制发现、丢失、追击范围以及视野/视线判断。
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRadius = 7f;
    [SerializeField] private float loseRadius = 11f;
    [SerializeField] private float leashRadius = 14f;
    [SerializeField] private float fieldOfView = 140f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask lineOfSightBlockers = ~0;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.2f, 0f);

    // 巡逻参数：控制随机巡逻半径、到点等待时间和到达判定。
    [Header("Patrol")]
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float waitAtPointMin = 0.75f;
    [SerializeField] private float waitAtPointMax = 2.5f;
    [SerializeField] private float arrivalThreshold = 0.2f;
    [SerializeField] private int patrolSampleAttempts = 8;
    [SerializeField] private float obstacleRepickDelay = 0.25f;
    [SerializeField, Range(0.05f, 0.8f)] private float minMoveProgressRatio = 0.2f;
    [SerializeField] private float detourDistance = 2.6f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    // 移动参数：分别控制巡逻、追击、返程速度，以及转向和重力。
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 3.4f;
    [SerializeField] private float returnSpeed = 3.2f;
    [SerializeField] private float rotateSpeed = 540f;
    [SerializeField] private float gravity = -10f;
    [SerializeField] private bool disableRootCapsuleCollider = true;

    // 攻击参数：控制伤害、攻击距离、前摇、冷却和动画触发器。
    [Header("Attack")]
    [SerializeField] private int attackDamage = 8;
    [SerializeField] private DamageType attackDamageType = DamageType.Physical;
    [SerializeField] private float attackRange = 1.35f;
    [SerializeField] private float attackHitGraceRange = 0.35f;
    [SerializeField, Range(1f, 360f)] private float attackArcDegrees = 120f;
    [SerializeField] private float attackCooldown = 1.25f;
    [SerializeField] private float attackWindupSeconds = 0.25f;
    [SerializeField] private float attackLockSeconds = 0.55f;
    [SerializeField] private string attackTriggerParam = "";
    [SerializeField] private string attackIndexParam = "AttackIndex";
    [SerializeField] private int smashDamageBonus = 4;
    [SerializeField] private float smashWindupSeconds = 0.68f;
    [SerializeField] private float smashLockSeconds = 1.32f;
    [SerializeField] private float smashCooldown = 1.9f;
    [SerializeField] private float smashEngageRange = 2.15f;
    [SerializeField] private float smashHitGraceRange = 0.45f;
    [SerializeField, Range(1f, 360f)] private float smashArcDegrees = 150f;
    [SerializeField] private float smashCommitFacingSeconds = 0.28f;
    [SerializeField] private float smashLungeDistance = 1.4f;
    [SerializeField] private float smashLungeSeconds = 0.16f;

    // 死亡参数：控制死亡后对象销毁延迟。
    [Header("Death")]
    [SerializeField] private float destroyAfterDeathSeconds = 0.5f;

    [Header("Drops")]
    [SerializeField, Range(0f, 1f)] private float dropChance = 1f;
    [SerializeField, Range(0f, 1f)] private float weaponDropChance = 0.15f;
    [SerializeField] private string consumableDropResourcePath = "Drops/Consumables";
    [SerializeField] private string weaponDropResourcePath = "Drops/Weapons";
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private float dropScatterRadius = 0.35f;
    [SerializeField] private float questWeaponDropDistance = 2.6f;
    [SerializeField] private GameObject guaranteedWeaponDropPrefab;
    [SerializeField] private string guaranteedWeaponDropResourcePath = "Drops/Weapons/PF_Drop_Weapon_Sword02";

    [Header("Hit Reaction")]
    [SerializeField] private float hitStaggerSeconds = 0.12f;
    [SerializeField] private float hitKnockbackSpeed = 2.2f;
    [SerializeField] private float hitKnockbackUpSpeed = 0.25f;
    [SerializeField] private float hitKnockbackDamping = 14f;

    // 状态机的全部状态；Update 会根据 currentState 分发到对应行为。
    private enum State
    {
        PatrolWait,   // 原地等待，等待计时结束或发现目标
        PatrolMove,   // 向随机巡逻点移动
        Chase,        // 追击目标
        Attack,       // 攻击目标并处理攻击前摇/锁定时间
        ReturnHome,   // 超出仇恨或活动范围后返回出生点
        Dead          // 死亡后停止行为
    }

    private enum MoveResult
    {
        Moving,
        Arrived,
        Blocked
    }

    // 当前状态保留为可序列化字段，便于在 Inspector 中观察调试。
    [SerializeField] private State currentState = State.PatrolWait;

    // 运行时缓存和计时数据。
    private CharacterController characterController;
    private Vector3 home;
    private Vector3 patrolTarget;
    private Vector3 moveDetour;
    private bool hasMoveDetour;
    private float blockedStartedAt = -1f;
    private Vector3 lastBlockedDirection;
    private readonly Collider[] obstacleOverlap = new Collider[12];
    private Vector3 verticalVelocity;
    private float waitUntil;
    private float attackStartedAt = -999f;
    private float nextAttackAt = -999f;
    private bool attackDamageApplied;
    private int nextAttackIndex;
    private int currentAttackIndex;
    private const int SlashAttackIndex = 0;
    private const int SmashAttackIndex = 1;
    private Vector3 hitKnockbackVelocity;
    private float hitStaggerUntil = -999f;
    private Vector3 smashLungeDirection = Vector3.forward;
    private bool smashFacingLocked;
    private bool lootDropped;
    private bool guaranteedBestWeaponDrop;
    private bool spawnInitialized;
    private Coroutine releaseRoutine;
    private float prefabDetectRadius;
    private float prefabLoseRadius;
    private float prefabLeashRadius;

    // 对外暴露当前状态名称，方便 UI、调试面板或测试读取。
    public string CurrentStateName => currentState.ToString();

    // 初始化依赖组件，并按配置关闭根节点胶囊碰撞体，避免和 CharacterController 重复碰撞。
    protected override void Awake()
    {
        base.Awake();

        characterController = GetComponent<CharacterController>();

        if (disableRootCapsuleCollider)
        {
            var capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
                capsuleCollider.enabled = false;
        }

        prefabDetectRadius = detectRadius;
        prefabLoseRadius = loseRadius;
        prefabLeashRadius = leashRadius;
    }

    private void Start()
    {
        if (!spawnInitialized)
            ResetForSpawn();
    }

    public void OnSpawnedFromPool()
    {
        ResetForSpawn();
    }

    /// <summary>
    /// 立刻以指定目标进入追击，用于任务刷怪等需要强制入侵的场合。
    /// </summary>
    public void ForceEngage(Transform engageTarget, float chaseLoseRadius = -1f, float chaseLeashRadius = -1f)
    {
        if (engageTarget == null || IsDead)
            return;

        target = engageTarget;
        if (chaseLoseRadius > 0f)
            loseRadius = chaseLoseRadius;
        if (chaseLeashRadius > 0f)
            leashRadius = Mathf.Max(loseRadius, chaseLeashRadius);
        EnterState(State.Chase);
    }

    /// <summary>
    /// 死后必定掉落当前武器掉落池里攻击力最高的那把，用于首个任务入侵哥布林。
    /// </summary>
    public void ForceGuaranteedBestWeaponDrop()
    {
        guaranteedBestWeaponDrop = true;
    }

    /// <summary>读档移除已死亡哥布林：不掉落，直接还回对象池。</summary>
    public override void RemoveForSaveRestore()
    {
        UnregisterWithoutDeath();
        BgmManager.NotifyGoblinDied(GetInstanceID());

        if (characterController != null)
            characterController.enabled = false;

        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        StopAllCoroutines();

        if (!PooledObject.TryRelease(gameObject))
            Destroy(gameObject);
    }

    public void OnReturnedToPool()
    {
        BgmManager.NotifyGoblinDisengaged(GetInstanceID());

        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        StopAllCoroutines();
        currentState = State.Dead;
        verticalVelocity = Vector3.zero;
        hitKnockbackVelocity = Vector3.zero;
        guaranteedBestWeaponDrop = false;
    }

    private void ResetForSpawn()
    {
        spawnInitialized = true;
        RestorePrefabVitals();
        detectRadius = prefabDetectRadius > 0f ? prefabDetectRadius : detectRadius;
        loseRadius = prefabLoseRadius > 0f ? prefabLoseRadius : loseRadius;
        leashRadius = prefabLeashRadius > 0f ? prefabLeashRadius : leashRadius;
        lastHitTime = -999f;
        lootDropped = false;
        waitUntil = 0f;
        attackStartedAt = -999f;
        nextAttackAt = -999f;
        attackDamageApplied = false;
        nextAttackIndex = SlashAttackIndex;
        currentAttackIndex = SlashAttackIndex;
        smashFacingLocked = false;
        smashLungeDirection = Vector3.forward;
        hitKnockbackVelocity = Vector3.zero;
        hitStaggerUntil = -999f;
        verticalVelocity = Vector3.zero;
        home = transform.position;
        hasMoveDetour = false;
        blockedStartedAt = -1f;
        lastBlockedDirection = Vector3.zero;

        if (characterController != null)
            characterController.enabled = true;

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        AcquireTarget();
        EnterState(State.PatrolWait);
    }

    // 在编辑器中修正参数范围，防止 Inspector 输入导致逻辑异常。
    private void OnValidate()
    {
        detectRadius = Mathf.Max(0f, detectRadius);
        loseRadius = Mathf.Max(detectRadius, loseRadius);
        leashRadius = Mathf.Max(loseRadius, leashRadius);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
        wanderRadius = Mathf.Max(0f, wanderRadius);
        waitAtPointMin = Mathf.Max(0f, waitAtPointMin);
        waitAtPointMax = Mathf.Max(waitAtPointMin, waitAtPointMax);
        arrivalThreshold = Mathf.Max(0.01f, arrivalThreshold);
        patrolSampleAttempts = Mathf.Max(1, patrolSampleAttempts);
        obstacleRepickDelay = Mathf.Max(0.05f, obstacleRepickDelay);
        minMoveProgressRatio = Mathf.Clamp(minMoveProgressRatio, 0.05f, 0.8f);
        detourDistance = Mathf.Max(0.5f, detourDistance);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
        rotateSpeed = Mathf.Max(0f, rotateSpeed);
        attackDamage = Mathf.Max(0, attackDamage);
        attackRange = Mathf.Max(0.1f, attackRange);
        attackHitGraceRange = Mathf.Max(0f, attackHitGraceRange);
        attackArcDegrees = Mathf.Clamp(attackArcDegrees, 1f, 360f);
        attackCooldown = Mathf.Max(0.05f, attackCooldown);
        attackWindupSeconds = Mathf.Max(0f, attackWindupSeconds);
        attackLockSeconds = Mathf.Max(attackWindupSeconds, attackLockSeconds);
        smashWindupSeconds = Mathf.Max(0f, smashWindupSeconds);
        smashLockSeconds = Mathf.Max(smashWindupSeconds, smashLockSeconds);
        smashCooldown = Mathf.Max(0.05f, smashCooldown);
        smashEngageRange = Mathf.Max(attackRange, smashEngageRange);
        smashHitGraceRange = Mathf.Max(0f, smashHitGraceRange);
        smashArcDegrees = Mathf.Clamp(smashArcDegrees, 1f, 360f);
        smashCommitFacingSeconds = Mathf.Clamp(smashCommitFacingSeconds, 0f, smashWindupSeconds);
        smashLungeDistance = Mathf.Max(0f, smashLungeDistance);
        smashLungeSeconds = Mathf.Max(0.05f, smashLungeSeconds);
        smashDamageBonus = Mathf.Max(0, smashDamageBonus);
        destroyAfterDeathSeconds = Mathf.Max(0f, destroyAfterDeathSeconds);
        dropChance = Mathf.Clamp01(dropChance);
        weaponDropChance = Mathf.Clamp01(weaponDropChance);
        dropScatterRadius = Mathf.Max(0f, dropScatterRadius);
        hitStaggerSeconds = Mathf.Max(0f, hitStaggerSeconds);
        hitKnockbackSpeed = Mathf.Max(0f, hitKnockbackSpeed);
        hitKnockbackUpSpeed = Mathf.Max(0f, hitKnockbackUpSpeed);
        hitKnockbackDamping = Mathf.Max(0f, hitKnockbackDamping);
    }

    // 主循环：先处理死亡和基础更新，再按当前状态执行对应行为。
    private void Update()
    {
        if (IsDead || characterController == null)
        {
            EnterState(State.Dead);
            return;
        }

        AcquireTarget();
        ApplyGravity();

        if (Time.time < hitStaggerUntil)
        {
            SetLocomotionSpeed01(0f);
            FaceTarget();
            MoveVerticalOnly();
            return;
        }

        switch (currentState)
        {
            case State.PatrolWait:
                UpdatePatrolWait();
                break;
            case State.PatrolMove:
                UpdatePatrolMove();
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Attack:
                UpdateAttack();
                break;
            case State.ReturnHome:
                UpdateReturnHome();
                break;
            case State.Dead:
                SetLocomotionSpeed01(0f);
                break;
        }
    }

    // 受击后如果伤害有效，就把攻击来源设为目标并进入追击。
    public override bool TryTakeDamage(DamageInfo damage)
    {
        bool accepted = base.TryTakeDamage(damage);
        if (!accepted || IsDead)
            return accepted;

        if (damage.source != null)
            target = damage.source.transform;

        ApplyHitReaction(damage);
        EnterState(State.Chase);
        return true;
    }

    // 巡逻等待：静止一段随机时间，期间如果发现目标就切换到追击。
    private void UpdatePatrolWait()
    {
        SetLocomotionSpeed01(0f);

        if (CanDetectTarget())
        {
            EnterState(State.Chase);
            return;
        }

        MoveVerticalOnly();

        if (Time.time >= waitUntil)
        {
            PickPatrolTarget();
            EnterState(State.PatrolMove);
        }
    }

    // 巡逻移动：向随机巡逻点移动；撞到障碍就另选一个点。
    private void UpdatePatrolMove()
    {
        if (CanDetectTarget())
        {
            EnterState(State.Chase);
            return;
        }

        MoveResult result = MoveToward(patrolTarget, moveSpeed);
        if (result == MoveResult.Arrived)
        {
            EnterState(State.PatrolWait);
            return;
        }

        if (result == MoveResult.Blocked)
            PickPatrolTarget(lastBlockedDirection);
    }

    // 追击：保持面向目标，进入攻击范围则切换攻击，失去目标则返程。
    private void UpdateChase()
    {
        if (!HasValidTarget() || ShouldReturnHome())
        {
            EnterState(State.ReturnHome);
            return;
        }

        FaceTarget();

        if (IsTargetInAttackRange())
        {
            EnterState(State.Attack);
            return;
        }

        Vector3 destination = hasMoveDetour ? moveDetour : target.position;
        MoveResult result = MoveToward(destination, chaseSpeed);
        if (result == MoveResult.Arrived && hasMoveDetour)
        {
            ClearMoveDetour();
            return;
        }

        if (result == MoveResult.Blocked)
            PickDetourToward(target.position);
    }

    // 攻击：处理前摇伤害、攻击锁定时间和下一次攻击冷却。
    private void UpdateAttack()
    {
        SetLocomotionSpeed01(0f);

        if (!HasValidTarget() || ShouldReturnHome())
        {
            EnterState(State.ReturnHome);
            return;
        }

        float elapsed = Time.time - attackStartedAt;
        if (IsSmashAttack)
            UpdateHatSmashMotion(elapsed);
        else
        {
            FaceTarget();
            MoveVerticalOnly();
        }

        if (!attackDamageApplied && elapsed >= GetCurrentAttackWindupSeconds())
        {
            attackDamageApplied = true;
            ApplyAttackDamage();
        }

        if (elapsed < GetCurrentAttackLockSeconds())
            return;

        if (!IsTargetInAttackRange())
        {
            EnterState(State.Chase);
            return;
        }

        if (Time.time >= nextAttackAt)
            EnterState(State.Attack);
    }

    // 返程：回到出生点；如果返程途中重新发现目标，可再次进入追击。
    private void UpdateReturnHome()
    {
        if (CanDetectTarget() && GetPlanarDistance(transform.position, home) <= leashRadius)
        {
            EnterState(State.Chase);
            return;
        }

        MoveResult result = MoveToward(hasMoveDetour ? moveDetour : home, returnSpeed);
        if (result == MoveResult.Arrived)
        {
            if (hasMoveDetour)
            {
                ClearMoveDetour();
                return;
            }

            EnterState(State.PatrolWait);
            return;
        }

        if (result == MoveResult.Blocked)
            PickDetourToward(home);
    }

    // 状态切换入口：集中处理进入某个状态时需要做的一次性初始化。
    private void EnterState(State nextState)
    {
        if (currentState == nextState && nextState != State.Attack)
            return;

        State previousState = currentState;
        currentState = nextState;
        UpdateCombatMusic(previousState, nextState);
        ClearMoveDetour();
        blockedStartedAt = -1f;

        switch (currentState)
        {
            case State.PatrolWait:
                ScheduleWait();
                break;
            case State.PatrolMove:
                break;
            case State.Chase:
                break;
            case State.Attack:
                BeginAttack();
                break;
            case State.ReturnHome:
                break;
            case State.Dead:
                SetLocomotionSpeed01(0f);
                break;
        }
    }

    private static bool IsCombatState(State state)
    {
        return state == State.Chase || state == State.Attack;
    }

    private void UpdateCombatMusic(State previousState, State nextState)
    {
        bool wasCombat = IsCombatState(previousState);
        bool isCombat = IsCombatState(nextState);

        if (!wasCombat && isCombat)
            BgmManager.NotifyGoblinEngaged(GetInstanceID());
        else if (wasCombat && !isCombat)
            BgmManager.NotifyGoblinDisengaged(GetInstanceID());
    }

    // 开始一次攻击：横斩与帽子头槌轮流，帽子头槌带前扑。
    private void BeginAttack()
    {
        currentAttackIndex = nextAttackIndex;
        nextAttackIndex = currentAttackIndex == SlashAttackIndex ? SmashAttackIndex : SlashAttackIndex;
        attackStartedAt = Time.time;
        nextAttackAt = Time.time + GetCurrentAttackCooldown();
        attackDamageApplied = false;
        smashFacingLocked = false;
        CacheSmashAim();
        SetLocomotionSpeed01(0f);

        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(attackIndexParam)
            && HasAnimatorParameter(attackIndexParam, AnimatorControllerParameterType.Int))
            animator.SetInteger(attackIndexParam, currentAttackIndex);

        if (!string.IsNullOrEmpty(attackTriggerParam)
            && HasAnimatorParameter(attackTriggerParam, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(attackTriggerParam);
    }

    private bool IsSmashAttack => currentAttackIndex == SmashAttackIndex;

    private float GetCurrentAttackWindupSeconds()
    {
        return IsSmashAttack ? smashWindupSeconds : attackWindupSeconds;
    }

    private float GetCurrentAttackLockSeconds()
    {
        return IsSmashAttack ? smashLockSeconds : attackLockSeconds;
    }

    private float GetCurrentAttackCooldown()
    {
        return IsSmashAttack ? smashCooldown : attackCooldown;
    }

    // 在攻击前摇结束后尝试结算伤害，只命中仍在有效范围内的目标。
    private void ApplyAttackDamage()
    {
        if (!HasValidTarget())
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        float hitRange = GetCurrentAttackHitRange();
        if (direction.magnitude > hitRange)
            return;

        if (!IsInsideAttackArc(direction, GetCurrentAttackArcDegrees()))
            return;

        IDamageable damageable = FindDamageable(target);
        if (damageable == null)
            return;

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = transform.forward;

        int baseDamage = attackDamage + (IsSmashAttack ? smashDamageBonus : 0);
        int targetDefense = CombatDamageFormulas.GetTargetDefense(damageable, attackDamageType);
        int damageAmount = ResolveOutgoingDamage(baseDamage, targetDefense, attackDamageType, out bool isCritical);
        DamageInfo damage = new DamageInfo(damageAmount, gameObject, target.position, direction, attackDamageType, isCritical);
        if (damageable.TryTakeDamage(damage))
            TryApplyAttackLifeSteal(damageAmount);
    }

    // 水平移动到指定位置，同时应用竖直速度并同步朝向。
    private MoveResult MoveToward(Vector3 destination, float speed)
    {
        Vector3 position = transform.position;
        Vector3 toTarget = new Vector3(destination.x, position.y, destination.z) - position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= arrivalThreshold)
        {
            SetLocomotionSpeed01(0f);
            MoveVerticalOnly();
            blockedStartedAt = -1f;
            return MoveResult.Arrived;
        }

        Vector3 direction = toTarget / distance;
        RotateToward(direction);

        SetLocomotionSpeed01(speed > 0f ? 1f : 0f);
        Vector3 before = transform.position;
        CollisionFlags flags = characterController.Move((direction * speed + verticalVelocity + ConsumeHitKnockbackVelocity()) * Time.deltaTime);

        Vector3 planarDelta = transform.position - before;
        planarDelta.y = 0f;
        float expectedMove = speed * Time.deltaTime;
        if (IsMovementBlocked(flags, planarDelta.magnitude, expectedMove, distance))
        {
            lastBlockedDirection = direction;
            if (blockedStartedAt < 0f)
                blockedStartedAt = Time.time;

            if (Time.time - blockedStartedAt >= obstacleRepickDelay)
            {
                blockedStartedAt = -1f;
                return MoveResult.Blocked;
            }
        }
        else
        {
            blockedStartedAt = -1f;
        }

        return MoveResult.Moving;
    }

    private bool IsMovementBlocked(CollisionFlags flags, float progress, float expectedMove, float remainingDistance)
    {
        if (expectedMove < 0.0001f || remainingDistance <= arrivalThreshold * 2f)
            return false;

        if ((flags & CollisionFlags.Sides) == 0)
            return false;

        return progress < expectedMove * minMoveProgressRatio;
    }

    private void ClearMoveDetour()
    {
        hasMoveDetour = false;
        moveDetour = Vector3.zero;
    }

    private void PickDetourToward(Vector3 goal)
    {
        Vector3 position = transform.position;
        Vector3 toGoal = goal - position;
        toGoal.y = 0f;
        if (toGoal.sqrMagnitude < 0.01f)
        {
            ClearMoveDetour();
            return;
        }

        toGoal.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, toGoal);
        if (side.sqrMagnitude < 0.01f)
            side = transform.right;
        else
            side.Normalize();

        float distance = detourDistance;
        Vector3[] candidates =
        {
            position + side * distance + toGoal * (distance * 0.55f),
            position - side * distance + toGoal * (distance * 0.55f),
            position + side * distance,
            position - side * distance,
            position - toGoal * distance
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!IsPlanarPathClear(candidates[i]))
                continue;

            moveDetour = candidates[i];
            hasMoveDetour = true;
            blockedStartedAt = -1f;
            return;
        }

        moveDetour = candidates[0];
        hasMoveDetour = true;
        blockedStartedAt = -1f;
    }

    // 不做水平移动，只应用重力产生的竖直位移。
    private void MoveVerticalOnly()
    {
        if (characterController != null)
            characterController.Move((verticalVelocity + ConsumeHitKnockbackVelocity()) * Time.deltaTime);
    }

    // 按给定方向平滑旋转，只改变 Y 轴朝向。
    private void RotateToward(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, rotateSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // 让自身朝向当前目标。
    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        RotateToward(direction);
    }

    // 维护竖直速度，让 CharacterController 持续受到重力影响。
    private void ApplyGravity()
    {
        bool grounded = characterController.isGrounded;
        if (grounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -1f;
        else
            verticalVelocity.y += gravity * Time.deltaTime;
    }

    // 受击时打断当前动作，应用短暂硬直和击退速度。
    private void ApplyHitReaction(DamageInfo damage)
    {
        attackDamageApplied = true;
        hitStaggerUntil = Mathf.Max(hitStaggerUntil, Time.time + hitStaggerSeconds);

        Vector3 direction = damage.direction;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f && damage.source != null)
        {
            direction = transform.position - damage.source.transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;

        direction.Normalize();
        hitKnockbackVelocity = direction * hitKnockbackSpeed;

        if (hitKnockbackUpSpeed > 0f)
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, hitKnockbackUpSpeed);
    }

    private Vector3 ConsumeHitKnockbackVelocity()
    {
        Vector3 current = hitKnockbackVelocity;
        if (hitKnockbackDamping <= 0f)
        {
            hitKnockbackVelocity = Vector3.zero;
            return current;
        }

        hitKnockbackVelocity = Vector3.MoveTowards(
            hitKnockbackVelocity,
            Vector3.zero,
            hitKnockbackDamping * Time.deltaTime);

        return current;
    }

    // 随机生成下一次巡逻等待结束的时间点。
    private void ScheduleWait()
    {
        float maxWait = Mathf.Max(waitAtPointMin, waitAtPointMax);
        waitUntil = Time.time + Random.Range(waitAtPointMin, maxWait);
    }

    // 在出生点周围随机选择下一个巡逻目标点，避开障碍和刚才卡住的方向。
    private void PickPatrolTarget()
    {
        PickPatrolTarget(Vector3.zero);
    }

    private void PickPatrolTarget(Vector3 avoidDirection)
    {
        avoidDirection.y = 0f;
        bool hasAvoid = avoidDirection.sqrMagnitude > 0.0001f;
        if (hasAvoid)
            avoidDirection.Normalize();

        Vector3 fallback = home;
        bool hasFallback = false;

        int attempts = Mathf.Max(1, patrolSampleAttempts);
        for (int i = 0; i < attempts; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = home + new Vector3(randomOffset.x, 0f, randomOffset.y);
            Vector3 fromHere = candidate - transform.position;
            fromHere.y = 0f;

            if (fromHere.sqrMagnitude < arrivalThreshold * arrivalThreshold)
                continue;

            if (hasAvoid && Vector3.Dot(fromHere.normalized, avoidDirection) > 0.35f)
                continue;

            if (!IsPlanarPathClear(candidate))
                continue;

            patrolTarget = candidate;
            blockedStartedAt = -1f;
            return;
        }

        for (int i = 0; i < attempts; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = home + new Vector3(randomOffset.x, 0f, randomOffset.y);
            if (!IsPlanarPathClear(candidate))
                continue;

            fallback = candidate;
            hasFallback = true;
            break;
        }

        patrolTarget = hasFallback ? fallback : home + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Mathf.Max(1.2f, wanderRadius * 0.4f);
        blockedStartedAt = -1f;
    }

    private bool IsPlanarPathClear(Vector3 destination)
    {
        Vector3 origin = GetProbeOrigin(transform.position);
        Vector3 dest = GetProbeOrigin(destination);
        Vector3 delta = dest - origin;
        delta.y = 0f;
        float distance = delta.magnitude;
        float radius = GetProbeRadius();

        if (IsDestinationOccupied(dest, radius))
            return false;

        if (distance <= arrivalThreshold)
            return true;

        if (!Physics.SphereCast(origin, radius, delta / distance, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return true;

        if (hit.normal.y > 0.65f)
            return true;

        return !HitsStaticObstacle(hit.collider);
    }

    private bool IsDestinationOccupied(Vector3 probe, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(probe, radius, obstacleOverlap, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = obstacleOverlap[i];
            if (!HitsStaticObstacle(collider) || IsGroundCollider(collider, probe))
                continue;

            return true;
        }

        return false;
    }

    private bool IsGroundCollider(Collider collider, Vector3 probe)
    {
        if (collider == null)
            return false;

        if (!Physics.Raycast(probe + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 2f, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        return hit.collider == collider && hit.normal.y > 0.65f;
    }

    private bool HitsStaticObstacle(Collider collider)
    {
        if (collider == null || !collider.enabled)
            return false;

        Transform hitTransform = collider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform))
            return false;

        if (hitTransform.GetComponentInParent<Player>() != null)
            return false;

        if (hitTransform.GetComponentInParent<Monster>() != null)
            return false;

        return true;
    }

    private Vector3 GetProbeOrigin(Vector3 position)
    {
        float height = characterController != null
            ? Mathf.Max(characterController.radius, characterController.height * 0.35f)
            : 0.6f;
        return new Vector3(position.x, position.y + height, position.z);
    }

    private float GetProbeRadius()
    {
        if (characterController == null)
            return 0.25f;

        return Mathf.Max(0.08f, characterController.radius * 0.85f);
    }

    // 如果没有目标，尝试把场景单例 Player 作为追击目标。
    private void AcquireTarget()
    {
        if (target != null)
            return;

        Player player = Player.Instance;
        if (player != null && !player.IsDead)
            target = player.transform;
    }

    // 判断当前目标是否存在；如果目标是玩家，还要确认玩家没有死亡。
    private bool HasValidTarget()
    {
        if (target == null)
            return false;

        var player = target.GetComponent<Player>();
        return player == null || !player.IsDead;
    }

    // 综合距离、视野角和可选视线检测，判断是否能发现目标。
    private bool CanDetectTarget()
    {
        if (!HasValidTarget())
            return false;

        float distance = GetPlanarDistance(transform.position, target.position);
        if (distance > detectRadius)
            return false;

        if (!IsTargetInsideFieldOfView())
            return false;

        return !requireLineOfSight || HasLineOfSight();
    }

    // 判断是否应该放弃目标并返回出生点。
    private bool ShouldReturnHome()
    {
        if (!HasValidTarget())
            return true;

        float distanceToTarget = GetPlanarDistance(transform.position, target.position);
        float distanceFromHome = GetPlanarDistance(transform.position, home);

        return distanceToTarget > loseRadius || distanceFromHome > leashRadius;
    }

    // 判断目标是否进入可攻击距离。
    private bool IsTargetInAttackRange()
    {
        if (!HasValidTarget())
            return false;

        float range = nextAttackIndex == SmashAttackIndex ? smashEngageRange : attackRange;
        return GetPlanarDistance(transform.position, target.position) <= range;
    }

    private float GetCurrentAttackHitRange()
    {
        return IsSmashAttack
            ? smashEngageRange + smashHitGraceRange
            : attackRange + attackHitGraceRange;
    }

    private float GetCurrentAttackArcDegrees()
    {
        return IsSmashAttack ? smashArcDegrees : attackArcDegrees;
    }

    private void CacheSmashAim()
    {
        Vector3 direction = Vector3.zero;
        if (target != null)
        {
            direction = target.position - transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
            direction.y = 0f;
        }

        smashLungeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private void UpdateHatSmashMotion(float elapsed)
    {
        if (!smashFacingLocked)
        {
            FaceTarget();
            CacheSmashAim();
            if (elapsed >= smashCommitFacingSeconds)
                smashFacingLocked = true;
        }

        float lungeStart = Mathf.Max(0f, smashWindupSeconds - smashLungeSeconds * 0.75f);
        if (elapsed >= lungeStart && elapsed < lungeStart + smashLungeSeconds && smashLungeDistance > 0f)
        {
            float speed = smashLungeDistance / smashLungeSeconds;
            if (characterController != null)
            {
                characterController.Move(
                    (smashLungeDirection * speed + verticalVelocity + ConsumeHitKnockbackVelocity()) * Time.deltaTime);
            }

            return;
        }

        MoveVerticalOnly();
    }

    private bool IsInsideAttackArc(Vector3 directionToTarget)
    {
        return IsInsideAttackArc(directionToTarget, attackArcDegrees);
    }

    private bool IsInsideAttackArc(Vector3 directionToTarget, float arcDegrees)
    {
        if (arcDegrees >= 359f)
            return true;

        directionToTarget.y = 0f;
        if (directionToTarget.sqrMagnitude < 0.0001f)
            return true;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return true;

        float angle = Vector3.Angle(forward, directionToTarget);
        return angle <= arcDegrees * 0.5f;
    }


    // 判断目标是否位于自身前方视野角内。
    private bool IsTargetInsideFieldOfView()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f || fieldOfView >= 359f)
            return true;

        float angle = Vector3.Angle(transform.forward, toTarget);
        return angle <= fieldOfView * 0.5f;
    }

    // 用射线检测目标之间是否被障碍物遮挡。
    private bool HasLineOfSight()
    {
        Vector3 origin = transform.position + eyeOffset;
        Vector3 destination = target.position + eyeOffset;
        Vector3 ray = destination - origin;

        if (!Physics.Raycast(origin, ray.normalized, out RaycastHit hit, ray.magnitude, lineOfSightBlockers, QueryTriggerInteraction.Ignore))
            return true;

        return hit.transform == target || hit.transform.IsChildOf(target);
    }

    // 触发动画前先确认 Animator 里确实存在对应参数。
    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }

    // 从目标及其父物体上寻找可受伤接口，兼容伤害脚本挂在父节点的情况。
    private static IDamageable FindDamageable(Transform transformToSearch)
    {
        var behaviours = transformToSearch.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            var damageable = behaviours[i] as IDamageable;
            if (damageable != null)
                return damageable;
        }

        return null;
    }

    // 计算忽略高度差的平面距离，用于追击、攻击和范围判断。
    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 最后锁定旋转，避免模型因物理或动画产生 X/Z 轴倾斜。
    private void LateUpdate()
    {
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    // 死亡时停止移动、禁用控制器，并按延迟销毁对象。
    protected override void OnDeath()
    {
        base.OnDeath();
        BgmManager.NotifyGoblinDied(GetInstanceID());
        currentState = State.Dead;
        verticalVelocity = Vector3.zero;
        hitKnockbackVelocity = Vector3.zero;
        DropLoot();

        if (characterController != null)
            characterController.enabled = false;

        if (releaseRoutine != null)
            StopCoroutine(releaseRoutine);

        releaseRoutine = StartCoroutine(ReleaseAfterDelay(destroyAfterDeathSeconds));
    }

    private IEnumerator ReleaseAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        releaseRoutine = null;

        if (!PooledObject.TryRelease(gameObject))
            Destroy(gameObject);
    }

    private void DropLoot()
    {
        if (lootDropped)
            return;

        lootDropped = true;

        bool forceBestWeapon = guaranteedBestWeaponDrop || IsInvadingQuestGoblin();

        GameObject dropPrefab = forceBestWeapon
            ? PickHighestAttackWeaponPrefab()
            : PickRandomDropPrefab();

        if (dropPrefab == null)
            return;

        Vector3 dropPosition = ResolveDropPosition(forceBestWeapon);
        Quaternion dropRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject dropInstance = SpawnDrop(dropPrefab, dropPosition, dropRotation);
        if (dropInstance == null || !forceBestWeapon)
            return;

        ConsumablePickup pickup = dropInstance.GetComponent<ConsumablePickup>();
        if (pickup == null)
            return;

        pickup.DelayPickup(0.45f);
        pickup.SetAutoEquipOnPickup(false);
    }

    private GameObject PickRandomDropPrefab()
    {
        if (Random.value > dropChance)
            return null;

        bool preferWeapon = Random.value < weaponDropChance;
        return PickDropPrefab(preferWeapon);
    }

    private GameObject PickHighestAttackWeaponPrefab()
    {
        GameObject best = guaranteedWeaponDropPrefab;
        int bestAttack = ReadWeaponAttack(best);
        int bestId = ReadWeaponId(best);

        GameObject[] weapons = LoadDropPool(weaponDropResourcePath);
        for (int i = 0; i < weapons.Length; i++)
        {
            GameObject candidate = weapons[i];
            int attack = ReadWeaponAttack(candidate);
            int id = ReadWeaponId(candidate);
            if (candidate == null || attack < 0)
                continue;

            if (attack > bestAttack || (attack == bestAttack && id > bestId))
            {
                best = candidate;
                bestAttack = attack;
                bestId = id;
            }
        }

        if (best != null)
            return best;

        GameObject loaded = Resources.Load<GameObject>(guaranteedWeaponDropResourcePath);
        if (loaded != null)
            return loaded;

        return PickDropPrefab(true);
    }

    private Vector3 ResolveDropPosition(bool questWeaponDrop)
    {
        Vector3 dropPosition = transform.position + dropOffset;
        if (questWeaponDrop)
        {
            dropPosition += ResolveQuestWeaponDropOffset();
        }
        else
        {
            Vector2 scatter = Random.insideUnitCircle * dropScatterRadius;
            dropPosition += new Vector3(scatter.x, 0f, scatter.y);
        }

        Vector3 rayOrigin = dropPosition + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            dropPosition.y = hit.point.y + 0.12f;

        return dropPosition;
    }

    private Vector3 ResolveQuestWeaponDropOffset()
    {
        Vector3 awayFromPlayer = transform.forward;
        if (Player.Instance != null)
        {
            awayFromPlayer = transform.position - Player.Instance.transform.position;
            awayFromPlayer.y = 0f;
            if (awayFromPlayer.sqrMagnitude < 0.01f)
                awayFromPlayer = transform.forward;
        }

        awayFromPlayer.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, awayFromPlayer);
        if (side.sqrMagnitude < 0.01f)
            side = transform.right;
        side.Normalize();

        return (awayFromPlayer * 0.7f + side * 0.7f).normalized * Mathf.Max(1.2f, questWeaponDropDistance);
    }

    private static GameObject SpawnDrop(GameObject dropPrefab, Vector3 dropPosition, Quaternion dropRotation)
    {
        GameObjectPoolService pool = GameObjectPoolService.EnsureInstance();
        if (pool != null)
        {
            GameObject pooled = pool.Get(dropPrefab, dropPosition, dropRotation);
            if (pooled != null)
                return pooled;
        }

        return Instantiate(dropPrefab, dropPosition, dropRotation);
    }

    private bool IsInvadingQuestGoblin()
    {
        return string.Equals(name, "InvadingGoblin", System.StringComparison.Ordinal);
    }

    private static int ReadWeaponAttack(GameObject dropPrefab)
    {
        ItemSO item = ReadDropItem(dropPrefab);
        return item != null ? item.GetPropertyValue(ItemPropertyType.AttackValue) : -1;
    }

    private static int ReadWeaponId(GameObject dropPrefab)
    {
        ItemSO item = ReadDropItem(dropPrefab);
        return item != null ? item.id : int.MinValue;
    }

    private static ItemSO ReadDropItem(GameObject dropPrefab)
    {
        if (dropPrefab == null)
            return null;

        ConsumablePickup pickup = dropPrefab.GetComponent<ConsumablePickup>();
        return pickup != null ? pickup.Item : null;
    }

    private GameObject PickDropPrefab(bool preferWeapon)
    {
        GameObject[] primaryPool = LoadDropPool(preferWeapon ? weaponDropResourcePath : consumableDropResourcePath);
        if (primaryPool.Length > 0)
            return primaryPool[Random.Range(0, primaryPool.Length)];

        GameObject[] fallbackPool = LoadDropPool(preferWeapon ? consumableDropResourcePath : weaponDropResourcePath);
        if (fallbackPool.Length > 0)
            return fallbackPool[Random.Range(0, fallbackPool.Length)];

        return null;
    }

    private static GameObject[] LoadDropPool(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return new GameObject[0];

        return Resources.LoadAll<GameObject>(resourcePath);
    }
}
