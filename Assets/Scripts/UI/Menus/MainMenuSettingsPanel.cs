using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单设置面板：主音量调节。
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuSettingsPanel : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.25f, 0.16f, 0.10f, 0.96f);
    private static readonly Color PrimaryTextColor = new Color(0.18f, 0.1f, 0.045f, 1f);
    private static readonly Color ButtonColor = new Color(0.55f, 0.38f, 0.22f, 1f);
    private static readonly Color ButtonTextColor = new Color(0.98f, 0.93f, 0.82f, 1f);
    private static readonly Color MutedTextColor = new Color(0.35f, 0.24f, 0.14f, 0.85f);

    private GameObject root;
    private Slider masterVolumeSlider;
    private Text volumeValueText;

    public System.Action HiddenCallback;

    public static MainMenuSettingsPanel Ensure(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return null;

        MainMenuSettingsPanel existing = canvasRoot.GetComponentInChildren<MainMenuSettingsPanel>(true);
        if (existing != null)
            return existing;

        GameObject host = new GameObject("MainMenuSettingsPanel", typeof(RectTransform), typeof(MainMenuSettingsPanel));
        host.transform.SetParent(canvasRoot, false);
        Stretch(host.GetComponent<RectTransform>());
        return host.GetComponent<MainMenuSettingsPanel>();
    }

    private void Awake()
    {
        if (root == null)
            BuildUi();
    }

    public void Show()
    {
        if (root == null)
            BuildUi();

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);

        UpdateVolumeLabel(GameSettings.MasterVolume);
        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        System.Action callback = HiddenCallback;
        HiddenCallback = null;
        callback?.Invoke();
    }

    private void BuildUi()
    {
        root = CreateChild(transform, "SettingsOverlay", typeof(RectTransform)).gameObject;
        Stretch(root.GetComponent<RectTransform>());

        Image dim = CreateChild(root.transform, "Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.45f);
        dim.raycastTarget = true;

        GameObject card = CreateChild(root.transform, "SettingsCard", typeof(RectTransform), typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(480f, 280f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = PanelColor;

        Text title = CreateText(card.transform, "Title", "设置", 32, TextAnchor.MiddleCenter, FontStyle.Bold, PrimaryTextColor);
        PlaceTop(title.rectTransform, -28f, 48f);

        Text volumeLabel = CreateText(card.transform, "VolumeLabel", "主音量", 22, TextAnchor.MiddleLeft, FontStyle.Bold, PrimaryTextColor);
        PlaceRow(volumeLabel.rectTransform, 0.58f);

        GameObject sliderObject = CreateChild(card.transform, "MasterVolumeSlider", typeof(RectTransform), typeof(Slider));
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        PlaceRow(sliderRect, 0.46f, new Vector2(-72f, 36f));

        GameObject track = CreateChild(sliderObject.transform, "Background", typeof(RectTransform), typeof(Image));
        Stretch(track.GetComponent<RectTransform>(), 0f, 6f);
        track.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 0.85f);

        GameObject fillArea = CreateChild(sliderObject.transform, "Fill Area", typeof(RectTransform));
        Stretch(fillArea.GetComponent<RectTransform>(), 8f, 10f);

        GameObject fill = CreateChild(fillArea.transform, "Fill", typeof(RectTransform), typeof(Image));
        Stretch(fill.GetComponent<RectTransform>());
        fill.GetComponent<Image>().color = ButtonColor;

        GameObject handleSlideArea = CreateChild(sliderObject.transform, "Handle Slide Area", typeof(RectTransform));
        Stretch(handleSlideArea.GetComponent<RectTransform>(), 8f, 0f);

        GameObject handle = CreateChild(handleSlideArea.transform, "Handle", typeof(RectTransform), typeof(Image));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 28f);
        handle.GetComponent<Image>().color = new Color(0.92f, 0.84f, 0.68f, 1f);

        masterVolumeSlider = sliderObject.GetComponent<Slider>();
        masterVolumeSlider.fillRect = fill.GetComponent<RectTransform>();
        masterVolumeSlider.handleRect = handleRect;
        masterVolumeSlider.targetGraphic = handle.GetComponent<Image>();
        masterVolumeSlider.direction = Slider.Direction.LeftToRight;
        masterVolumeSlider.minValue = 0f;
        masterVolumeSlider.maxValue = 1f;
        masterVolumeSlider.wholeNumbers = false;
        masterVolumeSlider.value = GameSettings.MasterVolume;
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        volumeValueText = CreateText(card.transform, "VolumeValue", "100%", 20, TextAnchor.MiddleRight, FontStyle.Normal, MutedTextColor);
        PlaceRow(volumeValueText.rectTransform, 0.34f);

        Button backButton = CreateButton(card.transform, "BackButton", "返回", 0.14f);
        backButton.onClick.AddListener(Hide);

        root.SetActive(false);
    }

    private void OnMasterVolumeChanged(float value)
    {
        GameSettings.MasterVolume = value;
        UpdateVolumeLabel(value);
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private Button CreateButton(Transform parent, string name, string label, float anchorY)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, anchorY);
        buttonRect.anchorMax = new Vector2(0.5f, anchorY);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(220f, 48f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject.transform, "Label", label, 22, TextAnchor.MiddleCenter, FontStyle.Bold, ButtonTextColor);
        Stretch(text.rectTransform);

        return button;
    }

    private static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, FontStyle style, Color color)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(Text));
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        ChineseUIFont.Apply(label, fontSize, style);
        return label;
    }

    private static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void Stretch(RectTransform rect, float horizontalPadding = 0f, float verticalPadding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        rect.localScale = Vector3.one;
    }

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-48f, height);
    }

    private static void PlaceRow(RectTransform rect, float anchorY, Vector2? sizeDelta = null)
    {
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta ?? new Vector2(-96f, 28f);
    }
}
