using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家 HUD 右上角卢比：绿宝石图标 + 数量。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRupeeHud : MonoBehaviour
{
    private const string RootName = "PlayerRupeeRoot";
    private const string IconResourcePath = "UI/Hud/ui_rupee_green";

    private static readonly Color AmountColor = new Color(0.55f, 1f, 0.62f, 1f);
    private static readonly Color OutlineColor = new Color(0.04f, 0.10f, 0.08f, 0.92f);

    [Header("Target")]
    [SerializeField] private Player player;

    [Header("Sprites")]
    [SerializeField] private Sprite rupeeSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-28f, -18f);
    [SerializeField] private Vector2 rootSize = new Vector2(210f, 56f);
    [SerializeField] private float iconSize = 48f;

    private RectTransform rootRect;
    private Image icon;
    private TextMeshProUGUI amountText;
    private int displayedAmount = int.MinValue;
    private Coroutine punchRoutine;

    public void BindPlayer(Player target)
    {
        Unsubscribe();
        player = target;
        Subscribe();
        EnsureUi();
        Refresh(animate: false);
    }

    private void Awake()
    {
        EnsureUi();
    }

    private void Start()
    {
        if (player == null)
            BindPlayer(Player.Resolve());
        else
            Refresh(animate: false);
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh(animate: false);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (player != null)
            player.WalletChanged += HandleWalletChanged;
    }

    private void Unsubscribe()
    {
        if (player != null)
            player.WalletChanged -= HandleWalletChanged;
    }

    private void HandleWalletChanged()
    {
        Refresh(animate: true);
    }

    private void Refresh(bool animate)
    {
        EnsureUi();
        int amount = player != null ? player.Rupees : 0;
        bool changed = displayedAmount != amount;
        displayedAmount = amount;

        if (amountText != null)
        {
            amountText.text = amount.ToString();
            ChineseUITmpFont.Apply(amountText, amountText.fontSize, FontStyles.Bold);
        }

        if (animate && changed && isActiveAndEnabled)
            Punch();
    }

    private void EnsureUi()
    {
        if (rootRect != null && amountText != null)
        {
            ApplyLayout();
            ApplySprite();
            return;
        }

        Transform existing = transform.Find(RootName);
        GameObject root = existing != null ? existing.gameObject : new GameObject(RootName, typeof(RectTransform));
        if (existing == null)
            root.transform.SetParent(transform, false);

        rootRect = root.GetComponent<RectTransform>();

        Transform iconTransform = root.transform.Find("Icon");
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(root.transform, false);
            iconTransform = iconObject.transform;
        }

        icon = iconTransform.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.color = Color.white;

        Transform amountTransform = root.transform.Find("Amount");
        if (amountTransform == null)
        {
            GameObject amountObject = new GameObject("Amount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            amountObject.transform.SetParent(root.transform, false);
            amountTransform = amountObject.transform;
        }

        amountText = amountTransform.GetComponent<TextMeshProUGUI>();
        amountText.raycastTarget = false;
        amountText.alignment = TextAlignmentOptions.MidlineLeft;
        amountText.color = AmountColor;
        amountText.enableWordWrapping = false;
        amountText.overflowMode = TextOverflowModes.Overflow;
        amountText.outlineWidth = 0.28f;
        amountText.outlineColor = OutlineColor;
        ChineseUITmpFont.Apply(amountText, 34f, FontStyles.Bold);

        ApplyLayout();
        ApplySprite();
    }

    private void ApplyLayout()
    {
        if (rootRect == null)
            return;

        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = rootSize;
        rootRect.localScale = Vector3.one;

        if (icon != null)
        {
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(4f, 0f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        }

        if (amountText != null)
        {
            RectTransform amountRect = amountText.rectTransform;
            amountRect.anchorMin = new Vector2(0f, 0f);
            amountRect.anchorMax = new Vector2(1f, 1f);
            amountRect.pivot = new Vector2(0f, 0.5f);
            amountRect.offsetMin = new Vector2(iconSize + 14f, 0f);
            amountRect.offsetMax = new Vector2(-4f, 0f);
        }
    }

    private void ApplySprite()
    {
        if (icon == null)
            return;

        if (rupeeSprite == null)
            rupeeSprite = Resources.Load<Sprite>(IconResourcePath);

        icon.sprite = rupeeSprite;
        icon.enabled = rupeeSprite != null;
    }

    private void Punch()
    {
        if (punchRoutine != null)
            StopCoroutine(punchRoutine);

        punchRoutine = StartCoroutine(PunchRoutine());
    }

    private System.Collections.IEnumerator PunchRoutine()
    {
        if (rootRect == null)
            yield break;

        float elapsed = 0f;
        const float duration = 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
            rootRect.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        rootRect.localScale = Vector3.one;
        punchRoutine = null;
    }
}
