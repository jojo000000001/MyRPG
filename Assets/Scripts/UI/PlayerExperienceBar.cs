using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左上角经验条：显示等级、当前/升级所需经验与进度条。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerExperienceBar : MonoBehaviour
{
    private const string RootName = "PlayerExperienceBarRoot";
    private const string ResourceRoot = "UI/HealthBar/";

    [Header("Target")]
    [SerializeField] private Player player;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -60f);
    [SerializeField] private Vector2 barSize = new Vector2(280f, 18f);
    [SerializeField] private float capWidth = 12f;
    [SerializeField] private float smoothSpeed = 12f;

    [Header("Colors")]
    [SerializeField] private Color fillColor = new Color(0.42f, 0.72f, 1f, 1f);

    private RectTransform rootRect;
    private RectTransform fillMaskRect;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI expText;
    private float displayedProgress01;

    public static void EnsureForHud(PlayerHealthBar healthBar)
    {
        if (healthBar == null)
            return;

        if (healthBar.GetComponent<PlayerExperienceBar>() == null)
            healthBar.gameObject.AddComponent<PlayerExperienceBar>();
    }

    private void Start()
    {
        if (player == null)
            player = FindObjectOfType<Player>();

        EnsureUi();
        UpdateImmediate();
    }

    private void LateUpdate()
    {
        if (player == null || rootRect == null)
            return;

        float target01 = player.ExperienceProgress01;
        float t = smoothSpeed <= 0f ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        displayedProgress01 = Mathf.Lerp(displayedProgress01, target01, t);

        UpdateFill(displayedProgress01);
        UpdateLabels();
    }

    private void EnsureUi()
    {
        Transform existing = transform.Find(RootName);
        if (existing != null)
        {
            rootRect = existing as RectTransform;
            CacheParts();
            ApplyLayout();
            return;
        }

        GameObject root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(transform, false);
        rootRect = root.GetComponent<RectTransform>();

        GameObject levelObject = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        levelObject.transform.SetParent(root.transform, false);
        levelText = levelObject.GetComponent<TextMeshProUGUI>();
        ConfigureLabel(levelText, TextAlignmentOptions.Left, 18f);

        GameObject expObject = new GameObject("ExpText", typeof(RectTransform), typeof(TextMeshProUGUI));
        expObject.transform.SetParent(root.transform, false);
        expText = expObject.GetComponent<TextMeshProUGUI>();
        ConfigureLabel(expText, TextAlignmentOptions.Right, 16f);
        expText.color = new Color(0.78f, 0.86f, 0.95f, 1f);

        CreateBar(root.transform, "Back", false);
        GameObject fillMask = new GameObject("FillMask", typeof(RectTransform), typeof(RectMask2D));
        fillMask.transform.SetParent(root.transform, false);
        fillMaskRect = fillMask.GetComponent<RectTransform>();
        CreateBar(fillMask.transform, "Fill", true);

        CacheParts();
        ApplyLayout();
    }

    private void CreateBar(Transform parent, string groupName, bool isFill)
    {
        GameObject group = new GameObject(groupName, typeof(RectTransform));
        group.transform.SetParent(parent, false);

        CreateSegment(group.transform, "Left", isFill);
        CreateSegment(group.transform, "Mid", isFill);
        CreateSegment(group.transform, "Right", isFill);
    }

    private Image CreateSegment(Transform parent, string name, bool isFill)
    {
        GameObject segment = new GameObject(name, typeof(RectTransform), typeof(Image));
        segment.transform.SetParent(parent, false);
        Image image = segment.GetComponent<Image>();
        image.raycastTarget = false;

        string prefix = isFill ? "barGreen" : "barBack";
        string suffix = name switch
        {
            "Left" => "_horizontalLeft",
            "Right" => "_horizontalRight",
            _ => "_horizontalMid",
        };

        image.sprite = LoadSprite(prefix + suffix);
        image.type = Image.Type.Simple;
        image.color = isFill ? fillColor : Color.white;
        return image;
    }

    private void CacheParts()
    {
        if (rootRect == null)
            return;

        if (levelText == null)
            levelText = rootRect.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
        if (expText == null)
            expText = rootRect.Find("ExpText")?.GetComponent<TextMeshProUGUI>();
        if (fillMaskRect == null)
            fillMaskRect = rootRect.Find("FillMask") as RectTransform;
    }

    private void ApplyLayout()
    {
        if (rootRect == null)
            return;

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(barSize.x, barSize.y + 28f);

        RectTransform levelRect = levelText.rectTransform;
        levelRect.anchorMin = new Vector2(0f, 1f);
        levelRect.anchorMax = new Vector2(0.5f, 1f);
        levelRect.pivot = new Vector2(0f, 1f);
        levelRect.anchoredPosition = new Vector2(0f, 0f);
        levelRect.sizeDelta = new Vector2(0f, 24f);

        RectTransform expRect = expText.rectTransform;
        expRect.anchorMin = new Vector2(0.5f, 1f);
        expRect.anchorMax = new Vector2(1f, 1f);
        expRect.pivot = new Vector2(1f, 1f);
        expRect.anchoredPosition = new Vector2(0f, 0f);
        expRect.sizeDelta = new Vector2(0f, 24f);

        LayoutBarGroup(rootRect.Find("Back") as RectTransform, barSize, -28f);
        if (fillMaskRect != null)
        {
            fillMaskRect.anchorMin = new Vector2(0f, 0f);
            fillMaskRect.anchorMax = new Vector2(0f, 0f);
            fillMaskRect.pivot = new Vector2(0f, 0f);
            fillMaskRect.anchoredPosition = new Vector2(0f, 0f);
            fillMaskRect.sizeDelta = new Vector2(barSize.x, barSize.y);
            LayoutBarGroup(fillMaskRect.Find("Fill") as RectTransform, barSize, 0f);
        }
    }

    private void LayoutBarGroup(RectTransform groupRect, Vector2 size, float y)
    {
        if (groupRect == null)
            return;

        groupRect.anchorMin = new Vector2(0f, 0f);
        groupRect.anchorMax = new Vector2(0f, 0f);
        groupRect.pivot = new Vector2(0f, 0f);
        groupRect.anchoredPosition = new Vector2(0f, y);
        groupRect.sizeDelta = size;

        LayoutSegment(groupRect.Find("Left") as RectTransform, 0f, capWidth, size.y);
        LayoutSegment(groupRect.Find("Mid") as RectTransform, capWidth, Mathf.Max(0f, size.x - capWidth * 2f), size.y);
        LayoutSegment(groupRect.Find("Right") as RectTransform, size.x - capWidth, capWidth, size.y);
    }

    private static void LayoutSegment(RectTransform rect, float x, float width, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void UpdateImmediate()
    {
        displayedProgress01 = player != null ? player.ExperienceProgress01 : 0f;
        UpdateFill(displayedProgress01);
        UpdateLabels();
    }

    private void UpdateFill(float progress01)
    {
        if (fillMaskRect == null)
            return;

        fillMaskRect.sizeDelta = new Vector2(barSize.x * Mathf.Clamp01(progress01), barSize.y);
    }

    private void UpdateLabels()
    {
        if (player == null)
            return;

        if (levelText != null)
            levelText.text = "Lv." + player.Level;

        if (expText != null)
            expText.text = player.Experience + " / " + player.ExperienceToNextLevel;
    }

    private static void ConfigureLabel(TextMeshProUGUI label, TextAlignmentOptions alignment, float fontSize)
    {
        label.alignment = alignment;
        label.color = new Color(0.95f, 0.92f, 0.78f, 1f);
        ChineseUITmpFont.Apply(label, fontSize, FontStyles.Bold);
    }

    private static Sprite LoadSprite(string resourceName)
    {
        Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + resourceName);
        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnValidate()
    {
        barSize.x = Mathf.Max(120f, barSize.x);
        barSize.y = Mathf.Max(10f, barSize.y);
        capWidth = Mathf.Clamp(capWidth, 1f, barSize.x * 0.45f);
    }
}
