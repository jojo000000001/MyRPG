using UnityEngine;

/// <summary>
/// Basic enemy finite state machine: patrol, chase, attack, return home, and death.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Goblin : Monster
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRadius = 7f;
    [SerializeField] private float loseRadius = 11f;
    [SerializeField] private float leashRadius = 14f;
    [SerializeField] private float fieldOfView = 140f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask lineOfSightBlockers = ~0;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Patrol")]
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float waitAtPointMin = 0.75f;
    [SerializeField] private float waitAtPointMax = 2.5f;
    [SerializeField] private float arrivalThreshold = 0.2f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 3.4f;
    [SerializeField] private float returnSpeed = 3.2f;
    [SerializeField] private float rotateSpeed = 540f;
    [SerializeField] private float gravity = -10f;
    [SerializeField] private bool disableRootCapsuleCollider = true;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 8;
    [SerializeField] private DamageType attackDamageType = DamageType.Physical;
    [SerializeField] private float attackRange = 1.35f;
    [SerializeField] private float attackHitGraceRange = 0.35f;
    [SerializeField] private float attackCooldown = 1.25f;
    [SerializeField] private float attackWindupSeconds = 0.25f;
    [SerializeField] private float attackLockSeconds = 0.55f;
    [SerializeField] private string attackTriggerParam = "";

    [Header("Death")]
    [SerializeField] private float destroyAfterDeathSeconds = 0.5f;

    private enum State
    {
        PatrolWait,
        PatrolMove,
        Chase,
        Attack,
        ReturnHome,
        Dead
    }

    [SerializeField] private State currentState = State.PatrolWait;

    private CharacterController characterController;
    private Vector3 home;
    private Vector3 patrolTarget;
    private Vector3 verticalVelocity;
    private float waitUntil;
    private float attackStartedAt = -999f;
    private float nextAttackAt = -999f;
    private bool attackDamageApplied;

    public string CurrentStateName => currentState.ToString();

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

    private void Start()
    {
        home = transform.position;
        AcquireTarget();
        EnterState(State.PatrolWait);
    }

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
        attackCooldown = Mathf.Max(0.05f, attackCooldown);
        attackWindupSeconds = Mathf.Max(0f, attackWindupSeconds);
        attackLockSeconds = Mathf.Max(attackWindupSeconds, attackLockSeconds);
        destroyAfterDeathSeconds = Mathf.Max(0f, destroyAfterDeathSeconds);
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

    public override bool TryTakeDamage(DamageInfo damage)
    {
        bool accepted = base.TryTakeDamage(damage);
        if (!accepted || IsDead)
            return accepted;

        if (damage.source != null)
            target = damage.source.transform;

        EnterState(State.Chase);
        return true;
    }

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

    private void BeginAttack()
    {
        attackStartedAt = Time.time;
        nextAttackAt = Time.time + attackCooldown;
        attackDamageApplied = false;
        SetLocomotionSpeed01(0f);

        if (!string.IsNullOrEmpty(attackTriggerParam) && HasAnimatorParameter(attackTriggerParam, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(attackTriggerParam);
    }

    private void ApplyAttackDamage()
    {
        if (!HasValidTarget())
            return;

        float hitRange = attackRange + attackHitGraceRange;
        if (GetPlanarDistance(transform.position, target.position) > hitRange)
            return;

        IDamageable damageable = FindDamageable(target);
        if (damageable == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = transform.forward;

        var damage = new DamageInfo(attackDamage, gameObject, target.position, direction, attackDamageType);
        damageable.TryTakeDamage(damage);
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

        Vector3 direction = toTarget / distance;
        RotateToward(direction);

        SetLocomotionSpeed01(speed > 0f ? 1f : 0f);
        characterController.Move((direction * speed + verticalVelocity) * Time.deltaTime);
        return false;
    }

    private void MoveVerticalOnly()
    {
        if (characterController != null)
            characterController.Move(verticalVelocity * Time.deltaTime);
    }

    private void RotateToward(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, rotateSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        RotateToward(direction);
    }

    private void ApplyGravity()
    {
        bool grounded = characterController.isGrounded;
        if (grounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -1f;
        else
            verticalVelocity.y += gravity * Time.deltaTime;
    }

    private void ScheduleWait()
    {
        float maxWait = Mathf.Max(waitAtPointMin, waitAtPointMax);
        waitUntil = Time.time + Random.Range(waitAtPointMin, maxWait);
    }

    private void PickPatrolTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
        patrolTarget = home + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private void AcquireTarget()
    {
        if (target != null)
            return;

        var player = Object.FindObjectOfType<Player>();
        if (player != null)
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
        if (!HasValidTarget())
            return false;

        float distance = GetPlanarDistance(transform.position, target.position);
        if (distance > detectRadius)
            return false;

        if (!IsTargetInsideFieldOfView())
            return false;

        return !requireLineOfSight || HasLineOfSight();
    }

    private bool ShouldReturnHome()
    {
        if (!HasValidTarget())
            return true;

        float distanceToTarget = GetPlanarDistance(transform.position, target.position);
        float distanceFromHome = GetPlanarDistance(transform.position, home);

        return distanceToTarget > loseRadius || distanceFromHome > leashRadius;
    }

    private bool IsTargetInAttackRange()
    {
        return HasValidTarget() && GetPlanarDistance(transform.position, target.position) <= attackRange;
    }

    private bool IsTargetInsideFieldOfView()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f || fieldOfView >= 359f)
            return true;

        float angle = Vector3.Angle(transform.forward, toTarget);
        return angle <= fieldOfView * 0.5f;
    }

    private bool HasLineOfSight()
    {
        Vector3 origin = transform.position + eyeOffset;
        Vector3 destination = target.position + eyeOffset;
        Vector3 ray = destination - origin;

        if (!Physics.Raycast(origin, ray.normalized, out RaycastHit hit, ray.magnitude, lineOfSightBlockers, QueryTriggerInteraction.Ignore))
            return true;

        return hit.transform == target || hit.transform.IsChildOf(target);
    }

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
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void LateUpdate()
    {
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    protected override void OnDeath()
    {
        base.OnDeath();
        currentState = State.Dead;
        verticalVelocity = Vector3.zero;

        if (characterController != null)
            characterController.enabled = false;

        Destroy(gameObject, destroyAfterDeathSeconds);
    }
}
