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
        Aerial,
        Landing,
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
    [SerializeField] private float rotateSpeed = 60f;
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float arrivalThreshold = 0.45f;
    [SerializeField] private float minPlayerSeparation = 5.4f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private DamageType attackDamageType = DamageType.Physical;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackHitGraceRange = 0.8f;
    [SerializeField, Range(1f, 360f)] private float attackArcDegrees = 200f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackWindupSeconds = 1.38f;
    [SerializeField] private float attackHitWindowSeconds = 0.5f;
    [SerializeField] private float attackLockSeconds = 2.55f;
    [SerializeField, Range(0f, 1f)] private float meleeHitStartNormalized = 0.42f;
    [SerializeField, Range(0f, 1f)] private float meleeHitEndNormalized = 0.63f;
    [SerializeField] private float meleeStrikeRadius = 2.4f;
    [SerializeField] private string meleeStrikeBone = "UpperMouth01";
    [SerializeField] private string attackTriggerParam = "Attack";
    [SerializeField] private string attackIndexParam = "AttackIndex";
    [SerializeField] private string deathBoolParam = "Dead";

    [Header("Death")]
    [SerializeField] private float destroyAfterDeathSeconds = 5f;

    [Header("Hit Reaction")]
    [SerializeField] private float hitStaggerSeconds;
    [SerializeField] private float hitKnockbackSpeed = 1.4f;
    [SerializeField] private float hitKnockbackDamping = 10f;

    [Header("Facing")]
    [SerializeField] private float attackRotateSpeed = 82f;
    [SerializeField] private bool snapFaceOnAttackStart = false;
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

    [Header("Aerial")]
    [SerializeField] private float aerialHeight = 16f;
    [SerializeField] private float aerialCircleRadius = 9f;
    [SerializeField] private float aerialAngularSpeed = 16f;
    [SerializeField] private float aerialBobAmplitude = 0.55f;
    [SerializeField] private float landSeconds = 2.6f;
    [SerializeField] private Vector3 cinematicLookOffset = new Vector3(0f, 3.2f, 0.35f);
    [SerializeField] private string flyingBoolParam = "Flying";
    [SerializeField] private string landTriggerParam = "Land";

    [Header("Flame VFX")]
    [SerializeField] private bool enableFlameAttack = true;
    [SerializeField] private Transform flameMouth;
    [SerializeField] private string flameMouthBone = "UpperMouth01";
    [SerializeField] private float flameVfxDelaySeconds = 0.45f;
    [SerializeField] private float flameVfxDurationSeconds = 1.9f;
    [SerializeField] private float flameAttackLockSeconds = 2.5f;
    [SerializeField] private float flameRange = 12f;
    [SerializeField, Range(1f, 180f)] private float flameArcDegrees = 55f;
    [SerializeField] private float flameTickInterval = 0.32f;
    [SerializeField] private int flameTickDamage = 10;
    [SerializeField] private DamageType flameDamageType = DamageType.Magic;
    [SerializeField] private float flameRecoveryCooldownSeconds = 0.45f;

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
    private Vector3 perchCenter;
    private float aerialAngle;
    private float landStartedAt = -999f;
    private Vector3 landFromPosition;
    private Quaternion landFromRotation;
    private Vector3 landToPosition;
    private Quaternion landToRotation;
    private DragonFlameBreath flameBreath;
    private Coroutine flameVfxRoutine;
    private Transform meleeStrikePoint;
    private int currentAttackIndex;
    private float nextFlameTickAt = -999f;
    private const int FlameAttackIndex = 1;
    private const string ClawAttackStateName = "ClawAttack";
    private const string FlameAttackStateName = "FlameAttack";
    private const string DieStateName = "Die";

    public bool IsCombatReady =>
        !IsDead && currentState != State.Aerial && currentState != State.Landing;

    /// <summary>是否已经离开盘旋状态（落地中或已可交战）。读档用来决定要不要让龙下来。</summary>
    public bool HasLandedOrDescended => currentState != State.Aerial;

    public Vector3 CinematicLookOffset => cinematicLookOffset;

    public float DeathPresentationSeconds
    {
        get
        {
            float clipLength = GetDieClipLength();
            if (clipLength <= 0f)
                clipLength = 2.2f;

            return clipLength + 0.55f;
        }
    }

    public Vector3 GetCinematicLookPoint()
    {
        return transform.position + cinematicLookOffset;
    }

    protected override void Awake()
    {
        if (maxHp <= 120)
            maxHp = 600;

        base.Awake();
        characterController = GetComponent<CharacterController>();
        CacheSpawnPlatform();
        flameBreath = DragonFlameBreath.Ensure(transform, ResolveFlameMouth());
        if (!IsDescentUnlocked())
            SetFlyingAnimator(true);
    }

    private void Start()
    {
        PrepareVisualBounds();
        CachePerchCenter();
        AcquireTarget();

        if (IsDescentUnlocked())
        {
            SnapToSpawnPlatform();
            EnterState(State.Idle);
        }
        else
        {
            EnterAerial();
        }

        if (registerWithDemo)
            TryRegisterWithDemo();
    }

    /// <summary>
    /// 第二个任务完成后落地，开始可以交战。
    /// </summary>
    public void BeginDescent()
    {
        if (IsDead || currentState == State.Landing || IsCombatReady)
            return;

        EnterLanding();
    }

    /// <summary>读档时巨龙已死：关掉对象，不播死亡演出、不发奖励。</summary>
    public void RestoreAsDead()
    {
        UnregisterWithoutDeath();
        BgmManager.NotifyDragonDisengaged(GetInstanceID());
        currentState = State.Dead;
        StopFlameBreath();
        SetFlyingAnimator(false);
        verticalVelocity = Vector3.zero;
        hitKnockbackVelocity = Vector3.zero;
        SetLocomotionSpeed01(0f);

        if (characterController != null)
            characterController.enabled = false;

        gameObject.SetActive(false);
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

        if (currentState == State.Aerial || currentState == State.Landing)
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
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
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
            if (!IsCombatReady)
            {
                hitStaggerUntil = -999f;
            }
            else
            {
                SetLocomotionSpeed01(0f);
                FaceTarget();
                MoveVerticalOnly();
                return;
            }
        }

        if (currentState == State.Aerial && IsDescentUnlocked())
            BeginDescent();

        switch (currentState)
        {
            case State.Aerial:
                UpdateAerial();
                break;
            case State.Landing:
                UpdateLanding();
                break;
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
        if (!IsCombatReady)
            return false;

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
        SetFlyingAnimator(false);
        StopFlameBreath();
        verticalVelocity = Vector3.zero;
        hitKnockbackVelocity = Vector3.zero;
        SetLocomotionSpeed01(0f);
        PlayDeathAnimation();

        if (characterController != null)
            characterController.enabled = false;

        if (defeatEndsDemo && DemoGameManager.Instance != null && QuestManager.Instance == null)
            StartCoroutine(NotifyBossDefeatedAfterPresentation());

        StartCoroutine(DestroyAfterDelay(GetDeathDespawnDelay()));
    }

    public override bool ShouldShowHealthBar => base.ShouldShowHealthBar && IsCombatReady;

    private void TryRegisterWithDemo()
    {
        if (registeredWithDemo || DemoGameManager.Instance == null)
            return;

        registeredWithDemo = true;
        DemoGameManager.Instance.RegisterEnemy(this);
    }

    private void UpdateAerial()
    {
        SetLocomotionSpeed01(0f);
        aerialAngle += aerialAngularSpeed * Mathf.Deg2Rad * Time.deltaTime;
        SetWorldPose(GetAerialPose(aerialAngle), GetAerialFacing(aerialAngle));
    }

    private void UpdateLanding()
    {
        SetLocomotionSpeed01(0f);
        float duration = Mathf.Max(0.35f, landSeconds);
        float t = Mathf.Clamp01((Time.time - landStartedAt) / duration);
        float eased = t * t * (3f - 2f * t);
        SetWorldPose(
            Vector3.Lerp(landFromPosition, landToPosition, eased),
            Quaternion.Slerp(landFromRotation, landToRotation, eased));

        if (t < 1f)
            return;

        SnapToSpawnPlatform();
        SetCharacterControllerEnabled(true);
        EnterState(State.Idle);
    }

    private void EnterAerial()
    {
        currentState = State.Aerial;
        verticalVelocity = Vector3.zero;
        SetCharacterControllerEnabled(false);
        aerialAngle = Random.Range(0f, Mathf.PI * 2f);
        SetWorldPose(GetAerialPose(aerialAngle), GetAerialFacing(aerialAngle));
        SetFlyingAnimator(true);
        SetLocomotionSpeed01(0f);
    }

    private void EnterLanding()
    {
        if (currentState == State.Landing || IsDead)
            return;

        currentState = State.Landing;
        BgmManager.NotifyDragonDisengaged(GetInstanceID());
        SetCharacterControllerEnabled(false);
        landFromPosition = transform.position;
        landFromRotation = transform.rotation;
        landToPosition = GetPerchStandPosition();
        landToRotation = GetPerchStandRotation();
        landStartedAt = Time.time;
        SetFlyingAnimator(false);
        TriggerLandAnimation();
        SetLocomotionSpeed01(0f);
    }

    private void CachePerchCenter()
    {
        if (spawnPlatform != null)
            perchCenter = spawnPlatform.transform.position;
        else if (spawnPlatformCollider != null)
            perchCenter = spawnPlatformCollider.bounds.center;
        else
            perchCenter = transform.position;
    }

    private Vector3 GetAerialPose(float angle)
    {
        Vector3 center = GetPerchPlanarCenter();
        float standY = GetStandSurfaceY();
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Max(1f, aerialCircleRadius);
        Vector3 position = center + offset;
        position.y = standY + Mathf.Max(4f, aerialHeight) + Mathf.Sin(Time.time * 0.85f) * aerialBobAmplitude;
        return position;
    }

    private Quaternion GetAerialFacing(float angle)
    {
        Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        if (tangent.sqrMagnitude < 0.0001f)
            return transform.rotation;

        return Quaternion.LookRotation(tangent, Vector3.up);
    }

    private Vector3 GetPerchPlanarCenter()
    {
        return new Vector3(perchCenter.x, 0f, perchCenter.z);
    }

    private Vector3 GetPerchStandPosition()
    {
        Vector3 position = GetPerchPlanarCenter();
        float feetOffset = characterController != null
            ? characterController.center.y - characterController.height * 0.5f
            : 0f;
        position.y = GetStandSurfaceY() - feetOffset - visualStandLower;
        return position;
    }

    private Quaternion GetPerchStandRotation()
    {
        Vector3 look = Vector3.zero;
        if (HasValidTarget())
        {
            look = target.position - GetPerchStandPosition();
            look.y = 0f;
        }

        if (look.sqrMagnitude < 0.0001f)
            look = new Vector3(-perchCenter.x, 0f, -perchCenter.z);

        if (look.sqrMagnitude < 0.0001f)
            return Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        return Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    private float GetStandSurfaceY()
    {
        if (spawnPlatform != null)
            return spawnPlatform.GetStandSurfaceY();

        if (spawnPlatformCollider != null)
            return spawnPlatformCollider.bounds.max.y + spawnGroundClearance;

        return transform.position.y;
    }

    private static bool IsDescentUnlocked()
    {
        QuestManager quests = QuestManager.Instance;
        if (quests == null)
            return false;

        return quests.GetStatus(QuestIds.DefeatAllGoblins) == QuestManager.Status.Completed
            || quests.GetStatus(QuestIds.SlayDragon) != QuestManager.Status.Inactive;
    }

    private void SetWorldPose(Vector3 position, Quaternion rotation)
    {
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
            characterController.enabled = false;

        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));

        if (controllerWasEnabled)
            characterController.enabled = true;
    }

    private void SetCharacterControllerEnabled(bool enabled)
    {
        if (characterController != null)
            characterController.enabled = enabled;
    }

    private void SetFlyingAnimator(bool flying)
    {
        if (animator == null || !HasAnimatorParameter(flyingBoolParam, AnimatorControllerParameterType.Bool))
            return;

        animator.SetBool(flyingBoolParam, flying);
    }

    private void TriggerLandAnimation()
    {
        if (animator == null || !HasAnimatorParameter(landTriggerParam, AnimatorControllerParameterType.Trigger))
            return;

        animator.ResetTrigger(landTriggerParam);
        animator.SetTrigger(landTriggerParam);
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

        if (IsFlameAttack)
            TryApplyFlameTicks();
        else
            TryApplyMeleeStrike();

        if (Time.time - attackStartedAt >= GetCurrentAttackLockSeconds())
        {
            bool wasFlame = IsFlameAttack;
            nextAttackAt = Time.time + (wasFlame ? flameRecoveryCooldownSeconds : attackCooldown);
            EnterState(State.Chase);
        }
    }

    private void EnterState(State nextState)
    {
        if (currentState == State.Dead || nextState == currentState)
            return;

        if ((nextState == State.Chase || nextState == State.Attack) && !IsCombatReady && nextState != State.Landing)
            return;

        State previousState = currentState;
        currentState = nextState;
        UpdateCombatMusic(previousState, nextState);

        if (previousState == State.Attack && nextState != State.Attack)
            StopFlameBreath();

        switch (nextState)
        {
            case State.Idle:
                SetFlyingAnimator(false);
                SetLocomotionSpeed01(0f);
                break;
            case State.Chase:
                attackDamageApplied = false;
                break;
            case State.Attack:
                attackStartedAt = Time.time;
                attackDamageApplied = false;
                nextFlameTickAt = -999f;
                SetLocomotionSpeed01(0f);
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
        currentAttackIndex = enableFlameAttack ? nextAttackIndex : 0;
        nextAttackIndex = enableFlameAttack && currentAttackIndex == 0 ? FlameAttackIndex : 0;

        if (animator != null)
        {
            if (HasAnimatorParameter(attackIndexParam, AnimatorControllerParameterType.Int))
                animator.SetInteger(attackIndexParam, currentAttackIndex);

            if (HasAnimatorParameter(attackTriggerParam, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(attackTriggerParam);
                animator.SetTrigger(attackTriggerParam);
            }

            PlayAnimatorState(currentAttackIndex == FlameAttackIndex ? FlameAttackStateName : ClawAttackStateName, 0.1f);
        }

        if (currentAttackIndex == FlameAttackIndex)
        {
            nextFlameTickAt = Time.time + flameVfxDelaySeconds;
            PlayFlameBreath();
        }
        else
            StopFlameBreath();
    }

    private void PlayFlameBreath()
    {
        if (flameBreath == null)
            flameBreath = DragonFlameBreath.Ensure(transform, ResolveFlameMouth());

        if (flameVfxRoutine != null)
            StopCoroutine(flameVfxRoutine);

        flameVfxRoutine = StartCoroutine(PlayFlameBreathSoon());
    }

    private IEnumerator PlayFlameBreathSoon()
    {
        if (flameVfxDelaySeconds > 0f)
            yield return new WaitForSeconds(flameVfxDelaySeconds);

        if (currentState == State.Attack && !IsDead && flameBreath != null)
            flameBreath.Play();

        if (flameVfxDurationSeconds > 0f)
            yield return new WaitForSeconds(flameVfxDurationSeconds);

        flameBreath?.Stop();
        flameVfxRoutine = null;
    }

    private void StopFlameBreath()
    {
        if (flameVfxRoutine != null)
        {
            StopCoroutine(flameVfxRoutine);
            flameVfxRoutine = null;
        }

        flameBreath?.Stop();
    }

    private Transform ResolveFlameMouth()
    {
        if (flameMouth != null)
            return flameMouth;

        if (string.IsNullOrEmpty(flameMouthBone))
            return transform;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == flameMouthBone)
            {
                flameMouth = children[i];
                return flameMouth;
            }
        }

        return transform;
    }

    private bool IsFlameAttack => currentAttackIndex == FlameAttackIndex && currentState == State.Attack;

    private float GetCurrentAttackLockSeconds()
    {
        if (!IsFlameAttack)
            return attackLockSeconds;

        return flameVfxDelaySeconds + flameVfxDurationSeconds;
    }

    private void TryApplyMeleeStrike()
    {
        if (attackDamageApplied || IsDead)
            return;

        if (!IsInMeleeHitWindow())
            return;

        if (!IsTargetInMeleeStrikeRange())
            return;

        bool hitLanded = TryApplyMeleeHit();
        if (hitLanded || IsTargetDodgeInvulnerable())
            attackDamageApplied = true;
    }

    private bool IsInMeleeHitWindow()
    {
        if (TryGetClawAttackNormalizedTime(out float normalizedTime))
            return normalizedTime >= meleeHitStartNormalized && normalizedTime <= meleeHitEndNormalized;

        float elapsed = Time.time - attackStartedAt;
        float start = Mathf.Max(0f, attackWindupSeconds);
        return elapsed >= start && elapsed <= start + Mathf.Max(0.05f, attackHitWindowSeconds);
    }

    private bool TryGetClawAttackNormalizedTime(out float normalizedTime)
    {
        normalizedTime = 0f;
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName(ClawAttackStateName))
        {
            normalizedTime = current.normalizedTime;
            return true;
        }

        return false;
    }

    private bool TryApplyMeleeHit()
    {
        if (!IsTargetInMeleeStrikeRange())
            return false;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        IDamageable damageable = FindDamageable(target);
        if (damageable == null)
            return false;

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = transform.forward;

        int targetDefense = CombatDamageFormulas.GetTargetDefense(damageable, attackDamageType);
        int damageAmount = ResolveOutgoingDamage(attackDamage, targetDefense, attackDamageType, out bool isCritical);
        var info = new DamageInfo(damageAmount, gameObject, target.position, direction, attackDamageType, isCritical);
        if (!damageable.TryTakeDamage(info))
            return false;

        TryApplyAttackLifeSteal(damageAmount);
        return true;
    }

    private bool IsTargetInMeleeStrikeRange()
    {
        if (!HasValidTarget())
            return false;

        Vector3 origin = GetMeleeStrikeOrigin();
        Vector3 aim = target.position + Vector3.up * 0.9f;
        if (Vector3.Distance(origin, aim) > meleeStrikeRadius)
            return false;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        return IsInsideAttackArc(direction, attackArcDegrees);
    }

    private bool IsTargetDodgeInvulnerable()
    {
        if (target == null)
            return false;

        Player player = target.GetComponent<Player>();
        return player != null && player.IsDodgeInvulnerable();
    }

    private Vector3 GetMeleeStrikeOrigin()
    {
        Transform point = ResolveMeleeStrikePoint();
        return point != null ? point.position : GetAttackAssistFacingPoint();
    }

    private Transform ResolveMeleeStrikePoint()
    {
        if (meleeStrikePoint != null)
            return meleeStrikePoint;

        if (flameMouth != null && (string.IsNullOrEmpty(meleeStrikeBone) || flameMouth.name == meleeStrikeBone))
        {
            meleeStrikePoint = flameMouth;
            return meleeStrikePoint;
        }

        if (string.IsNullOrEmpty(meleeStrikeBone))
            return transform;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == meleeStrikeBone)
            {
                meleeStrikePoint = children[i];
                return meleeStrikePoint;
            }
        }

        return transform;
    }

    private void TryApplyFlameTicks()
    {
        if (!IsFlameAttack || IsDead)
            return;

        float elapsed = Time.time - attackStartedAt;
        if (elapsed < flameVfxDelaySeconds)
            return;

        if (elapsed > flameVfxDelaySeconds + flameVfxDurationSeconds)
            return;

        if (Time.time < nextFlameTickAt)
            return;

        ApplyAttackDamage(flameTickDamage, flameDamageType, flameRange, flameArcDegrees);
        nextFlameTickAt = Time.time + Mathf.Max(0.05f, flameTickInterval);
    }

    private void ApplyAttackDamage(int damage, DamageType damageType, float hitRange, float arcDegrees)
    {
        if (!HasValidTarget())
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.magnitude > hitRange)
            return;

        if (!IsInsideAttackArc(direction, arcDegrees))
            return;

        IDamageable damageable = FindDamageable(target);
        if (damageable == null)
            return;

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = transform.forward;

        int targetDefense = CombatDamageFormulas.GetTargetDefense(damageable, damageType);
        int damageAmount = ResolveOutgoingDamage(damage, targetDefense, damageType, out bool isCritical);
        var info = new DamageInfo(damageAmount, gameObject, target.position, direction, damageType, isCritical);
        if (damageable.TryTakeDamage(info))
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
        if (characterController == null || !characterController.enabled)
            return;

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
        if (currentState == State.Aerial || currentState == State.Landing)
            return;

        if (characterController == null || !characterController.enabled)
            return;

        if (characterController.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -1f;
        else
            verticalVelocity.y += gravity * Time.deltaTime;
    }

    private void ApplyHitReaction(DamageInfo damage)
    {
        if (hitStaggerSeconds > 0f)
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

        Player player = Player.Instance;
        if (player != null && !player.IsDead)
            target = player.transform;
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
        if (!HasValidTarget())
            return false;

        float range = enableFlameAttack && nextAttackIndex == FlameAttackIndex ? flameRange : attackRange;
        return GetPlanarDistance(transform.position, target.position) <= range;
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

        return Vector3.Angle(forward, directionToTarget) <= arcDegrees * 0.5f;
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

    private IEnumerator NotifyBossDefeatedAfterPresentation()
    {
        yield return new WaitForSeconds(DeathPresentationSeconds);

        if (DemoGameManager.Instance != null)
            DemoGameManager.Instance.NotifyBossDefeated();
    }

    private void PlayDeathAnimation()
    {
        if (animator == null)
            return;

        if (HasAnimatorParameter(attackTriggerParam, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(attackTriggerParam);

        if (HasAnimatorParameter(landTriggerParam, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(landTriggerParam);

        if (HasAnimatorParameter(deathBoolParam, AnimatorControllerParameterType.Bool))
            animator.SetBool(deathBoolParam, true);

        PlayAnimatorState(DieStateName, 0f);
        animator.Update(0f);
    }

    private void PlayAnimatorState(string stateName, float fadeSeconds)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        if (fadeSeconds <= 0f)
            animator.Play(stateName, 0, 0f);
        else
            animator.CrossFadeInFixedTime(stateName, fadeSeconds, 0, 0f);
    }

    private float GetDeathDespawnDelay()
    {
        return Mathf.Max(0.5f, destroyAfterDeathSeconds, DeathPresentationSeconds + 0.5f);
    }

    private float GetDieClipLength()
    {
        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null)
            return 0f;

        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == "Die")
                return clip.length;
        }

        return 0f;
    }

    private void LateUpdate()
    {
        PreventPlatformSink();

        if (IsDead)
            return;

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
        attackHitWindowSeconds = Mathf.Max(0.05f, attackHitWindowSeconds);
        meleeHitStartNormalized = Mathf.Clamp01(meleeHitStartNormalized);
        meleeHitEndNormalized = Mathf.Max(meleeHitStartNormalized, Mathf.Clamp01(meleeHitEndNormalized));
        meleeStrikeRadius = Mathf.Max(0.5f, meleeStrikeRadius);
        attackLockSeconds = Mathf.Max(attackWindupSeconds + attackHitWindowSeconds, attackLockSeconds);
        aerialHeight = Mathf.Max(4f, aerialHeight);
        aerialCircleRadius = Mathf.Max(1f, aerialCircleRadius);
        aerialAngularSpeed = Mathf.Max(1f, aerialAngularSpeed);
        landSeconds = Mathf.Max(0.35f, landSeconds);
        hitStaggerSeconds = Mathf.Max(0f, hitStaggerSeconds);
        flameVfxDelaySeconds = Mathf.Max(0f, flameVfxDelaySeconds);
        flameVfxDurationSeconds = Mathf.Max(0.1f, flameVfxDurationSeconds);
        flameAttackLockSeconds = Mathf.Max(flameVfxDelaySeconds + flameVfxDurationSeconds, flameAttackLockSeconds);
        flameRange = Mathf.Max(attackRange, flameRange);
        flameArcDegrees = Mathf.Clamp(flameArcDegrees, 1f, 180f);
        flameTickInterval = Mathf.Max(0.05f, flameTickInterval);
        flameTickDamage = Mathf.Max(1, flameTickDamage);
        flameRecoveryCooldownSeconds = Mathf.Max(0f, flameRecoveryCooldownSeconds);
    }
}
