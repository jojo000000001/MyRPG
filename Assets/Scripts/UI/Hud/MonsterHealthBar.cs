using UnityEngine;

/// <summary>
/// 怪物头顶血条：哥布林用皮革骨头黄条，与玩家 Kenney 绿条区分。
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterHealthBar : MonoBehaviour
{
    private const string BarRootName = "MonsterHealthBar";
    private const string DefaultStyleResource = "UI/EnemyHealthBar/GoblinHealthBarStyle";

    [Header("Style")]
    [SerializeField] protected EnemyHealthBarStyle style;
    [SerializeField] protected string styleResourcePath = DefaultStyleResource;

    [Header("Prefab")]
    [SerializeField] protected RectTransform barRoot;
    [SerializeField] protected GameObject barPrefab;

    [Header("Layout")]
    [SerializeField] protected Vector3 worldOffset = new Vector3(0f, 2.35f, 0f);
    [SerializeField] protected Vector2 barSize = new Vector2(2.55f, 0.52f);
    [SerializeField] protected float pixelsPerUnit = 100f;
    [SerializeField] protected int sortingOrder = 50;

    [Header("Display")]
    [SerializeField] protected bool hideWhenFull;
    [SerializeField] protected bool hideOnDeath = true;
    [SerializeField] protected float smoothSpeed = 12f;

    private Monster monster;
    private EnemyBarView barView;
    private Canvas canvas;
    private Camera viewCamera;
    private float displayedHealth01 = 1f;
    private Vector2 pixelSize;
    private Vector2 fillInsetPixels;
    private bool barFromPrefab;

    protected virtual void Awake()
    {
        monster = GetComponent<Monster>();
        ResolveStyle();
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

        barView?.SetFill01(displayedHealth01);
        UpdateVisibility(target01);
        FaceCamera();
    }

    private void ResolveStyle()
    {
        if (style != null)
            return;

        if (!string.IsNullOrEmpty(styleResourcePath))
            style = Resources.Load<EnemyHealthBarStyle>(styleResourcePath);

        if (style == null)
            style = Resources.Load<EnemyHealthBarStyle>(DefaultStyleResource);
    }

    private void EnsureBar()
    {
        if (barView != null && barView.IsReady && canvas != null)
            return;

        RectTransform existingRoot = ResolveExistingRoot();
        if (existingRoot != null)
        {
            if (EnemyBarView.HasExpectedParts(existingRoot))
            {
                barView = EnemyBarView.Bind(existingRoot);
                canvas = existingRoot.GetComponent<Canvas>();
                barRoot = existingRoot;
                barFromPrefab = true;
            }
            else
            {
                existingRoot.name = BarRootName + "_Legacy";
                if (Application.isPlaying)
                    Destroy(existingRoot.gameObject);
                else
                    DestroyImmediate(existingRoot.gameObject);
            }
        }

        if (barView == null || !barView.IsReady)
            CreateBar();

        if (canvas == null && barView != null)
            canvas = barView.Root.GetComponent<Canvas>();

        if (barFromPrefab)
        {
            barView?.CaptureFillWidth();
            if (barView != null && barView.Root != null)
                pixelSize = barView.Root.sizeDelta;
            return;
        }

        ApplyWorldLayout();
        barView?.ApplyStyle(style);
    }

    private RectTransform ResolveExistingRoot()
    {
        if (barRoot != null)
            return barRoot;

        Transform existing = transform.Find(BarRootName);
        return existing as RectTransform;
    }

    private void CreateBar()
    {
        if (TryInstantiateBarPrefab())
            return;

        barView = EnemyBarView.Create(transform, BarRootName);

        canvas = barView.Root.GetComponent<Canvas>();
        if (canvas == null)
            canvas = barView.Root.gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        barRoot = barView.Root;
    }

    private bool TryInstantiateBarPrefab()
    {
        if (barPrefab == null)
            return false;

        GameObject instance = Instantiate(barPrefab, transform, false);
        instance.name = BarRootName;
        RectTransform root = instance.GetComponent<RectTransform>();
        if (root == null || !EnemyBarView.HasExpectedParts(root))
        {
            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
            return false;
        }

        barView = EnemyBarView.Bind(root);
        canvas = instance.GetComponent<Canvas>();
        barRoot = root;
        barFromPrefab = true;
        return barView.IsReady;
    }

    private void ApplyWorldLayout()
    {
        if (barView == null || barView.Root == null)
            return;

        Vector2 size = style != null ? style.worldBarSize : barSize;
        Vector3 offset = style != null ? style.worldOffset : worldOffset;
        fillInsetPixels = style != null ? style.fillInsetPixels : new Vector2(22f, 11f);

        float ppu = Mathf.Max(1f, pixelsPerUnit);
        pixelSize = new Vector2(
            Mathf.Max(1f, size.x * ppu),
            Mathf.Max(1f, size.y * ppu));

        RectTransform root = barView.Root;
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.localScale = Vector3.one / ppu;
        root.position = transform.position + offset;

        if (canvas != null)
            canvas.sortingOrder = sortingOrder;

        barView.LayoutContents(pixelSize, fillInsetPixels);
    }

    private void UpdateImmediate()
    {
        displayedHealth01 = GetHealth01();
        barView?.SetFill01(displayedHealth01);
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
        if (monster != null && !monster.ShouldShowHealthBar)
            visible = false;
        if (hideWhenFull && health01 >= 0.999f)
            visible = false;

        canvas.enabled = visible;
    }

    private void FaceCamera()
    {
        if (barView == null || barView.Root == null || canvas == null || !canvas.enabled)
            return;

        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera == null)
            return;

        barView.Root.rotation = viewCamera.transform.rotation;
    }
}
