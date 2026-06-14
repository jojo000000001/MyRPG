using UnityEngine;

/// <summary>
/// 敌人的有限状态机：负责巡逻、追击、攻击、返回出生点和死亡流程。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Goblin : Monster
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

    [Header("Hit Reaction")]
    [SerializeField] private float hitStaggerSeconds = 0.3f;
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

    // 当前状态保留为可序列化字段，便于在 Inspector 中观察调试。
    [SerializeField] private State currentState = State.PatrolWait;

    // 运行时缓存和计时数据。
    private CharacterController characterController;
    private Vector3 home;
    private Vector3 patrolTarget;
    private Vector3 verticalVelocity;
    private float waitUntil;
    private float attackStartedAt = -999f;
    private float nextAttackAt = -999f;
    private bool attackDamageApplied;
    private Vector3 hitKnockbackVelocity;
    private float hitStaggerUntil = -999f;
    private bool lootDropped;

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
    }

    // 记录出生点，尝试获取目标，并进入初始巡逻等待状态。
    private void Start()
    {
        home = transform.position;
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

    // 巡逻移动：向随机巡逻点移动，到达后回到等待状态。
    private void UpdatePatrolMove()
    {
        if (CanDetectTarget())
        {
            EnterState(State.Chase);
            return;
        }

        if (MoveToward(patrolTarget, moveSpeed))
            EnterState(State.PatrolWait);
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

        MoveToward(target.position, chaseSpeed);
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

        FaceTarget();
        MoveVerticalOnly();

        float elapsed = Time.time - attackStartedAt;
        if (!attackDamageApplied && elapsed >= attackWindupSeconds)
        {
            attackDamageApplied = true;
            ApplyAttackDamage();
        }

        if (elapsed < attackLockSeconds)
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

        if (MoveToward(home, returnSpeed))
            EnterState(State.PatrolWait);
    }

    // 状态切换入口：集中处理进入某个状态时需要做的一次性初始化。
    private void EnterState(State nextState)
    {
        if (currentState == nextState && nextState != State.Attack)
            return;

        currentState = nextState;

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

    // 开始一次攻击，记录计时并触发可选的攻击动画参数。
    private void BeginAttack()
    {
        attackStartedAt = Time.time;
        nextAttackAt = Time.time + attackCooldown;
        attackDamageApplied = false;
        SetLocomotionSpeed01(0f);

        if (!string.IsNullOrEmpty(attackTriggerParam) && HasAnimatorParameter(attackTriggerParam, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(attackTriggerParam);
    }

    // 在攻击前摇结束后尝试结算伤害，只命中仍在有效范围内的目标。
    private void ApplyAttackDamage()
    {
        if (!HasValidTarget())
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        float hitRange = attackRange + attackHitGraceRange;
        if (direction.magnitude > hitRange)
            return;

        if (!IsInsideAttackArc(direction))
            return;

        IDamageable damageable = FindDamageable(target);
        if (damageable == null)
            return;

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = transform.forward;

        DamageInfo damage = new DamageInfo(attackDamage, gameObject, target.position, direction, attackDamageType);
        damageable.TryTakeDamage(damage);
    }

    // 水平移动到指定位置，同时应用竖直速度并同步朝向。
    private bool MoveToward(Vector3 destination, float speed)
    {
        Vector3 position = transform.position;
        Vector3 toTarget = new Vector3(destination.x, position.y, destination.z) - position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= arrivalThreshold)
        {
            SetLocomotionSpeed01(0f);
            MoveVerticalOnly();
            return true;
        }

        Vector3 direction = toTarget / distance;
        RotateToward(direction);

        SetLocomotionSpeed01(speed > 0f ? 1f : 0f);
        characterController.Move((direction * speed + verticalVelocity + ConsumeHitKnockbackVelocity()) * Time.deltaTime);
        return false;
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

    // 在出生点周围随机选择下一个巡逻目标点。
    private void PickPatrolTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
        patrolTarget = home + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private static Player cachedPlayer;

    // 如果没有目标，尝试在场景中查找 Player 作为追击目标。
    private void AcquireTarget()
    {
        if (target != null)
            return;

        if (cachedPlayer == null)
            cachedPlayer = Object.FindObjectOfType<Player>();

        if (cachedPlayer != null && !cachedPlayer.IsDead)
            target = cachedPlayer.transform;
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
        return HasValidTarget() && GetPlanarDistance(transform.position, target.position) <= attackRange;
    }

    private bool IsInsideAttackArc(Vector3 directionToTarget)
    {
        if (attackArcDegrees >= 359f)
            return true;

        directionToTarget.y = 0f;
        if (directionToTarget.sqrMagnitude < 0.0001f)
            return true;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return true;

        float angle = Vector3.Angle(forward, directionToTarget);
        return angle <= attackArcDegrees * 0.5f;
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
        currentState = State.Dead;
        verticalVelocity = Vector3.zero;
        hitKnockbackVelocity = Vector3.zero;
        DropLoot();

        if (characterController != null)
            characterController.enabled = false;

        Destroy(gameObject, destroyAfterDeathSeconds);
    }

    private void DropLoot()
    {
        if (lootDropped)
            return;

        lootDropped = true;

        if (Random.value > dropChance)
            return;

        bool preferWeapon = Random.value < weaponDropChance;
        GameObject dropPrefab = PickDropPrefab(preferWeapon);
        if (dropPrefab == null)
            return;

        Vector2 scatter = Random.insideUnitCircle * dropScatterRadius;
        Vector3 dropPosition = transform.position + dropOffset + new Vector3(scatter.x, 0f, scatter.y);
        Quaternion dropRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Instantiate(dropPrefab, dropPosition, dropRotation);
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
