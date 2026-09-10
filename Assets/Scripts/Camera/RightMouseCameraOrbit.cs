using System.Reflection;
using UnityEngine;

/// <summary>
/// 按住鼠标右键时，临时接管第三人称镜头并围绕玩家旋转；松开后恢复原相机逻辑。
/// 不修改 ThirdPersonCameraRig 本身的行为。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class RightMouseCameraOrbit : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ThirdPersonCameraRig baseRig;

    [Header("Orbit")]
    [SerializeField] private float yawSpeed = 250f;
    [SerializeField] private float pitchSpeed = 72f;
    [SerializeField] private float minPitch = -10f;
    [SerializeField] private float maxPitch = 35f;
    [SerializeField] private float holdBeforeOrbitSeconds = 0.16f;

    private bool orbiting;
    private float yaw;
    private float pitch;
    private float rightMouseDownAt = -999f;

    private void Awake()
    {
        if (baseRig == null)
            baseRig = GetComponent<ThirdPersonCameraRig>();
    }

    private void LateUpdate()
    {
        if (baseRig == null || baseRig.IsCinematicLook)
            return;

        if (Input.GetMouseButtonDown(1))
            rightMouseDownAt = Time.unscaledTime;

        Player player = Player.Instance;
        bool playerDodging = player != null && player.IsDodging;
        bool heldLongEnough = Time.unscaledTime - rightMouseDownAt >= Mathf.Max(0f, holdBeforeOrbitSeconds);
        bool wantOrbit = Input.GetMouseButton(1)
            && Cursor.lockState == CursorLockMode.Locked
            && !GameplayCursor.BlocksWorldLook
            && !playerDodging
            && heldLongEnough;

        if (wantOrbit && !orbiting)
            BeginOrbit();

        if (!wantOrbit && orbiting)
            EndOrbit();

        if (!orbiting)
            return;

        CameraLookInput.Read(out float mouseX, out float mouseY);
        yaw += CameraLookInput.Apply(mouseX, yawSpeed);
        pitch = Mathf.Clamp(pitch - CameraLookInput.Apply(mouseY, pitchSpeed), minPitch, maxPitch);

        ApplyCameraPose();
    }

    private void BeginOrbit()
    {
        orbiting = true;
        ReadRigAngles(out yaw, out pitch);
        baseRig.enabled = false;
    }

    private void EndOrbit()
    {
        WriteRigAngles(yaw, pitch);
        orbiting = false;
        baseRig.enabled = true;
    }

    private void ApplyCameraPose()
    {
        Transform target = GetRigTransform("target");
        Transform pivot = GetRigTransform("pivot");
        Camera cam = GetRigCamera();
        if (target == null || pivot == null || cam == null)
            return;

        Vector3 pivotOffset = GetRigVector3("pivotOffset");
        float distance = GetRigFloat("distance");
        float shoulder = GetRigFloat("shoulder");
        float followSmooth = GetRigFloat("followSmooth");

        Vector3 desiredRigPos = target.position + pivotOffset;
        baseRig.transform.position = Vector3.Lerp(
            baseRig.transform.position,
            desiredRigPos,
            1f - Mathf.Exp(-followSmooth * Time.deltaTime));

        baseRig.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 shoulderOffset = baseRig.transform.right * shoulder;
        Vector3 camLocal = new Vector3(0f, 0f, -distance);
        cam.transform.position = pivot.TransformPoint(camLocal) + shoulderOffset;
        cam.transform.rotation = Quaternion.LookRotation(
            (pivot.position + shoulderOffset) - cam.transform.position,
            Vector3.up);
    }

    private void ReadRigAngles(out float rigYaw, out float rigPitch)
    {
        rigYaw = GetRigFloat("yaw");
        rigPitch = GetRigFloat("pitch");
    }

    private void WriteRigAngles(float rigYaw, float rigPitch)
    {
        SetRigFloat("yaw", rigYaw);
        SetRigFloat("pitch", rigPitch);
    }

    private Transform GetRigTransform(string fieldName)
    {
        FieldInfo field = typeof(ThirdPersonCameraRig).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? field.GetValue(baseRig) as Transform : null;
    }

    private Camera GetRigCamera()
    {
        FieldInfo field = typeof(ThirdPersonCameraRig).GetField(
            "cam",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var camera = field != null ? field.GetValue(baseRig) as Camera : null;
        return camera != null ? camera : baseRig.GetComponentInChildren<Camera>();
    }

    private float GetRigFloat(string fieldName)
    {
        FieldInfo field = typeof(ThirdPersonCameraRig).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (float)field.GetValue(baseRig) : 0f;
    }

    private Vector3 GetRigVector3(string fieldName)
    {
        FieldInfo field = typeof(ThirdPersonCameraRig).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (Vector3)field.GetValue(baseRig) : Vector3.zero;
    }

    private void SetRigFloat(string fieldName, float value)
    {
        FieldInfo field = typeof(ThirdPersonCameraRig).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(baseRig, value);
    }
}
