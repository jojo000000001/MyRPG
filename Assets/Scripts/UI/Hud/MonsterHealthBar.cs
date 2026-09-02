using UnityEngine;

/// <summary>
/// 怪物头顶血条：与玩家 HUD 共用 Kenney 三段血条。
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterHealthBar : MonoBehaviour
{
    private const string BarRootName = "MonsterHealthBar";
    private const bool ShowBackTrack = true;

    [Header("Layout")]
    [SerializeField] protected Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] protected Vector2 barSize = new Vector2(1.25f, 0.16f);
    [SerializeField] protected float pixelsPerUnit = 100f;
    [SerializeField] protected int sortingOrder = 50;

    [Header("Display")]
    [SerializeField] protected bool hideWhenFull;
    [SerializeField] protected bool hideOnDeath = true;
    [SerializeField] protected float smoothSpeed = 12f;

    private Monster monster;
    private KenneyBarView barView;
    private Canvas canvas;
    private Camera viewCamera;
    private float displayedHealth01 = 1f;
    private Vector2 pixelSize;
    private float capWidth;
    private float fillInset;

    protected virtual void Awake()
    {
        monster = GetComponent<Monster>();
        HealthBarSprites.BindCatalog(Resources.Load<HealthBarSpriteCatalog>("HealthBarSpriteCatalog"));
        EnsureBar();
        UpdateImmediate();
    }

    private void LateUpdate()
    {
        if (monster == null)
            return;

        EnsureBar();

        float target01 = GetHealth01();
        float t = smoothSpeed <= 0f ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        displayedHealth01 = Mathf.Lerp(displayedHealth01, target01, t);

        barView?.SetFill01(displayedHealth01, pixelSize.x, fillInset);
        UpdateVisibility(target01);
        FaceCamera();
    }

    private void EnsureBar()
    {
        if (barView != null && barView.IsReady && canvas != null)
            return;

        Transform existing = transform.Find(BarRootName);
        if (existing != null)
        {
            RectTransform existingRoot = existing as RectTransform;
            if (KenneyBarView.HasExpectedParts(existingRoot, ShowBackTrack))
            {
                barView = KenneyBarView.Bind(existingRoot, ShowBackTrack);
                canvas = existing.GetComponent<Canvas>();
            }
            else
            {
                existing.name = BarRootName + "_Legacy";
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }
        }

        if (barView == null || !barView.IsReady)
            CreateBar();

        if (canvas == null && barView != null)
            canvas = barView.Root.GetComponent<Canvas>();

        ApplyWorldLayout();
        barView?.ApplySprites(displayedHealth01 >= 0.5f);
    }

    private void CreateBar()
    {
        barView = KenneyBarView.Create(transform, BarRootName, ShowBackTrack, useTrackPlate: false);

        canvas = barView.Root.GetComponent<Canvas>();
        if (canvas == null)
            canvas = barView.Root.gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    private void ApplyWorldLayout()
    {
        if (barView == null || barView.Root == null)
            return;

        float ppu = Mathf.Max(1f, pixelsPerUnit);
        pixelSize = new Vector2(
            Mathf.Max(1f, barSize.x * ppu),
            Mathf.Max(1f, barSize.y * ppu));

        float scale = pixelSize.x / Mathf.Max(1f, HudBarVisualStyle.BarSize.x);
        capWidth = Mathf.Clamp(HudBarVisualStyle.CapWidth * scale, 8f, pixelSize.x * 0.4f);
        fillInset = Mathf.Clamp(HudBarVisualStyle.FillInset * scale, 1f, pixelSize.y * 0.4f);

        RectTransform root = barView.Root;
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.localScale = Vector3.one / ppu;
        root.position = transform.position + worldOffset;

        if (canvas != null)
            canvas.sortingOrder = sortingOrder;

        barView.LayoutContents(pixelSize, capWidth, fillInset);
    }

    private void UpdateImmediate()
    {
        displayedHealth01 = GetHealth01();
        barView?.SetFill01(displayedHealth01, pixelSize.x, fillInset);
        UpdateVisibility(displayedHealth01);
        FaceCamera();
    }

    private float GetHealth01()
    {
        if (monster == null || monster.MaxHp <= 0)
            return 0f;

        return Mathf.Clamp01((float)monster.CurrentHp / monster.MaxHp);
    }

    private void UpdateVisibility(float health01)
    {
        if (canvas == null)
            return;

        bool visible = true;
        if (hideOnDeath && monster != null && monster.IsDead)
            visible = false;
        if (hideWhenFull && health01 >= 0.999f)
            visible = false;

        canvas.enabled = visible;
    }

    private void FaceCamera()
    {
        if (barView == null || barView.Root == null || canvas == null || !canvas.enabled)
            return;

        barView.Root.position = transform.position + worldOffset;

        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera == null)
            return;

        barView.Root.rotation = viewCamera.transform.rotation;
    }
}
