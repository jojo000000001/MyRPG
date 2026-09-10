using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时构建的底部对话框：点击或空格翻句。默认不暂停时间，教学对话可选择暂停。
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueUI : MonoBehaviour
{
    private const int OverlaySortingOrder = 150;

    public static DialogueUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 10f);
    [SerializeField] private Vector2 panelSize = new Vector2(920f, 210f);

    private RectTransform rootRect;
    private RectTransform panelRect;
    private GameObject panelObject;
    private TextMeshProUGUI speakerText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI hintText;
    private GameObject choiceRoot;
    private readonly List<Button> choiceButtons = new List<Button>();
    private readonly List<Image> choiceImages = new List<Image>();
    private readonly List<TextMeshProUGUI> choiceLabels = new List<TextMeshProUGUI>();
    private string[] lines = Array.Empty<string>();
    private string[] choices = Array.Empty<string>();
    private int lineIndex;
    private int highlightedChoice;
    private Action onComplete;
    private Action<int> onChoice;
    private bool showingChoices;
    private float acceptInputAt;
    private bool lockedPlayer;
    private bool pausedTime;
    private float previousTimeScale = 1f;

    public static DialogueUI Ensure()
    {
        if (Instance != null)
            return Instance;

        PlayerHUD hud = PlayerHUD.Resolve();
        if (hud != null)
            return hud.EnsureDialogue();

        GameObject host = new GameObject("DialogueUI");
        return host.AddComponent<DialogueUI>();
    }

    public void Show(string speaker, string[] dialogueLines, Action completed = null, bool pauseTime = false)
    {
        EnsureUi();

        if (pauseTime)
            CloseOpenInventory();

        lines = dialogueLines != null && dialogueLines.Length > 0
            ? dialogueLines
            : new[] { "……" };
        lineIndex = 0;
        onComplete = completed;
        acceptInputAt = Time.unscaledTime + 0.2f;
        IsOpen = true;
        showingChoices = false;
        onChoice = null;
        HideChoiceButtons();
        SetTimePaused(pauseTime);

        if (speakerText != null)
        {
            speakerText.text = string.IsNullOrEmpty(speaker) ? string.Empty : speaker;
            ChineseUITmpFont.Apply(speakerText, 28f, FontStyles.Bold);
        }

        RefreshBody();
        panelObject.SetActive(true);
        rootRect.gameObject.SetActive(true);
        rootRect.SetAsLastSibling();
        transform.SetAsLastSibling();

        LockPlayer(true);
    }

    public void ShowChoices(string speaker, string prompt, string[] options, Action<int> chosen, bool pauseTime = true)
    {
        EnsureUi();
        choices = options != null && options.Length > 0 ? options : new[] { "离开" };
        highlightedChoice = 0;
        Show(speaker, new[] { string.IsNullOrEmpty(prompt) ? "……" : prompt }, null, pauseTime);
        showingChoices = true;
        onChoice = chosen;
        GameplayCursor.UnlockForUI();
        RefreshChoices();
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
        onChoice = null;
        showingChoices = false;
        HideChoiceButtons();
        lines = Array.Empty<string>();
        choices = Array.Empty<string>();
        lineIndex = 0;

        LockPlayer(false);
        SetTimePaused(false);
        RestoreGameplayCursor();
        completed?.Invoke();
    }

    public void ConfirmChoice(int index)
    {
        if (!IsOpen || !showingChoices)
            return;

        if (index < 0 || index >= choices.Length)
            return;

        Action<int> chosen = onChoice;
        onComplete = null;
        onChoice = null;
        showingChoices = false;
        HideChoiceButtons();
        IsOpen = false;
        if (panelObject != null)
            panelObject.SetActive(false);

        lines = Array.Empty<string>();
        choices = Array.Empty<string>();
        lineIndex = 0;
        LockPlayer(false);
        SetTimePaused(false);
        chosen?.Invoke(index);
        if (!ShopUI.IsOpen)
            RestoreGameplayCursor();
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
            SetTimePaused(false);
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

        if (showingChoices)
        {
            HandleChoiceInput();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            Advance();
    }

    private void HandleChoiceInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            highlightedChoice = (highlightedChoice - 1 + choices.Length) % choices.Length;
            RefreshChoiceHighlight();
            acceptInputAt = Time.unscaledTime + 0.08f;
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            highlightedChoice = (highlightedChoice + 1) % choices.Length;
            RefreshChoiceHighlight();
            acceptInputAt = Time.unscaledTime + 0.08f;
            return;
        }

        if (GameplayPauseMenu.TryConsumeEscape())
        {
            Hide();
            return;
        }

        for (int i = 0; i < choices.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                ConfirmChoice(i);
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ConfirmChoice(highlightedChoice);
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
        ChineseUITmpFont.Apply(bodyText, 26f);

        if (hintText != null)
        {
            bool lastLine = lineIndex >= lines.Length - 1;
            if (showingChoices)
                hintText.text = "↑↓ 选择    空格确认";
            else
                hintText.text = lastLine ? "点击或空格结束" : "点击或空格继续";
            ChineseUITmpFont.Apply(hintText, 18f);
        }
    }

    private void SetTimePaused(bool pause)
    {
        if (pause)
        {
            if (pausedTime)
                return;

            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            pausedTime = true;
            GameplayCursor.UnlockForUI();
            return;
        }

        if (!pausedTime)
            return;

        pausedTime = false;
        if (!GameplayPauseMenu.IsOpen)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        PlayerHUD hud = PlayerHUD.Resolve();
        if (hud != null && hud.IsInventoryOpen)
            return;

        if (!GameplayPauseMenu.IsOpen)
            GameplayCursor.LockForGameplay();
    }

    private static void RestoreGameplayCursor()
    {
        if (ShopUI.IsOpen || GameplayPauseMenu.IsOpen)
            return;

        PlayerHUD hud = PlayerHUD.Resolve();
        if (hud != null && hud.IsInventoryOpen)
            return;

        GameplayCursor.LockForGameplay();
    }

    private static void CloseOpenInventory()
    {
        PlayerHUD hud = PlayerHUD.Resolve();
        InventoryUI inventoryUi = hud != null ? hud.InventoryUi : null;
        if (inventoryUi != null && inventoryUi.IsOpen)
            inventoryUi.SetOpen(false);
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
        {
            ApplyPanelLayout();
            return;
        }

        Transform uiParent = CreateOrReuseHost();
        Transform existingRoot = uiParent.Find("DialogueRoot");
        GameObject root = existingRoot != null
            ? existingRoot.gameObject
            : new GameObject("DialogueRoot", typeof(RectTransform));
        if (existingRoot == null)
            root.transform.SetParent(uiParent, false);

        rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        Transform existingPanel = root.transform.Find("DialoguePanel");
        if (existingPanel != null)
        {
            panelObject = existingPanel.gameObject;
        }
        else
        {
            panelObject = SheikahUiStyle.CreateCard(root.transform, "DialoguePanel", panelSize, SheikahUiStyle.PanelSprite, 2.0f);
        }

        panelRect = panelObject.GetComponent<RectTransform>();
        ApplyPanelLayout();
        SheikahUiStyle.EnsureCardChrome(panelObject.transform, 2.0f);

        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.enabled = false;
            panelImage.raycastTarget = false;
        }

        speakerText = CreateText(panelObject, "Speaker", string.Empty, 28f, TextAlignmentOptions.TopLeft, FontStyles.Bold, SheikahUiStyle.Orange);
        RectTransform speakerRect = speakerText.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0f, 1f);
        speakerRect.anchoredPosition = new Vector2(36f, -18f);
        speakerRect.sizeDelta = new Vector2(-72f, 36f);

        bodyText = CreateText(panelObject, "Body", string.Empty, 26f, TextAlignmentOptions.TopLeft, FontStyles.Normal, SheikahUiStyle.Text);
        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -58f);
        bodyRect.offsetMin = new Vector2(36f, 40f);
        bodyRect.offsetMax = new Vector2(-36f, -58f);

        hintText = CreateText(panelObject, "Hint", "点击或空格继续", 18f, TextAlignmentOptions.BottomRight, FontStyles.Normal, SheikahUiStyle.Muted);
        RectTransform hintRect = hintText.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.anchoredPosition = new Vector2(-32f, 16f);
        hintRect.sizeDelta = new Vector2(-64f, 24f);

        panelObject.SetActive(false);
        CreateChoiceButtons();
    }

    private void CreateChoiceButtons()
    {
        if (choiceRoot != null || panelObject == null)
            return;

        choiceRoot = new GameObject("Choices", typeof(RectTransform));
        choiceRoot.transform.SetParent(panelObject.transform, false);
        RectTransform rootRectLocal = choiceRoot.GetComponent<RectTransform>();
        rootRectLocal.anchorMin = new Vector2(1f, 0.5f);
        rootRectLocal.anchorMax = new Vector2(1f, 0.5f);
        rootRectLocal.pivot = new Vector2(1f, 0.5f);
        rootRectLocal.anchoredPosition = new Vector2(-24f, 8f);
        rootRectLocal.sizeDelta = new Vector2(240f, 120f);

        for (int i = 0; i < 4; i++)
        {
            int choiceIndex = i;
            GameObject buttonObject = new GameObject("Choice_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(choiceRoot.transform, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -i * 48f);
            rect.sizeDelta = new Vector2(0f, 42f);

            Image image = buttonObject.GetComponent<Image>();
            SheikahUiStyle.ApplySliced(image, SheikahUiStyle.SlotSprite, SheikahUiStyle.Inactive, 5.5f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ConfirmChoice(choiceIndex));

            TextMeshProUGUI label = CreateText(buttonObject, "Label", string.Empty, 22f, TextAlignmentOptions.Center, FontStyles.Bold, SheikahUiStyle.Text);
            SheikahUiStyle.Stretch(label.rectTransform);

            choiceButtons.Add(button);
            choiceImages.Add(image);
            choiceLabels.Add(label);
            buttonObject.SetActive(false);
        }

        choiceRoot.SetActive(false);
    }

    private void RefreshChoices()
    {
        CreateChoiceButtons();
        if (choiceRoot == null)
            return;

        choiceRoot.SetActive(true);
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            bool active = i < choices.Length;
            choiceButtons[i].gameObject.SetActive(active);
            if (!active)
                continue;

            choiceLabels[i].text = choices[i];
            ChineseUITmpFont.Apply(choiceLabels[i], 22f, FontStyles.Bold);
        }

        RefreshChoiceHighlight();
        RefreshBody();
    }

    private void RefreshChoiceHighlight()
    {
        for (int i = 0; i < choiceImages.Count && i < choices.Length; i++)
        {
            bool selected = i == highlightedChoice;
            choiceImages[i].color = selected
                ? SheikahUiStyle.Orange
                : SheikahUiStyle.Inactive;
        }
    }

    private void HideChoiceButtons()
    {
        if (choiceRoot != null)
            choiceRoot.SetActive(false);
    }

    private Transform CreateOrReuseHost()
    {
        Transform existingOverlay = transform.Find("DialogueOverlay");
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            if (existingOverlay != null)
                existingOverlay.gameObject.SetActive(false);
            return transform;
        }

        GameObject overlay = existingOverlay != null
            ? existingOverlay.gameObject
            : new GameObject("DialogueOverlay", typeof(RectTransform));
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

        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = panelSize;
        panelRect.localScale = Vector3.one;
    }

    private static TextMeshProUGUI CreateText(
        GameObject parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style,
        Color color)
    {
        Transform existing = parent != null ? parent.transform.Find(name) : null;
        TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (label == null)
        {
            if (existing != null)
                existing.gameObject.SetActive(false);

            return SheikahUiStyle.CreateText(parent.transform, name, text, fontSize, alignment, style, color);
        }

        label.text = text;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        ChineseUITmpFont.Apply(label, fontSize, style);
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        SheikahUiStyle.Stretch(rect);
    }
}
