using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VerticalLetterRotation : MonoBehaviour
{
    [SerializeField] private float zRotationDegrees = 90f;
    [SerializeField] private bool waitOneFrameForLayout = true;
    [SerializeField] [Range(1, 30)] private int layoutSettleFrames = 8;

    [Tooltip("若字母在 VerticalLayout 下一层：勾选。关闭则只旋转一层子 RectTransform。")]
    [SerializeField] private bool rotateEachTextGraphic = true;

    // UI 布局可能分多帧稳定，保留若干 LateUpdate 重复应用旋转。
    private int _remainingLateFrames;

    private void OnEnable()
    {
        if (waitOneFrameForLayout && Application.isPlaying)
            StartCoroutine(DelayedStart());
        else
            BeginApplyPasses();
    }

    private IEnumerator DelayedStart()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        var self = transform as RectTransform;
        if (self != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(self);
        BeginApplyPasses();
    }

    private void BeginApplyPasses()
    {
        _remainingLateFrames = Mathf.Max(1, layoutSettleFrames);
    }

    private void LateUpdate()
    {
        if (_remainingLateFrames <= 0)
            return;

        ApplyNow();
        _remainingLateFrames--;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            ApplyNow();
    }
#endif

    /// <summary>
    /// 外部布局变化后可手动调用，重新应用竖排字母旋转。
    /// </summary>
    public void RefreshRotation()
    {
        BeginApplyPasses();
    }

    // 根据配置旋转所有文字组件，或只旋转直接子物体。
    private void ApplyNow()
    {
        if (rotateEachTextGraphic)
        {
            foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
                ApplyTo(tmp.rectTransform);

            foreach (var le in GetComponentsInChildren<Text>(true))
                ApplyTo(le.rectTransform);
        }
        else
        {
            int n = transform.childCount;
            for (int i = 0; i < n; i++)
            {
                var rt = transform.GetChild(i) as RectTransform;
                ApplyTo(rt);
            }
        }
    }

    private void ApplyTo(RectTransform rt)
    {
        if (rt == null)
            return;

        var e = rt.localEulerAngles;
        e.z = zRotationDegrees;
        rt.localEulerAngles = e;
    }
}
