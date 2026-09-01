using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo 胜负界面（运行时构建 UI）。
/// </summary>
[DisallowMultipleComponent]
public sealed class DemoFlowUI : MonoBehaviour
{
    private const int OverlaySortingOrder = 500;

    private RectTransform rootRect;
    private GameObject endScreenRoot;
    private Text endTitleText;
    private Text endBodyText;
    private Button restartButton;
    private Action restartCallback;

    private void Awake()
    {
        EnsureUi();
    }

    public void ShowDefeat(Action onRestart)
    {
        ShowEndScreen("你阵亡了", "哥布林占领了这片空地……", "再试一次", onRestart);
    }

    public void ShowVictory(Action onRestart)
    {
        ShowEndScreen("胜利！", "所有哥布林已被击退。", "再玩一次", onRestart);
    }

    private void ShowEndScreen(string title, string body, string buttonLabel, Action onRestart)
    {
        EnsureUi();

        restartCallback = onRestart;

        if (endScreenRoot != null)
            endScreenRoot.SetActive(true);

        if (endTitleText != null)
            endTitleText.text = title;

        if (endBodyText != null)
            endBodyText.text = body;

        if (restartButton != null)
        {
            Text buttonText = restartButton.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = buttonLabel;
        }

        GameplayCursor.UnlockForUI();
    }

    private void EnsureUi()
    {
        if (rootRect != null)
            return;

        GameObject overlay = new GameObject("DemoFlowOverlay", typeof(RectTransform));
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

        GameObject root = new GameObject("DemoFlowRoot", typeof(RectTransform));
        root.transform.SetParent(overlay.transform, false);
        rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        CreateEndScreen(root.transform);
    }

    private void CreateEndScreen(Transform parent)
    {
        endScreenRoot = new GameObject("EndScreen", typeof(RectTransform));
        endScreenRoot.transform.SetParent(parent, false);
        RectTransform endRect = endScreenRoot.GetComponent<RectTransform>();
        Stretch(endRect);

        Image backdrop = endScreenRoot.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.72f);
        backdrop.raycastTarget = true;

        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(endScreenRoot.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(520f, 300f);

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color(0.12f, 0.1f, 0.08f, 0.95f);

        endTitleText = CreateText(card, "Title", string.Empty, 42, TextAnchor.MiddleCenter);
        RectTransform titleRect = endTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(-48f, 60f);
        ChineseUIFont.Apply(endTitleText, 42, FontStyle.Bold);

        endBodyText = CreateText(card, "Body", string.Empty, 24, TextAnchor.MiddleCenter);
        RectTransform bodyRect = endBodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0.5f);
        bodyRect.anchorMax = new Vector2(1f, 0.5f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.anchoredPosition = new Vector2(0f, 10f);
        bodyRect.sizeDelta = new Vector2(-48f, 80f);

        restartButton = CreateButton(card.transform, "RestartButton", "再试一次");
        RectTransform buttonRect = restartButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 36f);
        buttonRect.sizeDelta = new Vector2(220f, 52f);
        restartButton.onClick.AddListener(HandleRestartClicked);

        endScreenRoot.SetActive(false);
    }

    private void HandleRestartClicked()
    {
        restartCallback?.Invoke();
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.78f, 0.58f, 0.22f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject, "Label", label, 24, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);

        return button;
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
