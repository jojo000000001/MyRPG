using System.Reflection;
using UnityEngine;

/// <summary>
/// 鼠标视角增量。旧输入的 Mouse X/Y 已是本帧位移，不能再乘 Time.deltaTime，
/// 否则低帧率会转得飞快、高帧率又发黏，打包后和编辑器手感差一截。
/// 速度仍按 60 帧手感换算，和原先 yawSpeed * deltaTime 在 60FPS 时一致。
/// </summary>
public static class CameraLookInput
{
    private const float ReferenceDeltaTime = 1f / 60f;
    private const float PixelToOldMouseAxis = 0.1f;

    private static bool newInputInit;
    private static object mouseDeltaControl;
    private static MethodInfo readValueMethod;

    public static void Read(out float mouseX, out float mouseY)
    {
        mouseX = Input.GetAxisRaw("Mouse X");
        mouseY = Input.GetAxisRaw("Mouse Y");

        if (!Mathf.Approximately(mouseX, 0f) || !Mathf.Approximately(mouseY, 0f))
            return;

        if (!TryReadNewInputPixels(out float dx, out float dy))
            return;

        mouseX = dx * PixelToOldMouseAxis;
        mouseY = dy * PixelToOldMouseAxis;
    }

    public static float Apply(float mouseDelta, float speed)
    {
        return mouseDelta * speed * ReferenceDeltaTime;
    }

    private static bool TryReadNewInputPixels(out float dx, out float dy)
    {
        dx = 0f;
        dy = 0f;

        try
        {
            if (!newInputInit)
            {
                newInputInit = true;
                var mouseType = System.Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
                if (mouseType == null)
                    return false;

                var currentProp = mouseType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                var mouseCurrent = currentProp != null ? currentProp.GetValue(null, null) : null;
                if (mouseCurrent == null)
                    return false;

                var deltaProp = mouseType.GetProperty("delta", BindingFlags.Public | BindingFlags.Instance);
                mouseDeltaControl = deltaProp != null ? deltaProp.GetValue(mouseCurrent, null) : null;
                if (mouseDeltaControl == null)
                    return false;

                readValueMethod = mouseDeltaControl.GetType().GetMethod("ReadValue", System.Type.EmptyTypes);
            }

            if (mouseDeltaControl == null || readValueMethod == null)
                return false;

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
