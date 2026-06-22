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
    [SerializeField] private float yawSpeed = 420f;
    [SerializeField] private float pitchSpeed = 120f;
    [SerializeField] private float minPitch = -10f;
    [SerializeField] private float maxPitch = 35f;

    private bool orbiting;
    private float yaw;
    private float pitch;

    private static bool newInputInit;
    private static object mouseDeltaControl;
    private static MethodInfo readValueMethod;

    private void Awake()
    {
        if (baseRig == null)
            baseRig = GetComponent<ThirdPersonCameraRig>();
    }

    private void LateUpdate()
    {
        if (baseRig == null)
            return;

        bool wantOrbit = Input.GetMouseButton(1) && Cursor.lockState == CursorLockMode.Locked;

        if (wantOrbit && !orbiting)
            BeginOrbit();

        if (!wantOrbit && orbiting)
            EndOrbit();

        if (!orbiting)
            return;

        ReadMouseDelta(out float mouseX, out float mouseY);
        yaw += mouseX * yawSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch - mouseY * pitchSpeed * Time.deltaTime, minPitch, maxPitch);

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

    private static void ReadMouseDelta(out float mouseX, out float mouseY)
    {
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");

        if (!Mathf.Approximately(mouseX, 0f) || !Mathf.Approximately(mouseY, 0f))
            return;

        if (TryReadNewInputDelta(out float dx, out float dy))
        {
            mouseX = dx;
            mouseY = dy;
        }
    }

    private static bool TryReadNewInputDelta(out float dx, out float dy)
    {
        dx = 0f;
        dy = 0f;

        try
        {
            if (!newInputInit)
            {
                newInputInit = true;

                var mouseType = System.Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
                if (mouseType == null) return false;

                var currentProp = mouseType.GetProperty(
                    "current",
                    BindingFlags.Public | BindingFlags.Static);
                if (currentProp == null) return false;

                var mouseCurrent = currentProp.GetValue(null, null);
                if (mouseCurrent == null) return false;

                var mouseDeltaProp = mouseType.GetProperty(
                    "delta",
                    BindingFlags.Public | BindingFlags.Instance);
                if (mouseDeltaProp == null) return false;

                mouseDeltaControl = mouseDeltaProp.GetValue(mouseCurrent, null);
                if (mouseDeltaControl == null) return false;

                readValueMethod = mouseDeltaControl.GetType().GetMethod(
                    "ReadValue",
                    System.Type.EmptyTypes);
            }

            if (mouseDeltaControl == null || readValueMethod == null) return false;

            var delta = (Vector2)readValueMethod.Invoke(mouseDeltaControl, null);
            dx = delta.x;
            dy = delta.y;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
