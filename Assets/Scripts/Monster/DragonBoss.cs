using System.Collections;
using UnityEngine;

/// <summary>
/// 地面 Boss 龙：发现玩家后追击，爪击/喷火交替攻击。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(BossHealthBar))]
public sealed class DragonBoss : Monster
{
    private enum State
    {
        Idle,
        Chase,
        Attack,
        Dead
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRadius = 22f;
    [SerializeField] private float loseRadius = 32f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.6f;
    [SerializeField] private float chaseSpeed = 2.4f;
    [SerializeField] private float rotateSpeed = 160f;
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float arrivalThreshold = 0.45f;
    [SerializeField] private float minPlayerSeparation = 3.1f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private DamageType attackDamageType = DamageType.Physical;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackHitGraceRange = 0.8f;
    [SerializeField, Range(1f, 360f)] private float attackArcDegrees = 200f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackWindupSeconds = 0.5f;
    [SerializeField] private float attackLockSeconds = 1.25f;
    [SerializeField] private string attackTriggerParam = "Attack";
    [SerializeField] private string attackIndexParam = "AttackIndex";
    [SerializeField] private string deathBoolParam = "Dead";

    [Header("Death")]
    [SerializeField] private float destroyAfterDeathSeconds = 4f;

    [Header("Hit Reaction")]
    [SerializeField] private float hitStaggerSeconds = 0.35f;
    [SerializeField] private float hitKnockbackSpeed = 1.4f;
    [SerializeField] private float hitKnockbackDamping = 10f;

    [Header("Facing")]
    [SerializeField] private float attackRotateSpeed = 480f;
    [SerializeField] private bool snapFaceOnAttackStart = true;
    [SerializeField] private Vector3 attackAssistFacingOffset = new Vector3(0f, 1.2f, 1.8f);
    [SerializeField] private float attackAssistRangeBonus = 5f;

    [Header("Demo")]
    [SerializeField] private bool registerWithDemo = true;
    [SerializeField] private bool defeatEndsDemo = true;

    [Header("Spawn")]
    [SerializeField] private DragonStumpSpawnPlatform spawnPlatform;
    [SerializeField] private string spawnPlatformName = "DragonStumpPlatform";
    [SerializeField] private float spawnGroundClearance = 0.05f;
    [SerializeField] private float visualStandLower = 0f;

    [SerializeField] private State currentState = State.Idle;

    private CharacterController characterController;
    private Collider spawnPlatformCollider;
    private Vector3 verticalVelocity;
    private Vector3 hitKnockbackVelocity;
    private float hitStaggerUntil = -999f;
    private float attackStartedAt = -999f;
    private float nextAttackAt = -999f;
    private bool attackDamageApplied;
    private int nextAttackIndex;
    private bool registeredWithDemo;
    private static Player cachedPlayer;

    protected override void Awake()
    {
        if (maxHp <= 120)
            maxHp = 600;

        base.Awake();
        characterController = GetComponent<CharacterController>();
        CacheSpawnPlatform();
    }

    private void Start()
    {
        PrepareVisualBounds();
        SnapToSpawnPlatform();
        AcquireTarget();
        EnterState(State.Idle);

        if (registerWithDemo)
            TryRegisterWithDemo();
    }

    private void CacheSpawnPlatform()
    {
        if (spawnPlatform == null && !string.IsNullOrEmpty(spawnPlatformName))
        {
            GameObject platformObject = GameObject.Find(spawnPlatformName);
            if (platformObject != null)
                spawnPlatform = platformObject.GetComponent<DragonStumpSpawnPlatform>();
        }

        spawnPlatformCollider = spawnPlatform != null
            ? spawnPlatform.GetComponent<Collider>()
            : null;

        if (spawnPlatformCollider == null && !string.IsNullOrEmpty(spawnPlatformName))
        {
            GameObject platformObject = GameObject.Find(spawnPlatformName);
            if (platformObject != null)
                spawnPlatformCollider = platformObject.GetComponent<Collider>();
        }
    }

    private void PrepareVisualBounds()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.Update(0f);

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].updateWhenOffscreen = true;
    }

    private void SnapToSpawnPlatform()
    {
        if (characterController == null || spawnPlatformCollider == null)
            return;

        float standY = spawnPlatform != null
            ? spawnPlatform.GetStandSurfaceY()
            : spawnPlatformCollider.bounds.max.y + spawnGroundClearance;

        float visualFeetOffset = GetVisualFeetOffsetFromTransform();
        Vector3 position = transform.position;
        position.y = standY - visualFeetOffset - visualStandLower;

        characterController.enabled = false;
        transform.position = position;
        verticalVelocity.y = -1f;
        characterController.enabled = true;
    }

    private void PreventPlatformSink()
    {
        if (characterController == null || spawnPlatformCollider == null || IsDead)
            return;

        if (spawnPlatform != null && !spawnPlatform.ContainsPlanar(transform.position))
            return;

        float standY = spawnPlatform != null
            ? spawnPlatform.GetStandSurfaceY()
            : spawnPlatformCollider.bounds.max.y + spawnGroundClearance;

        float visualFeetY = transform.position.y + GetVisualFeetOffsetFromTransform();
        float targetFeetY = standY - visualStandLower;
        if (visualFeetY >= targetFeetY - 0.02f)
            return;

        characterController.enabled = false;
        transform.position += Vector3.up * (targetFeetY - visualFeetY);
        verticalVelocity.y = -1f;
        characterController.enabled = true;
    }

    private float GetVisualFeetOffsetFromTransform()
    {
        float lowestY = float.PositiveInfinity;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (renderer.bounds.min.y < lowestY)
                lowestY = renderer.bounds.min.y;
        }

        if (float.IsPositiveInfinity(lowestY))
            return characterController.center.y - characterController.height * 0.5f;

        return lowestY - transform.position.y;
    }

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
            case State.Idle:
                UpdateIdle();
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Attack:
                UpdateAttack();
                break;
            case State.Dead:
                SetLocomotionSpeed01(0f);
                break;
        }
    }

    public override bool TryTakeDamage(DamageInfo damage)
    {
        bool accepted = base.TryTakeDamage(damage);
        if (!accepted || IsDead)
            return accepted;

        if (damage.source != null)
            target = damage.source.transform;

        ApplyHitReaction(damage);

        if (currentState != State.Attack)
            EnterState(State.Chase);

        return true;
    }

    protected override void OnDeath()
    {
        base.OnDeath();
        BgmManager.NotifyDragonDisengaged(GetInstanceID());
        currentState = State.Dead;
        verticalVelocity = Vector3.zero;
        hitKnockbackVelocity = Vector3.zero;
        SetLocomotionSpeed01(0f);

        if (animator != null && HasAnimatorParameter(deathBoolParam, AnimatorControllerParameterType.Bool))
            animator.SetBool(deathBoolParam, true);

        if (characterController != null)
            characterController.enabled = false;

        if (defeatEndsDemo && DemoGameManager.Instance != null)
            DemoGameManager.Instance.NotifyBossDefeated();

        StartCoroutine(DestroyAfterDelay(destroyAfterDeathSeconds));
    }

    private void TryRegisterWithDemo()
    {
        if (registeredWithDemo || DemoGameManager.Instance == null)
            return;

        registeredWithDemo = true;
        DemoGameManager.Instance.RegisterEnemy(this);
    }

    private void UpdateIdle()
    {
        SetLocomotionSpeed01(0f);
        FaceTarget();

        if (CanDetectTarget())
            EnterState(State.Chase);
        else
            MoveVerticalOnly();
    }

    private void UpdateChase()
    {
        if (!HasValidTarget() || ShouldLoseTarget())
        {
            EnterState(State.Idle);
            return;
        }

        FaceTarget();

        if (IsTargetInAttackRange() && Time.time >= nextAttackAt)
        {
            EnterState(State.Attack);
            return;
        }

        float distance = GetPlanarDistance(transform.position, target.position);
        float speed = distance > attackRange * 1.25f ? chaseSpeed : walkSpeed;
        MoveToward(target.position, speed);
    }

    private void UpdateAttack()
    {
        SetLocomotionSpeed01(0f);

        if (!HasValidTarget() || ShouldLoseTarget())
        {
            EnterState(State.Chase);
            return;
        }

        FaceTarget();
        MoveVerticalOnly();

        if (!attackDamageApplied && Time.time - attackStartedAt >= attackWindupSeconds)
        {
            ApplyAttackDamage();
            attackDamageApplied = true;
        }

        if (Time.time - attackStartedAt >= attackLockSeconds)
        {
            nextAttackAt = Time.time + attackCooldown;

            if (IsTargetInAttackRange() && HasValidTarget())
                EnterState(State.Attack);
            else
                EnterState(State.Chase);
        }
    }

    private void EnterState(State nextState)
    {
        if (currentState == State.Dead || nextState == currentState)
            return;

        State previousState = currentState;
        currentState = nextState;
        UpdateCombatMusic(previousState, nextState);

        switch (nextState)
        {
            case State.Idle:
                SetLocomotionSpeed01(0f);
                break;
            case State.Chase:
                attackDamageApplied = false;
                break;
            case State.Attack:
                attackStartedAt = Time.time;
                attackDamageApplied = false;
                if (snapFaceOnAttackStart)
                    FaceTarget(snap: true);
                TriggerAttackAnimation();
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
            BgmManager.NotifyDragonEngaged(GetInstanceID());
        else if (wasCombat && !isCombat)
            BgmManager.NotifyDragonDisengaged(GetInstanceID());
    }

    private void TriggerAttackAnimation()
    {
        if (animator == null)
            return;

        if (HasAnimatorParameter(attackIndexParam, AnimatorControllerParameterType.Int))
            animator.SetInteger(attackIndexParam, nextAttackIndex);

        if (HasAnimatorParameter(attackTriggerParam, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(attackTriggerParam);
            animator.SetTrigger(attackTriggerParam);
        }

        nextAttackIndex = nextAttackIndex == 0 ? 1 : 0;
    }

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

        int targetDefense = CombatDamageFormulas.GetTargetDefense(damageable, attackDamageType);
        int damageAmount = ResolveOutgoingDamage(attackDamage, targetDefense, attackDamageType, out bool isCritical);
        var damage = new DamageInfo(damageAmount, gameObject, target.position, direction, attackDamageType, isCritical);
        if (damageable.TryTakeDamage(damage))
            TryApplyAttackLifeSteal(damageAmount);
    }

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

        if (HasValidTarget() && distance <= minPlayerSeparation)
        {
            FaceTarget();
            SetLocomotionSpeed01(0f);
            MoveVerticalOnly();
            return false;
        }

        Vector3 direction = toTarget / distance;
        RotateToward(direction);

        float speed01 = speed >= chaseSpeed * 0.85f ? 1f : 0.5f;
        SetLocomotionSpeed01(speed01);
        characterController.Move((direction * speed + verticalVelocity + ConsumeHitKnockbackVelocity()) * Time.deltaTime);
        return false;
    }

    private void MoveVerticalOnly()
    {
        characterController.Move((verticalVelocity + ConsumeHitKnockbackVelocity()) * Time.deltaTime);
    }

    private void RotateToward(Vector3 direction)
    {
        float turnSpeed = currentState == State.Attack ? attackRotateSpeed : rotateSpeed;
        RotateYawToward(transform, direction, turnSpeed);
    }

    private void FaceTarget(bool snap = false)
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        float turnSpeed = currentState == State.Attack ? attackRotateSpeed : rotateSpeed;
        RotateYawToward(transform, direction, turnSpeed, snap);
    }

    public override Vector3 GetAttackAssistFacingPoint()
    {
        return transform.position + transform.TransformVector(attackAssistFacingOffset);
    }

    public override float GetAttackAssistRangeBonus()
    {
        return attackAssistRangeBonus;
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -1f;
        else
            verticalVelocity.y += gravity * Time.deltaTime;
    }

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
    }

    private Vector3 ConsumeHitKnockbackVelocity()
    {
        Vector3 current = hitKnockbackVelocity;
        hitKnockbackVelocity = Vector3.MoveTowards(
            hitKnockbackVelocity,
            Vector3.zero,
            hitKnockbackDamping * Time.deltaTime);
        return current;
    }

    private void AcquireTarget()
    {
        if (target != null)
            return;

        if (cachedPlayer == null)
            cachedPlayer = Object.FindObjectOfType<Player>();

        if (cachedPlayer != null && !cachedPlayer.IsDead)
            target = cachedPlayer.transform;
    }

    private bool HasValidTarget()
    {
        if (target == null)
            return false;

        var player = target.GetComponent<Player>();
        return player == null || !player.IsDead;
    }

    private bool CanDetectTarget()
    {
        return HasValidTarget() && GetPlanarDistance(transform.position, target.position) <= detectRadius;
    }

    private bool ShouldLoseTarget()
    {
        return !HasValidTarget() || GetPlanarDistance(transform.position, target.position) > loseRadius;
    }

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

        return Vector3.Angle(forward, directionToTarget) <= attackArcDegrees * 0.5f;
    }

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

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        return Monster.GetPlanarDistance(a, b);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Destroy(gameObject);
    }

    private void LateUpdate()
    {
        PreventPlatformSink();

        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void OnValidate()
    {
        detectRadius = Mathf.Max(0f, detectRadius);
        loseRadius = Mathf.Max(detectRadius, loseRadius);
        walkSpeed = Mathf.Max(0f, walkSpeed);
        chaseSpeed = Mathf.Max(walkSpeed, chaseSpeed);
        attackRange = Mathf.Max(0.5f, attackRange);
        minPlayerSeparation = Mathf.Max(0.5f, minPlayerSeparation);
        attackRotateSpeed = Mathf.Max(rotateSpeed, attackRotateSpeed);
        attackAssistRangeBonus = Mathf.Max(0f, attackAssistRangeBonus);
        attackCooldown = Mathf.Max(0.2f, attackCooldown);
        attackLockSeconds = Mathf.Max(attackWindupSeconds, attackLockSeconds);
    }
}
