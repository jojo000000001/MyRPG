using UnityEngine;

/// <summary>
/// 第三人称相机支架：跟随目标、处理水平环绕、俯仰角和肩位偏移。
/// </summary>
public class ThirdPersonCameraRig : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 0.62f, 0f);

    [Header("Look")]
    [SerializeField] private bool lockYawToTarget = false;
    [SerializeField] private bool requireRightMouseToLook = true;
    [SerializeField] private float yawSpeed = 250f;

    [Header("Pitch")]
    [SerializeField] private float pitchSpeed = 80f;
    [SerializeField] private float minPitch = -15f;
    [SerializeField] private float maxPitch = 40f;
    [SerializeField] private float initialPitch = 5f;

    [Header("Distance")]
    [SerializeField] private float distance = 3.8f;
    [SerializeField] private float shoulder = 1f;

    [Header("Smoothing")]
    [SerializeField] private float followSmooth = 12f;
    [SerializeField] private float yawFollowSmooth = 6f;

    [Header("Combat Assist")]
    [SerializeField] private float combatYawFollowSmooth = 5.5f;

    [Header("Refs")]
    [SerializeField] private Transform pivot;
    [SerializeField] private Camera cam;

    // yaw/pitch 保存当前相机角度，避免直接从 Transform 反推导致角度跳变。
    private float yaw;
    private float pitch;
    private Player player;
    private bool cinematicLook;
    private float cinematicYaw;
    private float cinematicPitch;
    private Vector3 cinematicFocusPoint;
    private float cinematicStartedAt;
    private float cinematicZoomInSeconds;
    private float cinematicHoldSeconds;
    private float cinematicZoomOutSeconds;
    private Transform cinematicFocusTarget;
    private Vector3 cinematicFocusWorldOffset;
    private float cinematicStandoffMin = 10f;
    private float cinematicStandoffMax = 22f;
    private float cinematicHeightBias = 1.1f;
    private float cinematicHeightMix = 0.55f;

    public bool IsCinematicLook => cinematicLook;

    private void Reset()
    {
        cam = GetComponentInChildren<Camera>();
    }

    private void Awake()
    {
        if (!pivot && transform.childCount > 0) pivot = transform.GetChild(0);
        if (!cam) cam = GetComponentInChildren<Camera>();

        if (!target)
        {
            var playerObject = GameObject.Find("Player");
            if (playerObject) target = playerObject.transform;
        }

        pitch = initialPitch;
        yaw = target ? target.eulerAngles.y : transform.eulerAngles.y;
        player = target != null ? target.GetComponent<Player>() : null;
    }

    private void Start()
    {
        if (SaveSession.PendingLoadSlot.HasValue)
            SnapToTarget();
        else
            SnapLookAtKnight();
    }

    public void SnapToTarget()
    {
        SnapImmediate();
    }

    /// <summary>
    /// 开局将镜头水平对准场景中的骑士，不改变俯仰。
    /// </summary>
    public void SnapLookAtKnight()
    {
        DogKnightNpc knight = Object.FindObjectOfType<DogKnightNpc>();
        if (knight == null)
        {
            SnapImmediate();
            return;
        }

        SnapLookAtWorldPointYaw(knight.transform.position);
    }

    /// <summary>
    /// 将镜头转向世界坐标点，沿该方向拉近后再回到正常第三人称。
    /// </summary>
    public void LookAtWorldPoint(Vector3 worldPoint, float duration)
    {
        float safeDuration = Mathf.Max(0.6f, duration);
        LookAtWorldPoint(worldPoint, safeDuration * 0.42f, safeDuration * 0.12f, safeDuration * 0.46f);
    }

    public void LookAtWorldPoint(Vector3 worldPoint, float zoomInSeconds, float holdSeconds, float zoomOutSeconds)
    {
        cinematicFocusTarget = null;
        cinematicStandoffMin = 10f;
        cinematicStandoffMax = 22f;
        cinematicHeightBias = 1.1f;
        cinematicHeightMix = 0.55f;
        BeginCinematicLook(worldPoint, zoomInSeconds, holdSeconds, zoomOutSeconds);
    }

    /// <summary>
    /// 过场镜头跟随一个物体，焦点随目标移动（例如巨龙落地）。
    /// </summary>
    public void LookAtFollow(
        Transform focus,
        Vector3 worldOffset,
        float zoomInSeconds,
        float holdSeconds,
        float zoomOutSeconds,
        float standoffMin = 12f,
        float standoffMax = 18f,
        float heightBias = 0.25f)
    {
        cinematicFocusTarget = focus;
        cinematicFocusWorldOffset = worldOffset;
        cinematicStandoffMin = Mathf.Max(4f, standoffMin);
        cinematicStandoffMax = Mathf.Max(cinematicStandoffMin, standoffMax);
        cinematicHeightBias = heightBias;
        cinematicHeightMix = 0.38f;
        BeginCinematicLook(ResolveCinematicFocusPoint(), zoomInSeconds, holdSeconds, zoomOutSeconds);
    }

    private void BeginCinematicLook(Vector3 worldPoint, float zoomInSeconds, float holdSeconds, float zoomOutSeconds)
    {
        if (target == null)
            return;

        cinematicFocusPoint = worldPoint;
        RefreshCinematicAim();
        Vector3 to = cinematicFocusPoint - (target.position + pivotOffset);
        if (to.sqrMagnitude < 0.01f)
            return;

        cinematicZoomInSeconds = Mathf.Max(0.2f, zoomInSeconds);
        cinematicHoldSeconds = Mathf.Max(0f, holdSeconds);
        cinematicZoomOutSeconds = Mathf.Max(0.2f, zoomOutSeconds);
        cinematicStartedAt = Time.time;
        cinematicLook = true;
    }

    // 相机跟随放在 LateUpdate，等角色本帧移动完成后再更新镜头位置。
    private void LateUpdate()
    {
        if (!target || !pivot || !cam) return;

        bool allowLook = !GameplayCursor.BlocksWorldLook
            && (!requireRightMouseToLook || Input.GetMouseButton(1));
        bool freezeLook = GameplayCursor.BlocksWorldLook;
        bool combatAssist = !freezeLook && player != null && player.IsCombatCameraAssistActive;
        float zoomBlend = 0f;
        bool returningToPlayer = false;

        if (cinematicLook)
        {
            RefreshCinematicAim();
            float zoomIn = cinematicZoomInSeconds;
            float hold = cinematicHoldSeconds;
            float zoomOut = cinematicZoomOutSeconds;
            float elapsed = Time.time - cinematicStartedAt;
            float zoomOutStartsAt = zoomIn + hold;
            float cinematicLength = zoomOutStartsAt + zoomOut;

            if (elapsed >= cinematicLength)
            {
                cinematicLook = false;
                cinematicFocusTarget = null;
            }
            else if (elapsed < zoomIn)
            {
                zoomBlend = Smooth01(elapsed / zoomIn);
            }
            else if (elapsed < zoomOutStartsAt)
            {
                zoomBlend = 1f;
            }
            else
            {
                returningToPlayer = true;
                zoomBlend = 1f - Smooth01((elapsed - zoomOutStartsAt) / zoomOut);
            }
        }

        if (cinematicLook && !returningToPlayer)
        {
            float t = 1f - Mathf.Exp(-Mathf.Max(4f, yawFollowSmooth) * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, cinematicYaw, t);
            pitch = Mathf.Lerp(pitch, cinematicPitch, t);
        }
        else if (freezeLook)
        {
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
        else
        {
            float mx;
            float my;
            CameraLookInput.Read(out mx, out my);

            if (combatAssist)
            {
                yaw = Mathf.LerpAngle(
                    yaw,
                    player.CombatCameraYaw,
                    1f - Mathf.Exp(-combatYawFollowSmooth * Time.deltaTime));
            }
            else if (lockYawToTarget || !allowLook)
            {
                yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1f - Mathf.Exp(-yawFollowSmooth * Time.deltaTime));
            }
            else
            {
                yaw += CameraLookInput.Apply(mx, yawSpeed);
            }

            if (allowLook)
                pitch -= CameraLookInput.Apply(my, pitchSpeed);

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        Vector3 desiredRigPos = target.position + pivotOffset;
        transform.position = Vector3.Lerp(transform.position, desiredRigPos, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float liveShoulder = Mathf.Lerp(shoulder, 0.15f, zoomBlend);
        Vector3 shoulderOffset = transform.right * liveShoulder;
        Vector3 followCamPos = pivot.TransformPoint(new Vector3(0f, 0f, -distance)) + shoulderOffset;
        Quaternion followCamRot = Quaternion.LookRotation((pivot.position + shoulderOffset) - followCamPos, Vector3.up);

        if (zoomBlend > 0.001f)
        {
            Vector3 focusCamPos;
            Quaternion focusCamRot;
            ComputeFocusCameraPose(out focusCamPos, out focusCamRot);
            cam.transform.position = Vector3.Lerp(followCamPos, focusCamPos, zoomBlend);
            cam.transform.rotation = Quaternion.Slerp(followCamRot, focusCamRot, zoomBlend);
        }
        else
        {
            cam.transform.position = followCamPos;
            cam.transform.rotation = followCamRot;
        }
    }

    private void ComputeFocusCameraPose(out Vector3 cameraPosition, out Quaternion cameraRotation)
    {
        Vector3 playerPivot = target.position + pivotOffset;
        Vector3 toFocus = cinematicFocusPoint - playerPivot;
        float totalDistance = toFocus.magnitude;
        if (totalDistance < 0.01f)
        {
            cameraPosition = cam.transform.position;
            cameraRotation = cam.transform.rotation;
            return;
        }

        Vector3 direction = toFocus / totalDistance;
        float standoff = Mathf.Clamp(totalDistance * 0.42f, cinematicStandoffMin, cinematicStandoffMax);
        standoff = Mathf.Min(standoff, Mathf.Max(cinematicStandoffMin * 0.75f, totalDistance - 6f));
        cameraPosition = cinematicFocusPoint - direction * standoff;
        cameraPosition.y = Mathf.Lerp(playerPivot.y + 1.4f, cinematicFocusPoint.y + cinematicHeightBias, cinematicHeightMix);
        Vector3 look = cinematicFocusPoint - cameraPosition;
        if (look.sqrMagnitude < 0.01f)
            look = direction;
        cameraRotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    private void RefreshCinematicAim()
    {
        cinematicFocusPoint = ResolveCinematicFocusPoint();
        if (target == null)
            return;

        Vector3 to = cinematicFocusPoint - (target.position + pivotOffset);
        if (to.sqrMagnitude < 0.01f)
            return;

        cinematicYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        float horizontal = new Vector3(to.x, 0f, to.z).magnitude;
        float elevation = Mathf.Atan2(to.y, Mathf.Max(0.01f, horizontal)) * Mathf.Rad2Deg;
        cinematicPitch = Mathf.Clamp(-elevation, -32f, maxPitch);
    }

    private Vector3 ResolveCinematicFocusPoint()
    {
        if (cinematicFocusTarget == null)
            return cinematicFocusPoint;

        return cinematicFocusTarget.position + cinematicFocusWorldOffset;
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    // 启动时直接贴到目标位置，避免相机从原点平滑飞过去。
    private void SnapImmediate()
    {
        if (!target || !pivot || !cam) return;

        yaw = target.eulerAngles.y;
        ApplySnapPose();
    }

    private void SnapLookAtWorldPointYaw(Vector3 worldPoint)
    {
        if (!target || !pivot || !cam) return;

        Vector3 to = worldPoint - (target.position + pivotOffset);
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        else
            yaw = target.eulerAngles.y;

        ApplySnapPose();
    }

    private void ApplySnapPose()
    {
        transform.position = target.position + pivotOffset;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 shoulderOffset = transform.right * shoulder;
        Vector3 camLocal = new Vector3(0f, 0f, -distance);
        cam.transform.position = pivot.TransformPoint(camLocal) + shoulderOffset;
        cam.transform.rotation = Quaternion.LookRotation((pivot.position + shoulderOffset) - cam.transform.position, Vector3.up);
    }

    /// <summary>采集当前镜头水平角和俯仰，写入存档。</summary>
    public void CaptureLook(out float yawValue, out float pitchValue)
    {
        yawValue = yaw;
        pitchValue = pitch;
    }

    /// <summary>读档还原镜头朝向并立刻贴到目标上。</summary>
    public void ApplyLook(float yawValue, float pitchValue)
    {
        yaw = yawValue;
        pitch = Mathf.Clamp(pitchValue, minPitch, maxPitch);
        ApplySnapPose();
    }
}
