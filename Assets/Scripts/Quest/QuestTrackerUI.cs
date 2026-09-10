using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 右上角任务追踪：显示当前任务标题与击杀进度。
/// 挂在 PlayerHUD 下时复用同一套 Canvas 缩放，避免嵌套 Overlay 在不同分辨率下错位。
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestTrackerUI : MonoBehaviour
{
    private const int OverlaySortingOrder = 120;

    public static QuestTrackerUI Instance { get; private set; }

    [SerializeField] private Vector2 anchoredPosition = new Vector2(-28f, -86f);
    [SerializeField] private Vector2 panelSize = new Vector2(360f, 118f);

    private RectTransform rootRect;
    private RectTransform panelRect;
    private GameObject panelObject;
    private Text headerText;
    private Text titleText;
    private Text objectiveText;

    public static QuestTrackerUI Ensure()
    {
        if (Instance != null)
            return Instance;

        PlayerHUD hud = PlayerHUD.Resolve();
        if (hud != null)
            return hud.EnsureQuestTracker();

        GameObject host = new GameObject("QuestTrackerUI");
        return host.AddComponent<QuestTrackerUI>();
    }

    public void ShowQuest(QuestDefinition definition, int current, int required)
    {
        EnsureUi();
        if (panelObject != null)
            panelObject.SetActive(true);

        Refresh(definition, current, required, completed: false);
    }

    public void ShowProgress(QuestDefinition definition, int current, int required, bool completed)
    {
        EnsureUi();
        if (panelObject != null)
            panelObject.SetActive(true);

        Refresh(definition, current, required, completed);
    }

    public void Hide()
    {
        if (panelObject != null)
            panelObject.SetActive(false);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureUi();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Refresh(QuestDefinition definition, int current, int required, bool completed)
    {
        if (definition == null)
            return;

        if (headerText != null)
        {
            headerText.text = completed ? "任务完成" : "当前任务";
            ChineseUIFont.Apply(headerText, 18, FontStyle.Bold);
            headerText.color = completed
                ? new Color(0.55f, 0.88f, 0.48f, 1f)
                : new Color(0.95f, 0.82f, 0.45f, 1f);
        }

        if (titleText != null)
        {
            titleText.text = definition.title ?? string.Empty;
            ChineseUIFont.Apply(titleText, 22, FontStyle.Bold);
        }

        if (objectiveText != null)
        {
            int shownRequired = Mathf.Max(1, required);
            int shownCurrent = Mathf.Clamp(current, 0, shownRequired);
            string label = string.IsNullOrEmpty(definition.objectiveLabel) ? "目标" : definition.objectiveLabel;
            objectiveText.text = $"{label}  {shownCurrent}/{shownRequired}";
            ChineseUIFont.Apply(objectiveText, 20);
        }
    }

    private void EnsureUi()
    {
        if (rootRect != null)
        {
            ApplyPanelLayout();
            return;
        }

        Transform uiParent = CreateOrReuseHost();
        Transform existingRoot = uiParent.Find("QuestTrackerRoot");
        GameObject root = existingRoot != null
            ? existingRoot.gameObject
            : new GameObject("QuestTrackerRoot", typeof(RectTransform));
        if (existingRoot == null)
            root.transform.SetParent(uiParent, false);

        rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        Transform existingPanel = root.transform.Find("QuestPanel");
        panelObject = existingPanel != null
            ? existingPanel.gameObject
            : new GameObject("QuestPanel", typeof(RectTransform), typeof(Image));
        if (existingPanel == null)
            panelObject.transform.SetParent(root.transform, false);

        panelRect = panelObject.GetComponent<RectTransform>();
        ApplyPanelLayout();

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.06f, 0.04f, 0.82f);
        panelImage.raycastTarget = false;

        headerText = CreateText(panelObject, "Header", "当前任务", 18, TextAnchor.UpperLeft);
        RectTransform headerRect = headerText.rectTransform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0f, 1f);
        headerRect.anchoredPosition = new Vector2(16f, -10f);
        headerRect.sizeDelta = new Vector2(-32f, 22f);
        headerText.color = new Color(0.95f, 0.82f, 0.45f, 1f);
        ChineseUIFont.Apply(headerText, 18, FontStyle.Bold);

        titleText = CreateText(panelObject, "Title", string.Empty, 22, TextAnchor.UpperLeft);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(16f, -34f);
        titleRect.sizeDelta = new Vector2(-32f, 28f);
        titleText.color = new Color(0.96f, 0.93f, 0.86f, 1f);
        ChineseUIFont.Apply(titleText, 22, FontStyle.Bold);

        objectiveText = CreateText(panelObject, "Objective", string.Empty, 20, TextAnchor.UpperLeft);
        RectTransform objectiveRect = objectiveText.rectTransform;
        objectiveRect.anchorMin = new Vector2(0f, 1f);
        objectiveRect.anchorMax = new Vector2(1f, 1f);
        objectiveRect.pivot = new Vector2(0f, 1f);
        objectiveRect.anchoredPosition = new Vector2(16f, -68f);
        objectiveRect.sizeDelta = new Vector2(-32f, 36f);
        objectiveText.color = new Color(0.86f, 0.8f, 0.68f, 1f);
        ChineseUIFont.Apply(objectiveText, 20);

        panelObject.SetActive(false);
    }

    private Transform CreateOrReuseHost()
    {
        Transform existingOverlay = transform.Find("QuestTrackerOverlay");
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            if (existingOverlay != null)
                existingOverlay.gameObject.SetActive(false);
            return transform;
        }

        GameObject overlay = existingOverlay != null
            ? existingOverlay.gameObject
            : new GameObject("QuestTrackerOverlay", typeof(RectTransform));
        if (existingOverlay == null)
            overlay.transform.SetParent(transform, false);

        Canvas canvas = overlay.GetComponent<Canvas>();
        if (canvas == null)
            canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (overlay.GetComponent<GraphicRaycaster>() == null)
            overlay.AddComponent<GraphicRaycaster>();

        return overlay.transform;
    }

    private void ApplyPanelLayout()
    {
        if (panelRect == null && panelObject != null)
            panelRect = panelObject.GetComponent<RectTransform>();
        if (panelRect == null)
            return;

        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = panelSize;
        panelRect.localScale = Vector3.one;
    }

    private void OnValidate()
    {
        anchoredPosition.x = Mathf.Min(0f, anchoredPosition.x);
        anchoredPosition.y = Mathf.Min(0f, anchoredPosition.y);
        panelSize.x = Mathf.Max(200f, panelSize.x);
        panelSize.y = Mathf.Max(80f, panelSize.y);
        if (panelRect != null)
            ApplyPanelLayout();
    }

    private static Text CreateText(GameObject parent, string name, string text, int fontSize, TextAnchor alignment)
    {
        Transform existing = parent != null ? parent.transform.Find(name) : null;
        Text label = existing != null ? existing.GetComponent<Text>() : null;
        if (label == null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent.transform, false);
            label = textObject.GetComponent<Text>();
        }

        label.text = text;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        ChineseUIFont.Apply(label, fontSize);
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
