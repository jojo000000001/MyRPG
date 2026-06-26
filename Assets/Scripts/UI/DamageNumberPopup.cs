using System.Collections;
using TMPro;
using UnityEngine;

public sealed class DamageNumberPopup : MonoBehaviour
{
    internal struct Style
    {
        public float normalFontSize;
        public float critFontSize;
        public Color normalColor;
        public Color critColor;
        public float lifetime;
        public float riseSpeed;
        public float critScaleMultiplier;
    }

    private TextMeshPro text;
    private DamageNumberSpawner owner;
    private Coroutine routine;
    private Camera viewCamera;

    internal void Initialize(DamageNumberSpawner spawner)
    {
        owner = spawner;
        text = GetComponent<TextMeshPro>();
    }

    internal void Play(DamageInfo damage, Vector3 worldPosition, Style style, TMP_FontAsset font)
    {
        if (routine != null)
            StopCoroutine(routine);

        if (text == null)
            text = GetComponent<TextMeshPro>();

        if (font != null)
            text.font = font;

        DamageNumberSpawner.ApplyUnlitMaterial(text);

        bool isCritical = damage.isCritical;
        Color displayColor = isCritical ? style.critColor : style.normalColor;

        text.text = damage.amount.ToString();
        text.fontSize = isCritical ? style.critFontSize : style.normalFontSize;
        SetDisplayColor(displayColor);

        transform.position = worldPosition;
        transform.localScale = Vector3.one * (isCritical ? style.critScaleMultiplier : 1f);
        gameObject.SetActive(true);

        viewCamera = Camera.main;
        if (viewCamera != null)
            FaceCamera();

        routine = StartCoroutine(Animate(style, displayColor));
    }

    private IEnumerator Animate(Style style, Color startColor)
    {
        float elapsed = 0f;
        Vector3 startPosition = transform.position;
        float duration = Mathf.Max(0.1f, style.lifetime);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.position = startPosition + Vector3.up * (style.riseSpeed * elapsed);
            FaceCamera();

            Color color = startColor;
            color.a = Mathf.Lerp(1f, 0f, t);
            SetDisplayColor(color);

            yield return null;
        }

        gameObject.SetActive(false);
        routine = null;
        owner?.Release(this);
    }

    private void SetDisplayColor(Color color)
    {
        if (text == null)
            return;

        text.color = color;
        text.faceColor = color;

        Material material = text.fontMaterial;
        if (material == null)
            return;

        material.SetColor(ShaderUtilities.ID_FaceColor, color);
        if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
            material.SetColor(ShaderUtilities.ID_UnderlayColor, color);
    }

    private void FaceCamera()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;

        if (viewCamera == null)
            return;

        Vector3 toCamera = viewCamera.transform.position - transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f)
            toCamera = viewCamera.transform.forward;

        transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
    }
}
