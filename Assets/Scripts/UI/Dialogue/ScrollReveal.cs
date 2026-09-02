using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 横向卷轴：左缘固定 + 中间纸条（可选 Mask 内 Paper）逐渐变宽 + 右侧卷柄跟随。
public sealed class ScrollReveal : MonoBehaviour
{
    [Header("布局")]
    [FormerlySerializedAs("viewport")]
    [SerializeField] private RectTransform revealMask;

    [Tooltip("卷柄（右侧把手），随展开宽度移动")]
    [FormerlySerializedAs("rightCap")]
    [SerializeField] private RectTransform rightHandle;

    [Tooltip("左缘固定装饰")]
    [FormerlySerializedAs("leftCap")]
    [SerializeField] private RectTransform leftAnchor;

    [Tooltip("纸条本体：应在 RevealMask 子级，宽度会被设为 targetWidth，由遮罩横向揭开")]
    [SerializeField] private RectTransform paperContent;

    [Header("Art（Assets/Art）")]
    [FormerlySerializedAs("leftCapSprite")]
    [SerializeField] private Sprite leftAnchorSprite;

    [SerializeField] private Sprite scrollBodySprite;

    [FormerlySerializedAs("rightCapSprite")]
    [SerializeField] private Sprite rightHandleSprite;

    [Header("卷柄贴合")]
    [FormerlySerializedAs("rightCapOverlapPixels")]
    [SerializeField] private float handleOverlapPixels = 28f;

    [FormerlySerializedAs("autoRightCapOverlap")]
    [SerializeField] private bool autoHandleOverlap = true;

    [Header("动画")]
    [SerializeField] private float startWidth = 0f;
    [SerializeField] private float targetWidth = 1200f;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private AnimationCurve ease = null;
    [SerializeField] private bool playOnEnable = true;

    // _t 记录当前动画时间，_playing 表示卷轴是否正在展开。
    private float _t;
    private bool _playing;

    private void Reset()
    {
        ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Awake()
    {
        ApplySpritesToImages();
        SyncPaperWidth();
    }

    private void OnValidate()
    {
        ApplySpritesToImages();
        SyncPaperWidth();
    }

    private void OnEnable()
    {
        Apply(startWidth);
        if (!playOnEnable) return;

        _t = 0f;
        _playing = true;
    }

    // 使用 unscaledDeltaTime，让暂停或时间缩放不影响 UI 展开动画。
    private void Update()
    {
        if (!_playing || revealMask == null) return;

        _t += Time.unscaledDeltaTime;
        float p = duration <= 0.0001f ? 1f : Mathf.Clamp01(_t / duration);
        float e = (ease != null && ease.length > 0) ? ease.Evaluate(p) : p;
        float w = Mathf.Lerp(startWidth, targetWidth, e);
        Apply(w);

        if (p >= 1f) _playing = false;
    }

    /// <summary>
    /// 从起始宽度重新播放卷轴展开动画。
    /// </summary>
    public void PlayReveal()
    {
        _t = 0f;
        _playing = true;
        Apply(startWidth);
    }

    public void CollapseImmediate()
    {
        _playing = false;
        Apply(startWidth);
    }

    public void ExpandImmediate()
    {
        _playing = false;
        Apply(targetWidth);
    }

    // 纸条自身保持完整目标宽度，由遮罩宽度负责“露出多少”。
    private void SyncPaperWidth()
    {
        if (paperContent == null) return;

        var sd = paperContent.sizeDelta;
        sd.x = Mathf.Max(1f, targetWidth);
        paperContent.sizeDelta = sd;
    }

    private void ApplySpritesToImages()
    {
        TrySetSprite(leftAnchor, leftAnchorSprite);
        if (paperContent != null)
            TrySetSprite(paperContent, scrollBodySprite);
        else
            TrySetSprite(revealMask, scrollBodySprite);
        TrySetSprite(rightHandle, rightHandleSprite);
    }

    private static void TrySetSprite(RectTransform rt, Sprite sprite)
    {
        if (sprite == null || rt == null) return;

        var image = rt.GetComponent<Image>();
        if (image != null)
            image.sprite = sprite;
    }

    // 根据当前展开宽度同步遮罩、纸条宽度和右侧卷柄位置。
    private void Apply(float width)
    {
        if (revealMask != null)
        {
            var sd = revealMask.sizeDelta;
            sd.x = width;
            revealMask.sizeDelta = sd;
        }

        if (paperContent != null)
        {
            var sd = paperContent.sizeDelta;
            sd.x = Mathf.Max(1f, targetWidth);
            paperContent.sizeDelta = sd;
        }

        if (rightHandle != null)
        {
            float overlap = handleOverlapPixels;
            if (autoHandleOverlap)
            {
                float capW = Mathf.Max(1f, rightHandle.rect.width);
                overlap = Mathf.Clamp(capW * 0.35f, 12f, 90f);
            }

            var pos = rightHandle.anchoredPosition;
            pos.x = width - overlap;
            rightHandle.anchoredPosition = pos;
        }
    }
}
