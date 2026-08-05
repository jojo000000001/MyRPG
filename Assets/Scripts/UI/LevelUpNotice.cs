using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 升级时屏幕中央短暂提示等级与成长奖励。
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelUpNotice : MonoBehaviour
{
    private const string RootName = "LevelUpNoticeRoot";

    [Header("Target")]
    [SerializeField] private Player player;

    [Header("Timing")]
    [SerializeField] private float showSeconds = 2.4f;
    [SerializeField] private float fadeSeconds = 0.35f;

    private RectTransform rootRect;
    private CanvasGroup canvasGroup;
    private Text titleText;
    private Text detailText;
    private Coroutine showRoutine;

    private bool prefabUi;

    private void Awake()
    {
        BindPrefabUiIfPresent();
    }

    private void OnEnable()
    {
        if (player == null)
            player = FindObjectOfType<Player>();

        if (player != null)
            player.LeveledUp += HandleLeveledUp;
    }

    private void OnDisable()
    {
        if (player != null)
            player.LeveledUp -= HandleLeveledUp;
    }

    private void Start()
    {
        if (!prefabUi)
            EnsureUi();

        HideImmediate();
    }

    private void HandleLeveledUp(Player.LevelUpInfo info)
    {
        EnsureUi();
        if (showRoutine != null)
            StopCoroutine(showRoutine);

        titleText.text = "升级！  " + info.NewLevel + " 级";
        detailText.text = BuildRewardText(info);
        ChineseUIFont.Apply(titleText, 42, FontStyle.Bold);
        ChineseUIFont.Apply(detailText, 22, FontStyle.Bold);
        showRoutine = StartCoroutine(ShowRoutine());
    }

    private static string BuildRewardText(Player.LevelUpInfo info)
    {
        return "+ " + info.BonusMaxHp + " 生命   + " + info.BonusAttack + " 攻击   + "
            + info.BonusDamagePercent.ToString("0.#") + "% 增伤   + "
            + (info.BonusCritChance * 100f).ToString("0.#") + "% 暴击   + "
            + (info.BonusLifeSteal * 100f).ToString("0.#") + "% 吸血";
    }

    private IEnumerator ShowRoutine()
    {
        rootRect.gameObject.SetActive(true);
        canvasGroup.alpha = 0f;

        float fadeIn = Mathf.Max(0.05f, fadeSeconds);
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeIn);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, showSeconds));

        elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
            yield return null;
        }

        HideImmediate();
        showRoutine = null;
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (rootRect != null)
            rootRect.gameObject.SetActive(false);
    }

    private void BindPrefabUiIfPresent()
    {
        Transform existing = transform.Find(RootName);
        if (existing == null)
            return;

        prefabUi = true;
        rootRect = existing as RectTransform;
        canvasGroup = existing.GetComponent<CanvasGroup>();
        Transform panel = existing.Find("Panel");
        titleText = panel != null ? panel.Find("Title")?.GetComponent<Text>() : null;
        detailText = panel != null ? panel.Find("Detail")?.GetComponent<Text>() : null;
    }

    private void EnsureUi()
    {
        if (prefabUi || rootRect != null)
            return;

        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        rootRect = root.GetComponent<RectTransform>();
        canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject backdropObject = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdropObject.transform.SetParent(root.transform, false);
        Image backdrop = backdropObject.GetComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.35f);
        Stretch(backdrop.rectTransform);

        GameObject panel = CreateChild<RectTransform>(root.transform, "Panel").gameObject;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620f, 150f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.18f, 0.12f, 0.08f, 0.94f);
        panelImage.raycastTarget = false;

        titleText = CreateLabel(panel.transform, "Title", 42, new Vector2(0f, 28f), new Vector2(580f, 56f));
        detailText = CreateLabel(panel.transform, "Detail", 22, new Vector2(0f, -34f), new Vector2(580f, 48f));
        detailText.color = new Color(0.92f, 0.84f, 0.62f, 1f);
    }

    private static Text CreateLabel(Transform parent, string name, int fontSize, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text label = textObject.GetComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = new Color(1f, 0.92f, 0.55f, 1f);
        label.raycastTarget = false;
        ChineseUIFont.Apply(label, fontSize, FontStyle.Bold);

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return label;
    }

    private static T CreateChild<T>(Transform parent, string name) where T : Component
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<T>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
