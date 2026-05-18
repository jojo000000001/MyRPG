using UnityEngine;

/// <summary>
/// 第一/第三人称视角旋转控制器，支持锁定鼠标和新旧输入系统的鼠标位移读取。
/// </summary>
public sealed class MouseLookCamera : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Transform yawBody;

    [Header("Tuning")]
    [SerializeField] private float sensitivity = 2.0f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnEnable = true;
    [SerializeField] private KeyCode toggleCursorKey = KeyCode.Escape;

    // yaw 负责水平旋转，pitch 负责上下抬头/低头。
    private float _yaw;
    private float _pitch;

    // 新输入系统的反射缓存：不直接依赖 InputSystem 包，项目未安装时也能编译。
    private static bool _newInputInit;
    private static object _mouseCurrent;
    private static System.Reflection.PropertyInfo _mouseDeltaProp;
    private static System.Reflection.MethodInfo _readValueMethod;

    private void OnEnable()
    {
        if (lockCursorOnEnable)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        float yaw0 = yawBody != null ? yawBody.localEulerAngles.y : transform.localEulerAngles.y;
        float pitch0 = transform.localEulerAngles.x;

        _yaw = NormalizeAngle(yaw0);
        _pitch = Mathf.Clamp(NormalizeAngle(pitch0), minPitch, maxPitch);

        ApplyRotation();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleCursorKey))
        {
            bool locked = Cursor.lockState != CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");

        // 旧输入轴没有值时，尝试读取新输入系统的 Mouse.delta。
        if (Mathf.Approximately(mx, 0f) && Mathf.Approximately(my, 0f))
        {
            float dx, dy;
            if (TryReadNewInputDelta(out dx, out dy))
            {
                mx = dx;
                my = dy;
            }
        }

        float yMul = invertY ? 1f : -1f;

        _yaw += mx * sensitivity;
        _pitch = Mathf.Clamp(_pitch + my * sensitivity * yMul, minPitch, maxPitch);

        ApplyRotation();
    }

    // yawBody 存在时拆成“身体水平转 + 相机俯仰”，否则直接旋转当前物体。
    private void ApplyRotation()
    {
        if (yawBody != null)
        {
            yawBody.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
        else
        {
            transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }

    private static bool TryReadNewInputDelta(out float dx, out float dy)
    {
        dx = 0f;
        dy = 0f;

        try
        {
            if (!_newInputInit)
            {
                _newInputInit = true;

                var mouseType = System.Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
                if (mouseType == null) return false;

                var currentProp = mouseType.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (currentProp == null) return false;

                _mouseCurrent = currentProp.GetValue(null, null);
                if (_mouseCurrent == null) return false;

                _mouseDeltaProp = mouseType.GetProperty("delta", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (_mouseDeltaProp == null) return false;

                var deltaControl = _mouseDeltaProp.GetValue(_mouseCurrent, null);
                if (deltaControl == null) return false;

                // delta 是 Vector2Control，缓存 ReadValue() 以降低后续反射开销。
                _readValueMethod = deltaControl.GetType().GetMethod("ReadValue", System.Type.EmptyTypes);
                if (_readValueMethod == null) return false;

                _mouseDeltaProp = null;
                _mouseCurrent = deltaControl;
            }

            if (_mouseCurrent == null || _readValueMethod == null) return false;
            var v = (Vector2)_readValueMethod.Invoke(_mouseCurrent, null);
            dx = v.x;
            dy = v.y;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static float NormalizeAngle(float a)
    {
        if (a > 180f) a -= 360f;
        return a;
    }
}
