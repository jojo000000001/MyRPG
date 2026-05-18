using UnityEngine;

/// <summary>
/// 基础巡逻怪物：围绕出生点随机游走，受击死亡后停止移动并销毁。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Goblin : Monster
{
    [Header("Wander")]
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotateSpeed = 540f;
    [SerializeField] private float gravity = -10f;
    [SerializeField] private float waitAtPointMin = 0.75f;
    [SerializeField] private float waitAtPointMax = 2.5f;
    [SerializeField] private float arrivalThreshold = 0.2f;
    [SerializeField] private bool disableRootCapsuleCollider = true;

    [Header("Death")]
    [SerializeField] private float destroyAfterDeathSeconds = 0.5f;

    private CharacterController characterController;
    private Vector3 home;
    private Vector3 targetXZ;
    private float waitUntil;
    private Vector3 moveVelocity;

    // 简单状态机：等待一段时间后选择新目标，再移动到目标点。
    private enum WanderPhase { Waiting, Moving }
    private WanderPhase wanderPhase = WanderPhase.Waiting;

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
        ScheduleWait();
    }

    // 每帧根据当前巡逻阶段推进等待或移动逻辑。
    private void Update()
    {
        if (IsDead || characterController == null)
        {
            SetLocomotionSpeed01(0f);
            return;
        }

        ApplyGravity();

        switch (wanderPhase)
        {
            case WanderPhase.Waiting:
                UpdateWaiting();
                break;
            case WanderPhase.Moving:
                UpdateMoving();
                break;
        }
    }

    private void UpdateWaiting()
    {
        SetLocomotionSpeed01(0f);

        if (Time.time >= waitUntil)
        {
            PickTarget();
            wanderPhase = WanderPhase.Moving;
        }

        characterController.Move(moveVelocity * Time.deltaTime);
    }

    private void UpdateMoving()
    {
        Vector3 position = transform.position;
        Vector3 toTarget = new Vector3(targetXZ.x, position.y, targetXZ.z) - position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= arrivalThreshold)
        {
            ScheduleWait();
            SetLocomotionSpeed01(0f);
            characterController.Move(moveVelocity * Time.deltaTime);
            return;
        }

        Vector3 direction = toTarget / distance;
        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, rotateSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        SetLocomotionSpeed01(1f);
        Vector3 move = direction * moveSpeed;
        characterController.Move((move + moveVelocity) * Time.deltaTime);
    }

    // CharacterController 不会自动受重力影响，这里手动累计竖直速度。
    private void ApplyGravity()
    {
        bool grounded = characterController.isGrounded;
        if (grounded && moveVelocity.y < 0f)
            moveVelocity.y = -1f;
        else
            moveVelocity.y += gravity * Time.deltaTime;
    }

    private void ScheduleWait()
    {
        float maxWait = Mathf.Max(waitAtPointMin, waitAtPointMax);
        waitUntil = Time.time + Random.Range(waitAtPointMin, maxWait);
        wanderPhase = WanderPhase.Waiting;
    }

    // 在出生点周围随机选一个水平目标点。
    private void PickTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
        targetXZ = home + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private void LateUpdate()
    {
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    // 死亡后停掉移动控制器，避免销毁前继续被寻路或重力推动。
    protected override void OnDeath()
    {
        base.OnDeath();
        wanderPhase = WanderPhase.Waiting;
        moveVelocity = Vector3.zero;

        if (characterController != null)
            characterController.enabled = false;

        Destroy(gameObject, Mathf.Max(0f, destroyAfterDeathSeconds));
    }
}
