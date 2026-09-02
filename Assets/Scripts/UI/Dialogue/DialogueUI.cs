using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时构建的底部对话框：点击或空格翻句，不暂停时间。
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueUI : MonoBehaviour
{
    private const int OverlaySortingOrder = 150;

    public static DialogueUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    private RectTransform rootRect;
    private GameObject panelObject;
    private Text speakerText;
    private Text bodyText;
    private Text hintText;
    private string[] lines = Array.Empty<string>();
    private int lineIndex;
    private Action onComplete;
    private float acceptInputAt;
    private bool lockedPlayer;

    public static DialogueUI Ensure()
    {
        if (Instance != null)
            return Instance;

        GameObject host = new GameObject("DialogueUI");
        return host.AddComponent<DialogueUI>();
    }

    public void Show(string speaker, string[] dialogueLines, Action completed = null)
    {
        EnsureUi();

        lines = dialogueLines != null && dialogueLines.Length > 0
            ? dialogueLines
            : new[] { "……" };
        lineIndex = 0;
        onComplete = completed;
        acceptInputAt = Time.unscaledTime + 0.2f;
        IsOpen = true;

        if (speakerText != null)
        {
            speakerText.text = string.IsNullOrEmpty(speaker) ? string.Empty : speaker;
            ChineseUIFont.Apply(speakerText, 28, FontStyle.Bold);
        }

        RefreshBody();
        panelObject.SetActive(true);
        rootRect.gameObject.SetActive(true);

        LockPlayer(true);
    }

    public void Hide()
    {
        if (!IsOpen)
        {
            LockPlayer(false);
            return;
        }

        IsOpen = false;
        if (panelObject != null)
            panelObject.SetActive(false);

        Action completed = onComplete;
        onComplete = null;
        lines = Array.Empty<string>();
        lineIndex = 0;

        LockPlayer(false);
        completed?.Invoke();
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

        if (IsOpen)
        {
            IsOpen = false;
            LockPlayer(false);
        }
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (GameplayPauseMenu.IsOpen)
        {
            if (panelObject != null && panelObject.activeSelf)
                panelObject.SetActive(false);
            return;
        }

        if (panelObject != null && !panelObject.activeSelf)
            panelObject.SetActive(true);

        if (Time.unscaledTime < acceptInputAt)
            return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            Advance();
    }

    private void Advance()
    {
        lineIndex++;
        if (lineIndex >= lines.Length)
        {
            Hide();
            return;
        }

        RefreshBody();
        acceptInputAt = Time.unscaledTime + 0.08f;
    }

    private void RefreshBody()
    {
        if (bodyText == null)
            return;

        bodyText.text = lineIndex >= 0 && lineIndex < lines.Length ? lines[lineIndex] : string.Empty;
        ChineseUIFont.Apply(bodyText, 26);

        if (hintText != null)
        {
            bool lastLine = lineIndex >= lines.Length - 1;
            hintText.text = lastLine ? "点击或空格结束" : "点击或空格继续";
            ChineseUIFont.Apply(hintText, 18);
        }
    }

    private void LockPlayer(bool locked)
    {
        Player player = Player.Resolve();

        if (locked)
        {
            if (player != null && !lockedPlayer)
            {
                player.SetConversationLocked(true);
                lockedPlayer = true;
            }
        }
        else if (lockedPlayer)
        {
            player?.SetConversationLocked(false);
            lockedPlayer = false;
        }
    }

    private void EnsureUi()
    {
        if (rootRect != null)
            return;

        GameObject overlay = new GameObject("DialogueOverlay", typeof(RectTransform));
        overlay.transform.SetParent(transform, false);

        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        overlay.AddComponent<GraphicRaycaster>();

        GameObject root = new GameObject("DialogueRoot", typeof(RectTransform));
        root.transform.SetParent(overlay.transform, false);
        rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        panelObject = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(root.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 36f);
        panelRect.sizeDelta = new Vector2(920f, 210f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.06f, 0.04f, 0.92f);
        panelImage.raycastTarget = false;

        speakerText = CreateText(panelObject, "Speaker", string.Empty, 28, TextAnchor.UpperLeft);
        RectTransform speakerRect = speakerText.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0f, 1f);
        speakerRect.anchoredPosition = new Vector2(28f, -18f);
        speakerRect.sizeDelta = new Vector2(-56f, 36f);
        speakerText.color = new Color(0.95f, 0.82f, 0.45f, 1f);
        ChineseUIFont.Apply(speakerText, 28, FontStyle.Bold);

        bodyText = CreateText(panelObject, "Body", string.Empty, 26, TextAnchor.UpperLeft);
        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -58f);
        bodyRect.offsetMin = new Vector2(28f, 40f);
        bodyRect.offsetMax = new Vector2(-28f, -58f);
        bodyText.color = new Color(0.96f, 0.93f, 0.86f, 1f);

        hintText = CreateText(panelObject, "Hint", "点击或空格继续", 18, TextAnchor.LowerRight);
        RectTransform hintRect = hintText.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.anchoredPosition = new Vector2(-24f, 12f);
        hintRect.sizeDelta = new Vector2(-48f, 24f);
        hintText.color = new Color(0.78f, 0.72f, 0.58f, 0.9f);
        ChineseUIFont.Apply(hintText, 18);

        panelObject.SetActive(false);
    }

    private static Text CreateText(GameObject parent, string name, string text, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent.transform, false);
        Text label = textObject.GetComponent<Text>();
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
