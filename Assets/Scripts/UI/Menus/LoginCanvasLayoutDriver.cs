using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 让登录 Canvas 在编辑模式下也保持可见缩放（CanvasScaler 运行前根节点 scale 可能为 0）。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
public sealed class LoginCanvasLayoutDriver : MonoBehaviour
{
    private CanvasScaler scaler;
    private RectTransform rect;

    private void OnEnable()
    {
        scaler = GetComponent<CanvasScaler>();
        rect = (RectTransform)transform;
        Apply();
    }

    private void OnRectTransformDimensionsChange()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying)
            Apply();
    }
#endif

    private void Apply()
    {
        if (scaler == null || rect == null)
            return;

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            rect.localScale = Vector3.one;
            return;
        }

        float referenceWidth = Mathf.Max(1f, scaler.referenceResolution.x);
        float referenceHeight = Mathf.Max(1f, scaler.referenceResolution.y);
        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);

        float widthScale = screenWidth / referenceWidth;
        float heightScale = screenHeight / referenceHeight;
        float scaleFactor = Mathf.Lerp(widthScale, heightScale, scaler.matchWidthOrHeight);
        rect.localScale = Vector3.one * Mathf.Max(0.0001f, scaleFactor);
    }
}
