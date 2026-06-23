using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class PlayerStatPanel : MonoBehaviour
{
    private const string RootName = "PlayerStatPanelRoot";
    private const float InventoryRightMargin = 36f;
    private const float InventoryWidth = 520f;

    [Header("Target")]
    [SerializeField] private Player player;

    [Header("Sprites")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite rowSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(320f, 520f);
    [SerializeField] private float gapFromInventory = 18f;
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField] private float padding = 26f;
    [SerializeField] private float rowHeight = 58f;
    [SerializeField] private float rowSpacing = 10f;

    [Header("Display")]
    [SerializeField] private bool followInventoryVisibility = true;

    private RectTransform rootRect;
    private GameObject inventoryPanel;
    private StatRow hpRow;
    private StatRow levelRow;
    private StatRow expRow;
    private StatRow attackRow;
    private StatRow damageBonusRow;
    private StatRow critRow;
    private StatRow critDamageRow;
    private StatRow lifeStealRow;
    private StatRow armorRow;
    private StatRow resistRow;

    private static readonly Color PanelFallbackColor = new Color(0.25f, 0.16f, 0.10f, 0.96f);
    private static readonly Color RowFallbackColor = new Color(0.78f, 0.61f, 0.42f, 1f);
    private static readonly Color PrimaryTextColor = new Color(0.18f, 0.10f, 0.045f, 1f);
    private static readonly Color MutedTextColor = new Color(0.33f, 0.20f, 0.10f, 1f);

    private void Start()
    {
        ResolvePlayer();
        BuildPanel();
        RefreshValues();
    }

    private void LateUpdate()
    {
        if (player == null || rootRect == null)
            return;

        UpdateVisibility();
        RefreshValues();
    }

    public void ApplyInventoryStyle(Sprite inventoryPanelSprite, Sprite inventoryRowSprite)
    {
        panelSprite = inventoryPanelSprite;
        rowSprite = inventoryRowSprite;

        if (rootRect != null)
        {
            BuildPanel();
            RefreshValues();
        }
    }

    private void ResolvePlayer()
    {
        if (player == null)
            player = FindObjectOfType<Player>();
    }

    private void BuildPanel()
    {
        Transform oldRoot = transform.Find(RootName);
        if (oldRoot != null)
        {
            if (Application.isPlaying)
                Destroy(oldRoot.gameObject);
            else
                DestroyImmediate(oldRoot.gameObject);
        }

        GameObject rootObject = CreateChild(gameObject, RootName, typeof(RectTransform), typeof(Image));
        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0.5f);
        rootRect.anchorMax = new Vector2(1f, 0.5f);
        rootRect.pivot = new Vector2(1f, 0.5f);
        rootRect.anchoredPosition = new Vector2(-(InventoryRightMargin + InventoryWidth + gapFromInventory), verticalOffset);
        rootRect.sizeDelta = panelSize;

        ConfigureImage(rootObject.GetComponent<Image>(), panelSprite, Image.Type.Sliced, PanelFallbackColor);

        TextMeshProUGUI title = CreateText(rootObject, "Title", "Player", 30f, TextAlignmentOptions.Left);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(padding, -22f);
        titleRect.sizeDelta = new Vector2(-padding * 2f, 42f);
        title.color = PrimaryTextColor;
        title.fontStyle = FontStyles.Bold;

        Image rule = CreateImage(rootObject, "TitleRule", new Color(0.30f, 0.17f, 0.08f, 0.35f));
        RectTransform ruleRect = rule.rectTransform;
        ruleRect.anchorMin = new Vector2(0f, 1f);
        ruleRect.anchorMax = new Vector2(1f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.anchoredPosition = new Vector2(0f, -72f);
        ruleRect.sizeDelta = new Vector2(-padding * 2f, 2f);

        float y = -88f;
        levelRow = CreateStatRow(rootObject, "LevelRow", "Level", ref y);
        expRow = CreateStatRow(rootObject, "ExpRow", "EXP", ref y);
        hpRow = CreateStatRow(rootObject, "HealthRow", "HP", ref y);
        attackRow = CreateStatRow(rootObject, "AttackRow", "ATK", ref y);
        damageBonusRow = CreateStatRow(rootObject, "DamageBonusRow", "DMG+", ref y);
        critRow = CreateStatRow(rootObject, "CritRow", "Crit", ref y);
        critDamageRow = CreateStatRow(rootObject, "CritDamageRow", "Crit DMG", ref y);
        lifeStealRow = CreateStatRow(rootObject, "LifeStealRow", "Lifesteal", ref y);
        armorRow = CreateStatRow(rootObject, "ArmorRow", "Armor", ref y);
        resistRow = CreateStatRow(rootObject, "ResistRow", "Resist", ref y);

        UpdateVisibility();
    }

    private StatRow CreateStatRow(GameObject parent, string name, string label, ref float y)
    {
        GameObject rowObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, y);
        rowRect.sizeDelta = new Vector2(panelSize.x - padding * 2f, rowHeight);
        ConfigureImage(rowObject.GetComponent<Image>(), rowSprite, Image.Type.Sliced, RowFallbackColor);

        TextMeshProUGUI labelText = CreateText(rowObject, "Label", label, 19f, TextAlignmentOptions.Left);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0.55f, 1f);
        labelRect.offsetMin = new Vector2(22f, 0f);
        labelRect.offsetMax = Vector2.zero;
        labelText.color = MutedTextColor;
        labelText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI valueText = CreateText(rowObject, "Value", "0", 24f, TextAlignmentOptions.Right);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = new Vector2(0.45f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = new Vector2(-22f, 0f);
        valueText.color = PrimaryTextColor;
        valueText.fontStyle = FontStyles.Bold;

        y -= rowHeight + rowSpacing;

        return new StatRow
        {
            ValueText = valueText
        };
    }

    private void UpdateVisibility()
    {
        if (!followInventoryVisibility || rootRect == null)
            return;

        if (inventoryPanel == null)
        {
            Transform panel = transform.Find("InventoryUIRoot/InventoryPanel");
            inventoryPanel = panel != null ? panel.gameObject : null;
        }

        if (inventoryPanel != null)
            rootRect.gameObject.SetActive(inventoryPanel.activeSelf);
    }

    private void RefreshValues()
    {
        if (player == null)
            return;

        SetValue(levelRow, "Lv " + player.Level);
        SetValue(expRow, player.Experience + "/" + player.ExperienceToNextLevel);
        SetValue(hpRow, player.CurrentHp + "/" + player.MaxHp);
        SetValue(attackRow, player.AttackPower.ToString());
        SetValue(damageBonusRow, player.DamageBonusPercent.ToString("0.#") + "%");
        SetValue(critRow, (player.CritChance * 100f).ToString("0.#") + "%");
        SetValue(critDamageRow, (player.CritDamageMultiplier * 100f).ToString("0.#") + "%");
        SetValue(lifeStealRow, (player.LifeStealPercent * 100f).ToString("0.#") + "%");
        SetValue(armorRow, player.Armor.ToString());
        SetValue(resistRow, player.MagicResistance.ToString());
    }

    private static void SetValue(StatRow row, string value)
    {
        if (row != null && row.ValueText != null)
            row.ValueText.text = value;
    }

    private static void ConfigureImage(Image image, Sprite sprite, Image.Type type, Color fallbackColor)
    {
        image.sprite = sprite;
        image.type = sprite != null ? type : Image.Type.Simple;
        image.color = sprite != null ? Color.white : fallbackColor;
        image.raycastTarget = false;
    }

    private static Image CreateImage(GameObject parent, string name, Color color)
    {
        GameObject imageObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image));
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(GameObject parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject CreateChild(GameObject parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private void OnValidate()
    {
        panelSize.x = Mathf.Max(260f, panelSize.x);
        panelSize.y = Mathf.Max(260f, panelSize.y);
        gapFromInventory = Mathf.Max(0f, gapFromInventory);
        padding = Mathf.Clamp(padding, 12f, 48f);
        rowHeight = Mathf.Max(44f, rowHeight);
        rowSpacing = Mathf.Max(0f, rowSpacing);
    }

    private sealed class StatRow
    {
        public TextMeshProUGUI ValueText;
    }
}
