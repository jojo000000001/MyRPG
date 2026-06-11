using UnityEngine;

/// <summary>
/// 第三人称相机支架：跟随目标、处理水平环绕、俯仰角和肩位偏移。
/// </summary>
public class ThirdPersonCameraRig : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.15f, 0f);

    [Header("Look")]
    [SerializeField] private bool lockYawToTarget = false;
    [SerializeField] private bool requireRightMouseToLook = true;
    [SerializeField] private float yawSpeed = 400f;

    [Header("Pitch")]
    [SerializeField] private float pitchSpeed = 140f;
    [SerializeField] private float minPitch = -15f;
    [SerializeField] private float maxPitch = 40f;
    [SerializeField] private float initialPitch = 4f;

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
            var player = GameObject.Find("Player");
            if (player) target = player.transform;
        }

        pitch = initialPitch;
        yaw = target ? target.eulerAngles.y : transform.eulerAngles.y;
        player = target != null ? target.GetComponent<Player>() : null;
    }

    private void Start()
    {
        SnapImmediate();
    }

    // 相机跟随放在 LateUpdate，等角色本帧移动完成后再更新镜头位置。
    private void LateUpdate()
    {
        if (!target || !pivot || !cam) return;

        bool allowLook = !requireRightMouseToLook || Input.GetMouseButton(1);
        bool combatAssist = player != null && player.IsCombatCameraAssistActive;

        // yaw：攻击辅助时镜头转到玩家背后；否则跟随主角或鼠标环绕。
        if (combatAssist)
        {
            yaw = Mathf.LerpAngle(
                yaw,
                target.eulerAngles.y,
                1f - Mathf.Exp(-combatYawFollowSmooth * Time.deltaTime));
        }
        else if (lockYawToTarget || !allowLook)
        {
            yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1f - Mathf.Exp(-yawFollowSmooth * Time.deltaTime));
        }
        else
        {
            float mx = Input.GetAxis("Mouse X");
            yaw += mx * yawSpeed * Time.deltaTime;
        }

        // pitch：上下看（Mouse Y）。
        if (allowLook)
        {
            float my = Input.GetAxis("Mouse Y");
            pitch -= my * pitchSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        Vector3 desiredRigPos = target.position + pivotOffset;
        transform.position = Vector3.Lerp(transform.position, desiredRigPos, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 shoulderOffset = transform.right * shoulder;
        Vector3 camLocal = new Vector3(0f, 0f, -distance);
        cam.transform.position = pivot.TransformPoint(camLocal) + shoulderOffset;
        cam.transform.rotation = Quaternion.LookRotation((pivot.position + shoulderOffset) - cam.transform.position, Vector3.up);
    }

    // 启动时直接贴到目标位置，避免相机从原点平滑飞过去。
    private void SnapImmediate()
    {
        if (!target || !pivot || !cam) return;

        yaw = target.eulerAngles.y;

        transform.position = target.position + pivotOffset;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 shoulderOffset = transform.right * shoulder;
        Vector3 camLocal = new Vector3(0f, 0f, -distance);
        cam.transform.position = pivot.TransformPoint(camLocal) + shoulderOffset;
        cam.transform.rotation = Quaternion.LookRotation((pivot.position + shoulderOffset) - cam.transform.position, Vector3.up);
    }
}
