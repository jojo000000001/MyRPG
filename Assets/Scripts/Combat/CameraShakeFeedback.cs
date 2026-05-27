using UnityEngine;

/// <summary>
/// 给主相机叠加短暂局部位移震动；每帧先移除上一帧偏移，避免和相机跟随逻辑互相积累。
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraShakeFeedback : MonoBehaviour
{
    private float remaining;
    private float duration;
    private float strength;
    private float frequency = 35f;
    private float seed;
    private Vector3 previousOffset;

    public static void ShakeMain(float seconds, float shakeStrength, float shakeFrequency)
    {
        if (seconds <= 0f || shakeStrength <= 0f)
            return;

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = Object.FindObjectOfType<Camera>();

        if (targetCamera == null)
            return;

        CameraShakeFeedback shake = targetCamera.GetComponent<CameraShakeFeedback>();
        if (shake == null)
            shake = targetCamera.gameObject.AddComponent<CameraShakeFeedback>();

        shake.AddShake(seconds, shakeStrength, shakeFrequency);
    }

    public void AddShake(float seconds, float shakeStrength, float shakeFrequency)
    {
        duration = Mathf.Max(duration, seconds);
        remaining = Mathf.Max(remaining, seconds);
        strength = Mathf.Max(strength, shakeStrength);
        frequency = Mathf.Max(1f, shakeFrequency);
        seed = Random.value * 100f;
        enabled = true;
    }

    private void LateUpdate()
    {
        RemovePreviousOffset();

        if (remaining <= 0f)
        {
            ResetShake();
            return;
        }

        remaining -= Time.unscaledDeltaTime;

        float life01 = duration <= 0f ? 0f : Mathf.Clamp01(remaining / duration);
        float damping = life01 * life01;
        float sampleTime = Time.unscaledTime * frequency;

        Vector3 noise = new Vector3(
            Mathf.PerlinNoise(seed, sampleTime) - 0.5f,
            Mathf.PerlinNoise(seed + 19.17f, sampleTime) - 0.5f,
            0f) * 2f;

        previousOffset = noise * strength * damping;
        transform.localPosition += previousOffset;
    }

    private void OnDisable()
    {
        RemovePreviousOffset();
    }

    private void ResetShake()
    {
        remaining = 0f;
        duration = 0f;
        strength = 0f;
        enabled = false;
    }

    private void RemovePreviousOffset()
    {
        if (previousOffset.sqrMagnitude <= 0f)
            return;

        transform.localPosition -= previousOffset;
        previousOffset = Vector3.zero;
    }
}
