using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitFeedback : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Flash")]
    [SerializeField] private bool flashEnabled = true;
    [SerializeField] private Color flashColor = new Color(1f, 0.22f, 0.16f, 1f);
    [SerializeField] private float flashSeconds = 0.14f;
    [SerializeField] private int flashPulses = 2;
    [SerializeField] private Transform renderRoot;

    [Header("Punch")]
    [SerializeField] private bool scalePunchEnabled = true;
    [SerializeField] private Transform scaleTarget;
    [SerializeField] private float scalePunchAmount = 0.06f;
    [SerializeField] private float scalePunchSeconds = 0.12f;

    [Header("Recoil")]
    [SerializeField] private bool recoilEnabled = true;
    [SerializeField] private Transform recoilTarget;
    [SerializeField] private float recoilDistance = 0.08f;
    [SerializeField] private float recoilSeconds = 0.14f;

    private Renderer[] renderers;
    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock scratchBlock;
    private Coroutine feedbackRoutine;
    private Coroutine boolRoutine;
    private Color? flashColorOverride;
    private bool bodyMotionEnabled = true;
    private Vector3 baseScale;
    private Vector3 baseLocalPosition;
    private bool hasBaseLocalPosition;
    private Vector3 recoilLocalDirection;
    private bool hasBaseScale;
    private bool flashApplied;

    private void Awake()
    {
        CacheTargets();
    }

private void OnDisable()
    {
        RestoreFlash();
        RestoreScale();
        RestoreRecoil();
    }

private void OnValidate()
    {
        flashSeconds = Mathf.Max(0f, flashSeconds);
        flashPulses = Mathf.Max(1, flashPulses);
        scalePunchAmount = Mathf.Max(0f, scalePunchAmount);
        scalePunchSeconds = Mathf.Max(0f, scalePunchSeconds);
        recoilDistance = Mathf.Max(0f, recoilDistance);
        recoilSeconds = Mathf.Max(0f, recoilSeconds);
    }

public void Play(Animator targetAnimator, string triggerParam, string boolParam, float boolSeconds)
    {
        flashColorOverride = null;
        recoilLocalDirection = Vector3.zero;
        PlayVisualsInternal(true);
        PlayAnimator(targetAnimator, triggerParam, boolParam, boolSeconds);
    }

    public void Play(Animator targetAnimator, string triggerParam, string boolParam, float boolSeconds, DamageInfo damage)
    {
        flashColorOverride = null;
        SetRecoilDirection(damage);
        PlayVisualsInternal(true);
        PlayAnimator(targetAnimator, triggerParam, boolParam, boolSeconds);
    }

    public bool IsPlaying => feedbackRoutine != null;

    public void Cancel()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        if (boolRoutine != null)
        {
            StopCoroutine(boolRoutine);
            boolRoutine = null;
        }

        RestoreFlash();
        RestoreScale();
        RestoreRecoil();
    }

    public void PlayVisuals()
    {
        flashColorOverride = null;
        recoilLocalDirection = Vector3.zero;
        PlayVisualsInternal(true);
    }

    public void PlayVisuals(Color color)
    {
        flashColorOverride = color;
        recoilLocalDirection = Vector3.zero;
        PlayVisualsInternal(true);
    }

    public void PlayFlashOnly(Color color)
    {
        flashColorOverride = color;
        recoilLocalDirection = Vector3.zero;
        PlayVisualsInternal(false);
    }

    public void PlayVisuals(DamageInfo damage)
    {
        flashColorOverride = null;
        SetRecoilDirection(damage);
        PlayVisualsInternal(true);
    }

    private void PlayVisualsInternal(bool enableBodyMotion)
    {
        bodyMotionEnabled = enableBodyMotion;
        CacheTargets();

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        RestoreFlash();
        RestoreScale();
        RestoreRecoil();
        feedbackRoutine = StartCoroutine(FeedbackRoutine());
    }

    private void PlayAnimator(Animator targetAnimator, string triggerParam, string boolParam, float boolSeconds)
    {
        if (targetAnimator == null)
            return;

        if (!string.IsNullOrEmpty(triggerParam) && HasAnimatorParameter(targetAnimator, triggerParam, AnimatorControllerParameterType.Trigger))
            targetAnimator.SetTrigger(triggerParam);

        if (boolSeconds > 0f
            && !string.IsNullOrEmpty(boolParam)
            && HasAnimatorParameter(targetAnimator, boolParam, AnimatorControllerParameterType.Bool))
        {
            targetAnimator.SetBool(boolParam, true);

            if (boolRoutine != null)
                StopCoroutine(boolRoutine);

            boolRoutine = StartCoroutine(ResetBoolAfter(targetAnimator, boolParam, boolSeconds));
        }
    }

private IEnumerator FeedbackRoutine()
    {
        float duration = Mathf.Max(
            flashEnabled ? flashSeconds : 0f,
            bodyMotionEnabled && scalePunchEnabled ? scalePunchSeconds : 0f,
            bodyMotionEnabled && recoilEnabled ? recoilSeconds : 0f);

        if (duration <= 0f)
        {
            feedbackRoutine = null;
            yield break;
        }

        if (scaleTarget != null)
        {
            baseScale = scaleTarget.localScale;
            hasBaseScale = true;
        }

        if (recoilTarget != null)
        {
            baseLocalPosition = recoilTarget.localPosition;
            hasBaseLocalPosition = true;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            UpdateFlash(elapsed);
            UpdateScale(elapsed);
            UpdateRecoil(elapsed);

            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreFlash();
        RestoreScale();
        RestoreRecoil();
        feedbackRoutine = null;
    }

    private IEnumerator ResetBoolAfter(Animator targetAnimator, string boolParam, float seconds)
    {
        if (seconds > 0f)
            yield return new WaitForSeconds(seconds);
        else
            yield return null;

        if (targetAnimator != null && HasAnimatorParameter(targetAnimator, boolParam, AnimatorControllerParameterType.Bool))
            targetAnimator.SetBool(boolParam, false);

        boolRoutine = null;
    }

    private void UpdateFlash(float elapsed)
    {
        if (!flashEnabled || flashSeconds <= 0f || renderers == null || renderers.Length == 0 || elapsed > flashSeconds)
        {
            RestoreFlash();
            return;
        }

        float step = flashSeconds / (Mathf.Max(1, flashPulses) * 2f);
        bool flashOn = step <= 0f || Mathf.FloorToInt(elapsed / step) % 2 == 0;

        if (flashOn)
            ApplyFlash();
        else
            RestoreFlash();
    }

    private void UpdateScale(float elapsed)
    {
        if (!bodyMotionEnabled || !scalePunchEnabled || scaleTarget == null || scalePunchSeconds <= 0f || !hasBaseScale)
            return;

        float t = Mathf.Clamp01(elapsed / scalePunchSeconds);
        float punch = Mathf.Sin(t * Mathf.PI) * scalePunchAmount;
        scaleTarget.localScale = baseScale * (1f + punch);
    }

private void UpdateRecoil(float elapsed)
    {
        if (!bodyMotionEnabled || !recoilEnabled || recoilTarget == null || recoilSeconds <= 0f || !hasBaseLocalPosition)
            return;

        Vector3 direction = recoilLocalDirection.sqrMagnitude > 0.0001f
            ? recoilLocalDirection
            : Vector3.back;

        float t = Mathf.Clamp01(elapsed / recoilSeconds);
        float weight = 1f - Mathf.SmoothStep(0f, 1f, t);
        recoilTarget.localPosition = baseLocalPosition + direction * recoilDistance * weight;
    }


    private void ApplyFlash()
    {
        if (renderers == null || renderers.Length == 0)
            return;

        if (!flashApplied)
            CaptureOriginalBlocks();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
                continue;

            scratchBlock.Clear();
            targetRenderer.GetPropertyBlock(scratchBlock);
            Color color = flashColorOverride ?? flashColor;
            scratchBlock.SetColor(BaseColorId, color);
            scratchBlock.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(scratchBlock);
        }
    }

    private void CaptureOriginalBlocks()
    {
        EnsureBlockCache();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].GetPropertyBlock(originalBlocks[i]);
        }

        flashApplied = true;
    }

    private void RestoreFlash()
    {
        if (!flashApplied || renderers == null || originalBlocks == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && i < originalBlocks.Length)
                renderers[i].SetPropertyBlock(originalBlocks[i]);
        }

        flashApplied = false;
    }

    private void RestoreScale()
    {
        if (scaleTarget != null && hasBaseScale)
            scaleTarget.localScale = baseScale;

        hasBaseScale = false;
    }

private void RestoreRecoil()
    {
        if (recoilTarget != null && hasBaseLocalPosition)
            recoilTarget.localPosition = baseLocalPosition;

        hasBaseLocalPosition = false;
    }

    private void SetRecoilDirection(DamageInfo damage)
    {
        Vector3 direction = damage.direction;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f && damage.source != null)
        {
            direction = transform.position - damage.source.transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;

        direction.Normalize();

        Transform target = recoilTarget != null ? recoilTarget : (renderRoot != null ? renderRoot : transform);
        Transform basis = target != null ? target.parent : null;
        recoilLocalDirection = basis != null ? basis.InverseTransformDirection(direction) : direction;
        recoilLocalDirection.y = 0f;

        if (recoilLocalDirection.sqrMagnitude > 0.0001f)
            recoilLocalDirection.Normalize();
        else
            recoilLocalDirection = Vector3.back;
    }


private void CacheTargets()
    {
        Transform root = renderRoot != null ? renderRoot : transform;
        renderers = root.GetComponentsInChildren<Renderer>(true);

        if (scaleTarget == null)
            scaleTarget = transform;

        if (recoilTarget == null)
            recoilTarget = renderRoot != null ? renderRoot : scaleTarget;

        EnsureBlockCache();
    }

    private void EnsureBlockCache()
    {
        if (renderers == null)
            renderers = new Renderer[0];

        if (scratchBlock == null)
            scratchBlock = new MaterialPropertyBlock();

        if (originalBlocks != null && originalBlocks.Length == renderers.Length)
            return;

        originalBlocks = new MaterialPropertyBlock[renderers.Length];
        for (int i = 0; i < originalBlocks.Length; i++)
            originalBlocks[i] = new MaterialPropertyBlock();
    }

    private static bool HasAnimatorParameter(Animator targetAnimator, string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (targetAnimator == null || string.IsNullOrEmpty(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }
}