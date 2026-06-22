using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 怪物头顶血条：运行时自动创建世界空间 Canvas，并根据 Monster 血量更新填充宽度。
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterHealthBar : MonoBehaviour
{
    private const string BarRootName = "MonsterHealthBar";
    private const string FillAreaName = "FillArea";
    private const string FillName = "Fill";

    [Header("Layout")]
    [SerializeField] protected Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] protected Vector2 barSize = new Vector2(1.25f, 0.16f);
    [SerializeField] protected float pixelsPerUnit = 100f;
    [SerializeField] protected float borderPixels = 3f;
    [SerializeField] protected int sortingOrder = 50;

    [Header("Display")]
    [SerializeField] protected bool hideWhenFull = false;
    [SerializeField] protected bool hideOnDeath = true;
    [SerializeField] protected float smoothSpeed = 12f;

    [Header("Color")]
    [SerializeField] protected Color frameColor = new Color(0.03f, 0.02f, 0.02f, 0.85f);
    [SerializeField] protected Color backgroundColor = new Color(0.18f, 0.04f, 0.04f, 0.85f);
    [SerializeField] protected Color highHealthColor = new Color(0.2f, 0.85f, 0.25f, 0.95f);
    [SerializeField] protected Color lowHealthColor = new Color(0.95f, 0.12f, 0.08f, 0.95f);

    private Monster monster;
    private RectTransform barRoot;
    private RectTransform fillArea;
    private RectTransform fillRect;
    private Image fillImage;
    private Canvas canvas;
    private Camera viewCamera;
    private float displayedHealth01 = 1f;

    protected virtual void Awake()
    {
        monster = GetComponent<Monster>();
        EnsureBar();
        UpdateImmediate();
    }

    private void LateUpdate()
    {
        if (monster == null) return;

        EnsureBar();

        float target01 = GetHealth01();
        float t = smoothSpeed <= 0f ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        displayedHealth01 = Mathf.Lerp(displayedHealth01, target01, t);

        UpdateFill(displayedHealth01);
        UpdateVisibility(target01);
        FaceCamera();
    }
    //复用血条
    private void EnsureBar()
    {
        if (barRoot != null && fillRect != null && fillImage != null && canvas != null)
            return;

        Transform existing = transform.Find(BarRootName);
        if (existing != null)
        {
            barRoot = existing as RectTransform;
            canvas = existing.GetComponent<Canvas>();
            fillArea = existing.Find(FillAreaName) as RectTransform;
            fillRect = fillArea != null ? fillArea.Find(FillName) as RectTransform : null;
            fillImage = fillRect != null ? fillRect.GetComponent<Image>() : null;
        }

        if (barRoot == null || fillArea == null || fillRect == null || fillImage == null || canvas == null)
            CreateBar();

        ApplyLayout();
    }

    private void CreateBar()
    {
        GameObject root = new GameObject(BarRootName, typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);

        barRoot = root.GetComponent<RectTransform>();
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;

        GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(root.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        StretchToParent(frameRect);
        frame.GetComponent<Image>().color = frameColor;

        GameObject area = new GameObject(FillAreaName, typeof(RectTransform), typeof(Image));
        area.transform.SetParent(frame.transform, false);
        fillArea = area.GetComponent<RectTransform>();
        area.GetComponent<Image>().color = backgroundColor;

        GameObject fill = new GameObject(FillName, typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(area.transform, false);
        fillRect = fill.GetComponent<RectTransform>();
        fillImage = fill.GetComponent<Image>();
        fillImage.color = highHealthColor;

        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
    }

    private void ApplyLayout()
    {
        if (barRoot == null) return;

        Vector2 pixelSize = new Vector2(
            Mathf.Max(1f, barSize.x * pixelsPerUnit),
            Mathf.Max(1f, barSize.y * pixelsPerUnit));

        barRoot.sizeDelta = pixelSize;
        barRoot.localScale = Vector3.one / Mathf.Max(1f, pixelsPerUnit);
        barRoot.position = transform.position + worldOffset;

        if (canvas != null)
            canvas.sortingOrder = sortingOrder;

        if (fillArea != null)
        {
            StretchToParent(fillArea);
            fillArea.offsetMin = new Vector2(borderPixels, borderPixels);
            fillArea.offsetMax = new Vector2(-borderPixels, -borderPixels);
        }
    }

    private void UpdateImmediate()
    {
        displayedHealth01 = GetHealth01();
        UpdateFill(displayedHealth01);
        UpdateVisibility(displayedHealth01);
        FaceCamera();
    }

    private float GetHealth01()
    {
        if (monster == null || monster.MaxHp <= 0)
            return 0f;

        return Mathf.Clamp01((float)monster.CurrentHp / monster.MaxHp);
    }

    private void UpdateFill(float health01)
    {
        if (fillArea == null || fillRect == null || fillImage == null) return;

        float innerWidth = Mathf.Max(0f, fillArea.rect.width);
        fillRect.sizeDelta = new Vector2(innerWidth * Mathf.Clamp01(health01), 0f);
        fillImage.color = Color.Lerp(lowHealthColor, highHealthColor, Mathf.Clamp01(health01));
    }

    private void UpdateVisibility(float health01)
    {
        if (canvas == null) return;

        bool visible = true;
        if (hideOnDeath && monster.IsDead)
            visible = false;
        if (hideWhenFull && health01 >= 0.999f)
            visible = false;

        canvas.enabled = visible;
    }

    private void FaceCamera()
    {
        if (barRoot == null || canvas == null || !canvas.enabled) return;

        barRoot.position = transform.position + worldOffset;

        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera == null)
            return;

        barRoot.rotation = viewCamera.transform.rotation;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
