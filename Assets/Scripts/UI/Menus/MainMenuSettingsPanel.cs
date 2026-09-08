using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单设置面板：主音量调节。
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuSettingsPanel : MonoBehaviour
{
    private GameObject root;
    private Slider masterVolumeSlider;
    private TextMeshProUGUI volumeValueText;

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
        SheikahUiStyle.Stretch(host.GetComponent<RectTransform>());
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
        SetMainMenuCardVisible(false);
        root.transform.SetAsLastSibling();
        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        SetMainMenuCardVisible(true);

        System.Action callback = HiddenCallback;
        HiddenCallback = null;
        callback?.Invoke();
    }

    private void BuildUi()
    {
        Sprite panelSprite = SheikahUiStyle.PanelSprite;
        Sprite slotSprite = SheikahUiStyle.SlotSprite;

        root = SheikahUiStyle.CreateChild(transform, "SettingsOverlay").gameObject;
        SheikahUiStyle.Stretch(root.GetComponent<RectTransform>());

        Image dim = SheikahUiStyle.CreateChild(root.transform, "Dim", typeof(Image)).GetComponent<Image>();
        SheikahUiStyle.Stretch(dim.rectTransform);
        dim.color = SheikahUiStyle.Dim;
        dim.raycastTarget = true;

        GameObject card = SheikahUiStyle.CreateCard(root.transform, "SettingsCard", new Vector2(520f, 340f), panelSprite, 2.6f);

        TextMeshProUGUI title = SheikahUiStyle.CreateText(card.transform, "Title", "设置", 32f, TextAlignmentOptions.Center, FontStyles.Bold, SheikahUiStyle.Orange);
        PlaceTop(title.rectTransform, -40f, 48f);

        TextMeshProUGUI volumeLabel = SheikahUiStyle.CreateText(card.transform, "VolumeLabel", "主音量", 22f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, SheikahUiStyle.Text);
        PlaceRow(volumeLabel.rectTransform, 0.58f);

        GameObject sliderObject = SheikahUiStyle.CreateChild(card.transform, "MasterVolumeSlider", typeof(Slider));
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        PlaceRow(sliderRect, 0.44f, new Vector2(-96f, 36f));

        GameObject track = SheikahUiStyle.CreateChild(sliderObject.transform, "Background", typeof(Image));
        SheikahUiStyle.Stretch(track.GetComponent<RectTransform>(), 0f, 8f);
        SheikahUiStyle.ApplySliced(track.GetComponent<Image>(), slotSprite, SheikahUiStyle.Inactive, 6f);

        GameObject fillArea = SheikahUiStyle.CreateChild(sliderObject.transform, "Fill Area");
        SheikahUiStyle.Stretch(fillArea.GetComponent<RectTransform>(), 10f, 12f);

        GameObject fill = SheikahUiStyle.CreateChild(fillArea.transform, "Fill", typeof(Image));
        SheikahUiStyle.Stretch(fill.GetComponent<RectTransform>());
        fill.GetComponent<Image>().color = SheikahUiStyle.Orange;

        GameObject handleSlideArea = SheikahUiStyle.CreateChild(sliderObject.transform, "Handle Slide Area");
        SheikahUiStyle.Stretch(handleSlideArea.GetComponent<RectTransform>(), 10f, 0f);

        GameObject handle = SheikahUiStyle.CreateChild(handleSlideArea.transform, "Handle", typeof(Image));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(22f, 28f);
        handle.GetComponent<Image>().color = SheikahUiStyle.Text;

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

        volumeValueText = SheikahUiStyle.CreateText(card.transform, "VolumeValue", "100%", 20f, TextAlignmentOptions.MidlineRight, FontStyles.Normal, SheikahUiStyle.Muted);
        PlaceRow(volumeValueText.rectTransform, 0.30f);

        Button backButton = SheikahUiStyle.CreateButton(
            card.transform,
            "BackButton",
            "返回",
            new Vector2(220f, 48f),
            SheikahUiStyle.Inactive,
            SheikahUiStyle.Text,
            slotSprite,
            5.5f);
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.16f);
        backRect.anchorMax = new Vector2(0.5f, 0.16f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = Vector2.zero;
        backButton.onClick.AddListener(Hide);

        root.SetActive(false);
    }

    private void SetMainMenuCardVisible(bool visible)
    {
        Transform menuCard = FindMainMenuCard();
        if (menuCard != null)
            menuCard.gameObject.SetActive(visible);
    }

    private Transform FindMainMenuCard()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        return canvas.transform.Find("UIRoot/MenuCard");
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

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-96f, height);
    }

    private static void PlaceRow(RectTransform rect, float anchorY, Vector2? sizeDelta = null)
    {
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta ?? new Vector2(-120f, 28f);
    }
}
