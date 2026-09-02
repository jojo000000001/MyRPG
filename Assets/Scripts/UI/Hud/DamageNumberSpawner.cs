using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 世界空间伤害飘字：普通黄字，暴击红字。
/// 由 Resources/Systems/DamageNumberSpawner.prefab 实例化。
/// </summary>
[DisallowMultipleComponent]
public sealed class DamageNumberSpawner : MonoBehaviour
{
    private const string PrefabResourcePath = "Systems/DamageNumberSpawner";

    private static DamageNumberSpawner instance;

    [Header("样式")]
    [SerializeField] private float normalFontSize = 4.5f;
    [SerializeField] private float critFontSize = 6f;
    [SerializeField] private Color normalColor = new Color(1f, 0.95f, 0.15f, 1f);
    [SerializeField] private Color critColor = new Color(1f, 0.35f, 0.25f, 1f);

    [Header("动画")]
    [SerializeField] private float lifetime = 0.85f;
    [SerializeField] private float riseSpeed = 1.8f;
    [SerializeField] private float horizontalSpread = 0.2f;
    [SerializeField] private float worldYOffset = 1.1f;
    [SerializeField] private float critScaleMultiplier = 1.15f;

    [Header("对象池")]
    [SerializeField] private int poolSize = 16;
    [SerializeField] private Transform popupRoot;

    private readonly Queue<DamageNumberPopup> pool = new Queue<DamageNumberPopup>();
    private TMP_FontAsset fontAsset;
    private static Material overlayMaterial;

    public static void Show(DamageInfo damage, Vector3 fallbackWorldPosition)
    {
        if (!Application.isPlaying || damage.amount <= 0)
            return;

        DamageNumberSpawner spawner = Instance;
        if (spawner == null)
        {
            Debug.LogWarning("DamageNumberSpawner: missing instance. Run Tools/MyRPG/Setup System Prefabs.");
            return;
        }

        spawner.Spawn(damage, fallbackWorldPosition);
    }

    public static DamageNumberSpawner Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"DamageNumberSpawner: missing prefab at Resources/{PrefabResourcePath}.prefab. Run Tools/MyRPG/Setup System Prefabs.");
            return;
        }

        Instantiate(prefab);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (!ValidatePopupRoot())
            return;

        EnsureFont();
        WarmPool();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private bool ValidatePopupRoot()
    {
        if (popupRoot != null)
            return true;

        Debug.LogError("DamageNumberSpawner: assign popupRoot on the prefab.", this);
        enabled = false;
        return false;
    }

    private void EnsureFont()
    {
        if (fontAsset == null)
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (overlayMaterial == null)
            overlayMaterial = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Overlay");
    }

    internal static void ApplyUnlitMaterial(TextMeshPro text)
    {
        if (text == null)
            return;

        if (overlayMaterial != null)
            text.fontSharedMaterial = overlayMaterial;

        var renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void WarmPool()
    {
        int count = Mathf.Max(4, poolSize);
        for (int i = 0; i < count; i++)
            pool.Enqueue(CreatePopup());
    }

    private DamageNumberPopup CreatePopup()
    {
        var popupObject = new GameObject("DamageNumber");
        popupObject.transform.SetParent(popupRoot, false);

        var text = popupObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;
        text.richText = false;
        text.raycastTarget = false;

        if (fontAsset != null)
            text.font = fontAsset;

        ApplyUnlitMaterial(text);

        var popup = popupObject.AddComponent<DamageNumberPopup>();
        popup.Initialize(this);
        popupObject.SetActive(false);
        return popup;
    }

    private void Spawn(DamageInfo damage, Vector3 fallbackWorldPosition)
    {
        EnsureFont();

        DamageNumberPopup popup = pool.Count > 0 ? pool.Dequeue() : CreatePopup();

        Vector3 spawnPosition = damage.point;
        if (spawnPosition.sqrMagnitude < 0.0001f)
            spawnPosition = fallbackWorldPosition;

        spawnPosition += Vector3.up * worldYOffset;
        spawnPosition += new Vector3(
            Random.Range(-horizontalSpread, horizontalSpread),
            0f,
            Random.Range(-horizontalSpread, horizontalSpread));

        var style = new DamageNumberPopup.Style
        {
            normalFontSize = normalFontSize,
            critFontSize = critFontSize,
            normalColor = normalColor,
            critColor = critColor,
            lifetime = lifetime,
            riseSpeed = riseSpeed,
            critScaleMultiplier = critScaleMultiplier,
        };

        popup.Play(damage, spawnPosition, style, fontAsset);
    }

    internal void Release(DamageNumberPopup popup)
    {
        if (popup == null)
            return;

        popup.gameObject.SetActive(false);
        pool.Enqueue(popup);
    }
}
