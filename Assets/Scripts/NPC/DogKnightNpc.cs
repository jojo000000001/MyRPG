using UnityEngine;

/// <summary>
/// 狗骑士 NPC：第一次靠近会主动走来发布讨伐任务，不弹出商店选项；之后走进对话距离会自动出现商店对话。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class DogKnightNpc : MonoBehaviour
{
    private enum State
    {
        Idle,
        Approach,
        Talk
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRadius = 10f;
    [SerializeField] private float talkRadius = 2.2f;
    [SerializeField] private float loseRadius = 16f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float rotateSpeed = 360f;
    [SerializeField] private float gravity = -12f;
    [SerializeField] private string speedFloatParam = "Speed";

    [Header("Dialogue")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string speakerName = "狗骑士";
    [SerializeField] [TextArea(2, 4)] private string[] dialogueLines =
    {
        "旅人，你终于来了。",
        "这片林地并不平静，哥布林最近越发猖狂。",
        "有一只已经摸到这边了——帮我把它解决掉！"
    };

    [SerializeField] private State currentState = State.Idle;

    private CharacterController characterController;
    private Animator animator;
    private Vector3 verticalVelocity;
    private bool hasGreeted;
    private bool dialogueStarted;
    private bool interactionConsumedThisVisit;
    private int speedParamHash;
    private bool speedParamExists;

    public string CurrentStateName => currentState.ToString();
    public bool HasGreeted => hasGreeted;

    /// <summary>读档还原是否已完成开场对话。已打过招呼则不再主动走来发布第一任务。</summary>
    public void RestoreGreeting(bool greeted)
    {
        hasGreeted = greeted;
        dialogueStarted = greeted;
        if (greeted)
            currentState = State.Idle;
    }

    /// <summary>读档还原犬骑士站位。</summary>
    public void RestoreSavedPose(Vector3 position, float rotationY)
    {
        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));

        if (characterController != null)
            characterController.enabled = true;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        speedParamHash = Animator.StringToHash(speedFloatParam);
    }

    private void Update()
    {
        AcquireTarget();
        ApplyGravity();

        switch (currentState)
        {
            case State.Idle:
                UpdateIdle();
                break;
            case State.Approach:
                UpdateApproach();
                break;
            case State.Talk:
                UpdateTalk();
                break;
        }
    }

    private void LateUpdate()
    {
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void UpdateIdle()
    {
        SetLocomotionSpeed01(0f);
        MoveVerticalOnly();

        if (!HasValidTarget())
            return;

        float distance = GetPlanarDistance(transform.position, target.position);
        if (!hasGreeted)
        {
            if (distance <= detectRadius)
                EnterState(State.Approach);
            return;
        }

        if (distance <= talkRadius)
            EnterState(State.Talk);
    }

    private void UpdateApproach()
    {
        if (!HasValidTarget())
        {
            EnterState(State.Idle);
            return;
        }

        float distance = GetPlanarDistance(transform.position, target.position);
        if (distance > loseRadius)
        {
            EnterState(State.Idle);
            return;
        }

        FaceTarget();

        if (distance <= talkRadius)
        {
            EnterState(State.Talk);
            return;
        }

        MoveToward(target.position);
    }

    private void UpdateTalk()
    {
        SetLocomotionSpeed01(0f);

        if (!HasValidTarget() || GetPlanarDistance(transform.position, target.position) > GetLeaveTalkRadius())
        {
            EnterState(State.Idle);
            return;
        }

        FaceTarget();
        MoveVerticalOnly();

        if (!hasGreeted && !dialogueStarted)
        {
            dialogueStarted = true;
            DialogueUI.Ensure().Show(speakerName, dialogueLines, OnGreetingFinished);
            return;
        }

        TryOfferShopDialogue();
    }

    private void TryOfferShopDialogue()
    {
        if (!hasGreeted || DialogueUI.IsOpen || ShopUI.IsOpen || GameplayPauseMenu.IsOpen)
            return;

        PlayerHUD hud = PlayerHUD.Resolve();
        if (hud != null && hud.IsInventoryOpen)
            return;

        bool pressedInteract = Input.GetKeyDown(interactKey);
        if (!pressedInteract && interactionConsumedThisVisit)
            return;

        interactionConsumedThisVisit = true;
        OfferInteractChoices();
    }

    private void OnGreetingFinished()
    {
        hasGreeted = true;
        interactionConsumedThisVisit = true;
        QuestManager.Ensure().StartHuntInvadingGoblin(transform);
    }

    private void OfferInteractChoices()
    {
        if (DialogueUI.IsOpen || ShopUI.IsOpen)
            return;

        DialogueUI.Ensure().ShowChoices(
            speakerName,
            "需要点补给吗？我这儿还有些货物。",
            new[] { "商店", "离开" },
            OnInteractChoice,
            false);
    }

    private void OnInteractChoice(int index)
    {
        if (index != 0)
            return;

        OpenShop();
    }

    private static void OpenShop()
    {
        ShopUI shop = ShopUI.Ensure();
        if (shop != null)
            shop.Open();
    }

    private void EnterState(State nextState)
    {
        if (currentState == nextState)
            return;

        currentState = nextState;

        if (currentState == State.Talk)
            interactionConsumedThisVisit = false;

        if (currentState != State.Approach)
            SetLocomotionSpeed01(0f);
    }

    private void MoveToward(Vector3 destination)
    {
        Vector3 position = transform.position;
        Vector3 toTarget = new Vector3(destination.x, position.y, destination.z) - position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= 0.05f)
        {
            SetLocomotionSpeed01(0f);
            MoveVerticalOnly();
            return;
        }

        Vector3 direction = toTarget / distance;
        SetLocomotionSpeed01(1f);
        characterController.Move((direction * moveSpeed + verticalVelocity) * Time.deltaTime);
    }

    private void MoveVerticalOnly()
    {
        if (characterController != null)
            characterController.Move(verticalVelocity * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -1f;
        else
            verticalVelocity.y += gravity * Time.deltaTime;
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, rotateSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void SetLocomotionSpeed01(float speed01)
    {
        if (animator == null || !HasSpeedParameter())
            return;

        animator.SetFloat(speedParamHash, Mathf.Clamp01(speed01));
    }

    private bool HasSpeedParameter()
    {
        if (speedParamExists)
            return true;

        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == speedParamHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                speedParamExists = true;
                return true;
            }
        }

        return false;
    }

    private void AcquireTarget()
    {
        if (target != null)
            return;

        Player player = Player.Resolve();

        if (player != null && !player.IsDead)
            target = player.transform;
    }

    private bool HasValidTarget()
    {
        if (target == null)
            return false;

        Player player = target.GetComponent<Player>();
        return player == null || !player.IsDead;
    }

    private float GetLeaveTalkRadius()
    {
        if (!hasGreeted)
            return loseRadius;

        return Mathf.Max(talkRadius + 2.5f, talkRadius * 2f);
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        Gizmos.color = new Color(0.3f, 0.75f, 1f, 0.7f);
        DrawRadius(origin, detectRadius);
        Gizmos.color = new Color(0.95f, 0.8f, 0.2f, 0.9f);
        DrawRadius(origin, talkRadius);
        Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.45f);
        DrawRadius(origin, loseRadius);
    }

    private static void DrawRadius(Vector3 origin, float radius)
    {
        const int segments = 32;
        Vector3 previous = origin + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
