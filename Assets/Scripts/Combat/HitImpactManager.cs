using System.Collections;
using UnityEngine;

/// <summary>
/// 命中瞬间的全局反馈：短暂停顿时间并触发相机震动，增强近战打击感。
/// </summary>
[DisallowMultipleComponent]
public sealed class HitImpactManager : MonoBehaviour
{
    private static HitImpactManager instance;

    private Coroutine hitStopRoutine;
    private float originalTimeScale = 1f;
    private float originalFixedDeltaTime = 0.02f;
    private bool hitStopActive;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDisable()
    {
        RestoreTimeScale();

        if (instance == this)
            instance = null;
    }

    public static void PlayImpact(float hitStopSeconds, float hitStopTimeScale, float shakeSeconds, float shakeStrength, float shakeFrequency)
    {
        if (!Application.isPlaying)
            return;

        bool needsHitStop = hitStopSeconds > 0f && hitStopTimeScale < 1f;
        bool needsShake = shakeSeconds > 0f && shakeStrength > 0f;
        if (!needsHitStop && !needsShake)
            return;

        HitImpactManager manager = GetOrCreate();
        manager.Play(hitStopSeconds, hitStopTimeScale, shakeSeconds, shakeStrength, shakeFrequency);
    }

    private static HitImpactManager GetOrCreate()
    {
        if (instance != null)
            return instance;

        var gameObject = new GameObject("HitImpactManager");
        instance = gameObject.AddComponent<HitImpactManager>();
        return instance;
    }

    private void Play(float hitStopSeconds, float hitStopTimeScale, float shakeSeconds, float shakeStrength, float shakeFrequency)
    {
        if (hitStopSeconds > 0f && hitStopTimeScale < 1f)
        {
            if (hitStopRoutine != null)
                StopCoroutine(hitStopRoutine);

            hitStopRoutine = StartCoroutine(HitStopRoutine(hitStopSeconds, hitStopTimeScale));
        }

        CameraShakeFeedback.ShakeMain(shakeSeconds, shakeStrength, shakeFrequency);
    }

    private IEnumerator HitStopRoutine(float seconds, float timeScale)
    {
        if (!hitStopActive)
        {
            originalTimeScale = Time.timeScale;
            originalFixedDeltaTime = Time.fixedDeltaTime;
            hitStopActive = true;
        }

        Time.timeScale = Mathf.Clamp(timeScale, 0.01f, 1f);
        Time.fixedDeltaTime = Mathf.Max(0.0001f, originalFixedDeltaTime * Time.timeScale);

        float endAt = Time.unscaledTime + Mathf.Max(0f, seconds);
        while (Time.unscaledTime < endAt)
            yield return null;

        RestoreTimeScale();
        hitStopRoutine = null;
    }

    private void RestoreTimeScale()
    {
        if (!hitStopActive)
            return;

        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        hitStopActive = false;
    }
}
