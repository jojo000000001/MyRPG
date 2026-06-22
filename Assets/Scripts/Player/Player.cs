using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家控制器：处理移动、镜头相对转向、跳跃手感以及三段连击攻击。
/// </summary>
/// <summary>
/// 玩家控制器：处理移动、镜头相对转向、跳跃手感、三段连击以及基础战斗属性。
/// </summary>
public class Player : MonoBehaviour, IDamageable
{
    // 常用组件缓存，避免每帧重复 GetComponent。
    private Animator animator;
    private CharacterController characterController;
    private AttackHitbox attackHitbox;
    private HitFeedback hitFeedback;

    [Header("Stats")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int armor = 5;
    [SerializeField] private int magicResistance = 5;
    [SerializeField] private int attackPower = 10;
    [SerializeField] private float damageInvulnSeconds = 0.3f;
    [Header("Equipment")]
    [SerializeField] private ItemSO equippedWeapon;
    [Header("Hit Feedback")]
    [SerializeField] private bool enableHitFeedback = true;
    [SerializeField] private string hitTriggerParam = "Hit";
    [SerializeField] private string hitBoolParam = "";
    [SerializeField] private float hitBoolSeconds = 0.05f;
    [SerializeField] private bool cancelAttackOnHit = false;

    [Header("Attack Hitbox")]
    [SerializeField] private bool useFallbackHitbox = true;
    [SerializeField] private float fallbackHitboxDelay = 0.12f;
    [SerializeField] private float fallbackHitboxSeconds = 0.12f;
    [Tooltip("动画 Close 后，若此时间内没有继续攻击，再关闭判定盒。")]
    [SerializeField] private float hitboxIdleCloseSeconds = 0.3f;

    [Header("Attack Assist")]
    [SerializeField] private bool enableAttackAssist = true;
    [SerializeField] private float attackAssistRadius = 4.5f;
    [SerializeField] private float attackFaceTurnSpeed = 720f;
    [Tooltip("相对敌人朝向的额外偏航（度）。0 表示正面对准敌人。")]
    [SerializeField, Range(-45f, 45f)] private float attackYawOffset = 0f;
    [SerializeField] private bool assistCameraBehindOnAttack = true;
    private int currentHp;
    private float lastDamagedAt = -999f;

    private Coroutine queuedHitboxRoutine;
    private bool attackHitboxEventReceived;
    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public int Armor => armor;
    public int MagicResistance => magicResistance;
    public event Action EquipmentChanged;

    public int BaseAttackPower => Mathf.Max(0, attackPower);
    public int WeaponAttackBonus => GetWeaponAttackBonus(equippedWeapon);
    public int AttackPower => Mathf.Max(0, BaseAttackPower + WeaponAttackBonus);
    public ItemSO EquippedWeapon => equippedWeapon;
    public bool IsDead => currentHp <= 0;
    public float Health01 => maxHp <= 0 ? 0f : Mathf.Clamp01((float)currentHp / maxHp);
    public bool IsAttacking => animator != null && animator.GetBool("IsAttacking");
    public bool AssistCameraBehindOnAttack => assistCameraBehindOnAttack;
    public bool IsCombatCameraAssistActive => assistCameraBehindOnAttack && IsAttacking && HasEnemyInAttackAssistRange();

    public event Action Died;

    [Header("Move / Jump")]
    public float moveSpeed = 50f;
    [Tooltip("移动时的转身速度（度/秒）")]
    public float rotateSpeed = 50f;
    public float gravity = -10f;
    public float jumpHeight = 5f;

    // 跳跃手感参数：输入缓冲、土狼时间、下落加速度和短按跳跃。
    [Header("Jump Comfort")]
    [Tooltip("落地前按下跳跃后，输入会被保留多久（秒）。")]
    public float jumpBufferTime = 0.12f;
    [Tooltip("离开地面后，仍允许起跳的宽容时间（秒）。")]
    public float coyoteTime = 0.1f;
    [Tooltip("下落时的重力倍率。")]
    public float fallGravityMultiplier = 1.8f;
    [Tooltip("提前松开跳跃键时，向上速度的保留比例。")]
    public float lowJumpVelocityMultiplier = 0.5f;

    private float runValue = 0.5f;

    [Header("AD Turn (camera-relative)")]
    [Tooltip("仅按 A/D 时的原地转向速度（度/秒）")]
    public float adTurnSpeed = 450f;
    [Tooltip("仅按 A/D 时，相对镜头正前方的偏航角度（度）")]
    public float adTurnAngle = 90f;
    [Tooltip("A/D 转向后开始移动前允许的偏航误差（度）")]
    public float adMoveStartAngle = 1f;

    [Header("WS Turn Limit")]
    [Tooltip("按住 W/S 时的转向速度（度/秒）")]
    public float wsTurnSpeed = 120f;
    [Tooltip("按住 W/S 时允许的最大偏航偏移（度）")]
    public float wsMaxYawOffset = 45f;

    [Header("Camera-relative W/S")]
    [Tooltip("用于镜头相对移动的视角参考。为空时会先查找 ThirdPersonCameraRig，再使用 Camera.main。")]
    public Transform cameraTransform;

    [Tooltip("按住 W/S 时，朝向镜头相对方向的转身速度（度/秒）。")]
    public float wsFaceTurnSpeed = 720f;

    [Header("Input Comfort")]
    [Tooltip("仅 A/D 转向开始前的短暂延迟，让 W+A/D 即使略微错开按下也能形成斜向移动。")]
    public float inputBufferTime = 0.1f;
    [Tooltip("水平移动加速响应时间（秒）。")]
    public float accelerationTime = 0.08f;
    [Tooltip("水平移动减速响应时间（秒）。")]
    public float decelerationTime = 0.12f;

    // 运行时移动状态：镜头参考、竖直速度和水平移动平滑值。
    private Transform cachedViewTransform;
    private Vector3 velocity;
    private Vector3 smoothedMove;

    private float lastGroundedAt = -999f;
    private float lastJumpPressedAt = -999f;
    private float adOnlyStartedAt = -1f;

    // 仅按 A/D 时的镜头相对转向状态。
    private bool adYawActive;
    private float adYawBase;
    private float adYawOffset;

    // 按 W/S 前后移动时允许左右微调的转向状态。
    private bool wsYawActive;
    private float wsYawOffset;

    // 连击系统：在 comboWindow 内连续点击会推进到下一段攻击。
    private int comboCount = 0;
    public float comboWindow = 0.8f;
    [Tooltip("两次攻击输入的最小间隔，避免一次连按触发多段 Trigger。")]
    [SerializeField] private float attackInputMinInterval = 0.1f;
    [Tooltip("连击输入缓冲：在此时间内按下的攻击键会在间隔结束后自动衔接。")]
    [SerializeField] private float attackInputBufferTime = 0.22f;
    private float comboExpiresAt = -999f;
    private float lastAttackInputAt = -999f;
    private bool pendingComboInput;
    private float pendingComboInputExpiresAt = -999f;

    private static readonly int Atk1Hash = Animator.StringToHash("Atk1");
    private static readonly int Atk2Hash = Animator.StringToHash("Atk2");
    private static readonly int Atk3Hash = Animator.StringToHash("Atk3");

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        armor = Mathf.Max(0, armor);
        magicResistance = Mathf.Max(0, magicResistance);
        attackPower = Mathf.Max(0, attackPower);
        damageInvulnSeconds = Mathf.Max(0f, damageInvulnSeconds);
        hitBoolSeconds = Mathf.Max(0f, hitBoolSeconds);
        fallbackHitboxDelay = Mathf.Max(0f, fallbackHitboxDelay);
        fallbackHitboxSeconds = Mathf.Max(0.01f, fallbackHitboxSeconds);
        attackAssistRadius = Mathf.Max(0f, attackAssistRadius);
        attackFaceTurnSpeed = Mathf.Max(1f, attackFaceTurnSpeed);
        attackYawOffset = Mathf.Clamp(attackYawOffset, -45f, 45f);
        attackInputMinInterval = Mathf.Max(0f, attackInputMinInterval);
        attackInputBufferTime = Mathf.Max(0f, attackInputBufferTime);

        if (Application.isPlaying)
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
    }

    public bool EquipWeapon(ItemSO weapon)
    {
        if (!IsValidWeapon(weapon))
            return false;

        if (equippedWeapon == weapon)
            return true;

        equippedWeapon = weapon;
        NotifyEquipmentChanged();
        return true;
    }

    public void UnequipWeapon()
    {
        if (equippedWeapon == null)
            return;

        equippedWeapon = null;
        NotifyEquipmentChanged();
    }

    private static bool IsValidWeapon(ItemSO item)
    {
        return item != null
            && item.itemType == ItemType.Weapon
            && GetWeaponAttackBonus(item) > 0;
    }

    private static int GetWeaponAttackBonus(ItemSO weapon)
    {
        return weapon != null ? Mathf.Max(0, weapon.GetPropertyValue(ItemPropertyType.AttackValue)) : 0;
    }

    private void NotifyEquipmentChanged()
    {
        if (EquipmentChanged != null)
            EquipmentChanged.Invoke();
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        attackHitbox = GetComponentInChildren<AttackHitbox>(true);
        hitFeedback = GetComponent<HitFeedback>();
        if (hitFeedback == null)
            hitFeedback = gameObject.AddComponent<HitFeedback>();
        currentHp = Mathf.Max(1, maxHp);
    }

    private void Start()
    {
        if (cameraTransform != null)
        {
            cachedViewTransform = cameraTransform;
        }
        else
        {
            var rig = UnityEngine.Object.FindObjectOfType<ThirdPersonCameraRig>();
            if (rig != null)
                cachedViewTransform = rig.transform;
        }
    }

    // 输入、移动、跳跃和攻击都在 Update 中集中推进，方便与动画参数同步。
    private void Update()
    {
        if (IsDead)
        {
            StopMovementAnimation();
            return;
        }

        // 同时兼容 Unity 轴输入和直接按键输入，轴输入优先。
        float verticalAxis = Input.GetAxisRaw("Vertical");
        float horizontalAxis = Input.GetAxisRaw("Horizontal");

        float verticalKey = (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f);
        float horizontalKey = (Input.GetKey(KeyCode.D) ? 1f : 0f) + (Input.GetKey(KeyCode.A) ? -1f : 0f);

        float vertical = Mathf.Abs(verticalAxis) > 0.001f ? verticalAxis : verticalKey;
        float horizontal = Mathf.Abs(horizontalAxis) > 0.001f ? horizontalAxis : horizontalKey;

        bool isGrounded = characterController.isGrounded;

        if (Input.GetKeyDown(KeyCode.LeftShift)) runValue = 1f;
        if (Input.GetKeyUp(KeyCode.LeftShift)) runValue = 0.5f;

        bool hasForward = vertical > 0.01f;
        bool hasBack = vertical < -0.01f;
        bool hasWS = hasForward || hasBack;
        bool hasAD = Mathf.Abs(horizontal) > 0.01f;
        bool onlyADCandidate = !hasWS && hasAD;

        if (onlyADCandidate)
        {
            if (!adYawActive && adOnlyStartedAt < 0f)
                adOnlyStartedAt = Time.time;
        }
        else
        {
            adOnlyStartedAt = -1f;
            adYawActive = false;
        }

        bool onlyAD = onlyADCandidate &&
            (adYawActive || Time.time - adOnlyStartedAt >= Mathf.Max(0f, inputBufferTime));

        if (hasWS && !wsYawActive)
        {
            wsYawActive = true;
            wsYawOffset = 0f;
        }

        if (!hasWS)
            wsYawActive = false;

        // 仅按 A/D：经过短暂输入缓冲后，锁定到镜头朝向左右两侧的目标角度。
        if (onlyAD && !adYawActive)
        {
            adYawActive = true;
            adYawBase = GetCameraYawDegrees();
            adYawOffset = (horizontal > 0f ? 1f : -1f) * Mathf.Max(0f, adTurnAngle);
        }

        Vector3 desiredMove = Vector3.zero;

        if (hasWS && wsYawActive)
        {
            float maxYaw = Mathf.Max(0f, wsMaxYawOffset);
            float yawDelta = horizontal * wsTurnSpeed * Time.deltaTime;
            wsYawOffset = Mathf.Clamp(wsYawOffset + yawDelta, -maxYaw, maxYaw);

            float camYaw = GetCameraYawDegrees();
            float yawTarget = camYaw + wsYawOffset;
            Quaternion targetRot = Quaternion.Euler(0f, yawTarget, 0f);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                wsFaceTurnSpeed * Time.deltaTime);

            Vector3 moveDir = targetRot * Vector3.forward;
            desiredMove = moveDir * vertical * moveSpeed;

            animator.SetFloat("y", vertical * runValue);
            animator.SetFloat("x", 0f);
        }
        else if (onlyAD && adYawActive)
        {
            float desiredOffset = (horizontal > 0f ? 1f : -1f) * Mathf.Max(0f, adTurnAngle);
            if (!Mathf.Approximately(Mathf.Sign(adYawOffset), Mathf.Sign(desiredOffset)))
            {
                adYawBase = GetCameraYawDegrees();
                adYawOffset = desiredOffset;
            }

            float yawTarget = adYawBase + adYawOffset;
            Quaternion targetRot = Quaternion.Euler(0f, yawTarget, 0f);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                adTurnSpeed * Time.deltaTime);

            float yawError = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, yawTarget));
            desiredMove = yawError <= Mathf.Max(0f, adMoveStartAngle)
                ? targetRot * Vector3.forward * moveSpeed
                : Vector3.zero;

            animator.SetFloat("y", desiredMove.sqrMagnitude > 0.0001f ? runValue : 0f);
            animator.SetFloat("x", 0f);
        }
        else if (onlyADCandidate)
        {
            desiredMove = Vector3.zero;
            animator.SetFloat("y", 0f);
            animator.SetFloat("x", 0f);
        }
        else
        {
            desiredMove = transform.forward * vertical * moveSpeed + transform.right * horizontal * moveSpeed;

            Vector3 moveDir = (transform.forward * vertical + transform.right * horizontal).normalized;
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
            }

            animator.SetFloat("y", vertical * runValue);
            animator.SetFloat("x", horizontal);
        }

        MaintainAttackAssistFacing();

        // 对水平速度做指数平滑，让起步和停下更柔和。
        Vector3 move = SmoothHorizontalMove(desiredMove);

        // 跳跃使用缓冲输入和土狼时间，降低“差一点按到”的挫败感。
        if (isGrounded)
        {
            lastGroundedAt = Time.time;
            if (velocity.y < 0f)
                velocity.y = -1f;
        }

        if (Input.GetKeyDown(KeyCode.Space))
            lastJumpPressedAt = Time.time;

        bool hasBufferedJump = Time.time - lastJumpPressedAt <= Mathf.Max(0f, jumpBufferTime);
        bool canUseCoyoteJump = Time.time - lastGroundedAt <= Mathf.Max(0f, coyoteTime);
        if (hasBufferedJump && canUseCoyoteJump)
        {
            velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
            lastJumpPressedAt = -999f;
            lastGroundedAt = -999f;
            animator.SetTrigger("Jump");
        }
        else
        {
            float gravityMultiplier = velocity.y < 0f ? Mathf.Max(1f, fallGravityMultiplier) : 1f;
            velocity.y += gravity * gravityMultiplier * Time.deltaTime;
        }

        if (Input.GetKeyUp(KeyCode.Space) && velocity.y > 0f)
            velocity.y *= Mathf.Clamp01(lowJumpVelocityMultiplier);

        characterController.Move((move + velocity) * Time.deltaTime);

        // 连击窗口过期后复位，避免角色一直保持攻击状态。
        if (comboCount != 0 && Time.time > comboExpiresAt)
        {
            comboCount = 0;
            pendingComboInput = false;
            CancelQueuedAttackHitbox();
            animator.SetBool("IsAttacking", false);
        }

        // 鼠标左键推进三段攻击，并开启一次攻击判定盒。
        if (Input.GetMouseButtonDown(0))
            RegisterComboAttackInput();

        TryConsumePendingComboInput();
    }

    private void RegisterComboAttackInput()
    {
        pendingComboInput = true;
        pendingComboInputExpiresAt = Time.time + attackInputBufferTime;
        TryConsumePendingComboInput();
    }

    private void TryConsumePendingComboInput()
    {
        if (!pendingComboInput || Time.time > pendingComboInputExpiresAt)
        {
            pendingComboInput = false;
            return;
        }

        if (!TryAdvanceComboAttack())
            return;

        pendingComboInput = false;

        ApplyAttackAssistFacing(snap: true);

        animator.SetBool("IsAttacking", true);
        comboExpiresAt = Time.time + comboWindow;

        if (attackHitbox != null)
            attackHitbox.CancelIdleClose();

        QueueAttackHitbox();
    }

    private bool TryAdvanceComboAttack()
    {
        if (animator == null)
            return false;

        if (Time.time - lastAttackInputAt < attackInputMinInterval)
            return false;

        lastAttackInputAt = Time.time;
        comboCount++;
        if (comboCount > 3)
            comboCount = 1;

        FireComboTrigger(comboCount);
        return true;
    }

    private void FireComboTrigger(int step)
    {
        animator.ResetTrigger(Atk1Hash);
        animator.ResetTrigger(Atk2Hash);
        animator.ResetTrigger(Atk3Hash);

        switch (step)
        {
            case 1:
                animator.SetTrigger(Atk1Hash);
                break;
            case 2:
                animator.SetTrigger(Atk2Hash);
                break;
            case 3:
                animator.SetTrigger(Atk3Hash);
                break;
        }
    }

    /// <summary>
    /// 玩家承受一次伤害，物理伤害由护甲减免，魔法伤害由魔抗减免。
    /// </summary>
    public bool TryTakeDamage(DamageInfo damage)
    {
        if (IsDead) return false;
        if (Time.time - lastDamagedAt < damageInvulnSeconds) return false;

        int appliedDamage = CalculateDamageAfterDefense(damage);
        if (appliedDamage <= 0) return false;

        lastDamagedAt = Time.time;
        currentHp = Mathf.Max(0, currentHp - appliedDamage);
        PlayHitFeedback(damage);

        if (IsDead)
            OnDeath();

        return true;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
    }

    public void RestoreFullHealth()
    {
        currentHp = Mathf.Max(1, maxHp);
    }

    private void PlayHitFeedback(DamageInfo damage)
    {
        if (!enableHitFeedback)
            return;

        if (hitFeedback != null)
            hitFeedback.Play(animator, hitTriggerParam, hitBoolParam, hitBoolSeconds, damage);

        if (cancelAttackOnHit)
        {
            comboCount = 0;
            if (animator != null)
                animator.SetBool("IsAttacking", false);
        }
    }

    private int CalculateDamageAfterDefense(DamageInfo damage)
    {
        int rawDamage = Mathf.Max(0, damage.amount);
        if (rawDamage == 0) return 0;

        int defense = 0;
        switch (damage.damageType)
        {
            case DamageType.Physical:
                defense = armor;
                break;
            case DamageType.Magic:
                defense = magicResistance;
                break;
            case DamageType.TrueDamage:
                defense = 0;
                break;
        }

        return Mathf.Max(1, rawDamage - Mathf.Max(0, defense));
    }

    private void OnDeath()
    {
        comboCount = 0;
        CancelQueuedAttackHitbox();
        velocity = Vector3.zero;
        smoothedMove = Vector3.zero;
        StopMovementAnimation();
        Died?.Invoke();
    }

    private void StopMovementAnimation()
    {
        if (animator == null) return;

        animator.SetFloat("x", 0f);
        animator.SetFloat("y", 0f);
        animator.SetBool("IsAttacking", false);
    }

    private static float NormalizeAngle(float a)
    {
        if (a > 180f) a -= 360f;
        return a;
    }

    /// <summary>
    /// 视角参考物在水平面上的朝向角（度）。优先使用 ThirdPersonCameraRig，否则使用 Camera.main。
    /// </summary>
    private float GetCameraYawDegrees()
    {
        Transform view = cameraTransform != null
            ? cameraTransform
            : (cachedViewTransform != null ? cachedViewTransform : (Camera.main != null ? Camera.main.transform : null));

        if (view == null)
            return NormalizeAngle(transform.eulerAngles.y);

        Vector3 flat = view.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 1e-6f)
            return NormalizeAngle(transform.eulerAngles.y);

        flat.Normalize();
        return NormalizeAngle(Quaternion.LookRotation(flat).eulerAngles.y);
    }

    /// <summary>
    /// 根据加速/减速响应时间平滑水平移动速度。
    /// </summary>
    private Vector3 SmoothHorizontalMove(Vector3 desiredMove)
    {
        float responseTime = desiredMove.sqrMagnitude > smoothedMove.sqrMagnitude
            ? accelerationTime
            : decelerationTime;

        float t = responseTime <= 0f
            ? 1f
            : 1f - Mathf.Exp(-Time.deltaTime / responseTime);

        smoothedMove = Vector3.Lerp(smoothedMove, desiredMove, t);
        if (desiredMove.sqrMagnitude < 0.0001f && smoothedMove.sqrMagnitude < 0.0001f)
            smoothedMove = Vector3.zero;

        return smoothedMove;
    }


    public bool HasEnemyInAttackAssistRange()
    {
        return enableAttackAssist &&
            Monster.TryFindNearestLiving(transform.position, attackAssistRadius, out _, out _);
    }

    private void ApplyAttackAssistFacing(bool snap = false)
    {
        if (!TryGetAttackAssistYaw(out float targetYaw))
            return;

        Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
        transform.rotation = snap
            ? targetRot
            : Quaternion.RotateTowards(transform.rotation, targetRot, attackFaceTurnSpeed * Time.deltaTime);

        adYawActive = false;
        adOnlyStartedAt = -1f;
        wsYawActive = false;
        wsYawOffset = 0f;
    }

    private void MaintainAttackAssistFacing()
    {
        if (!IsAttacking)
            return;

        ApplyAttackAssistFacing(snap: false);
    }

    private bool TryGetAttackAssistYaw(out float targetYaw)
    {
        targetYaw = 0f;

        if (!enableAttackAssist)
            return false;

        if (!Monster.TryFindNearestLiving(transform.position, attackAssistRadius, out Monster enemy, out _))
            return false;

        Vector3 toEnemy = enemy.GetAttackAssistFacingPoint() - transform.position;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.0001f)
            return false;

        float yawToEnemy = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;
        targetYaw = yawToEnemy + attackYawOffset;
        return true;
    }

    private void QueueAttackHitbox()
    {
        attackHitboxEventReceived = false;

        if (queuedHitboxRoutine != null)
            StopCoroutine(queuedHitboxRoutine);

        if (!useFallbackHitbox || attackHitbox == null)
            return;

        queuedHitboxRoutine = StartCoroutine(OpenFallbackHitboxAfterDelay());
    }

    private IEnumerator OpenFallbackHitboxAfterDelay()
    {
        if (fallbackHitboxDelay > 0f)
            yield return new WaitForSeconds(fallbackHitboxDelay);

        queuedHitboxRoutine = null;

        if (!attackHitboxEventReceived && attackHitbox != null)
            attackHitbox.ActivateForSeconds(fallbackHitboxSeconds);
    }

    private void CancelQueuedAttackHitbox()
    {
        if (queuedHitboxRoutine != null)
        {
            StopCoroutine(queuedHitboxRoutine);
            queuedHitboxRoutine = null;
        }

        if (attackHitbox != null)
            attackHitbox.CloseHitbox();
    }

    public void AttackHitboxOpen()
    {
        attackHitboxEventReceived = true;

        if (queuedHitboxRoutine != null)
        {
            StopCoroutine(queuedHitboxRoutine);
            queuedHitboxRoutine = null;
        }

        if (attackHitbox != null)
            attackHitbox.OpenHitbox();
    }

    public void AttackHitboxClose()
    {
        attackHitboxEventReceived = true;
        if (attackHitbox != null)
            attackHitbox.ScheduleIdleClose(hitboxIdleCloseSeconds);
    }

    public void AttackHitboxPulse()
    {
        attackHitboxEventReceived = true;
        if (attackHitbox != null)
            attackHitbox.ActivateOnce();
    }
}
