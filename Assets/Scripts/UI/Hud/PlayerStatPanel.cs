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
    [SerializeField] private Vector2 panelSize = new Vector2(400f, 560f);
    [SerializeField] private float gapFromInventory = 18f;
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField] private float padding = 32f;
    [SerializeField] private float rowHeight = 40f;
    [SerializeField] private float rowSpacing = 4f;
    [SerializeField] private float headerHeight = 72f;
    [SerializeField] private float bottomPadding = 20f;

    [Header("Typography")]
    [SerializeField] private int titleFontSize = 24;
    [SerializeField] private int labelFontSize = 14;
    [SerializeField] private int valueFontSize = 15;

    [Header("Display")]
    [SerializeField] private bool followInventoryVisibility = true;

    private RectTransform rootRect;
    private GameObject inventoryPanel;
    private StatRow hpRow;
    private StatRow shieldRow;
    private StatRow energyRow;
    private StatRow mentalRow;
    private StatRow levelRow;
    private StatRow expRow;
    private StatRow attackRow;
    private StatRow damageBonusRow;
    private StatRow critRow;
    private StatRow critDamageRow;
    private StatRow lifeStealRow;
    private StatRow armorRow;
    private StatRow resistRow;

    private const int StatRowCount = 13;

    private static readonly Color PanelFallbackColor = new Color(0.25f, 0.16f, 0.10f, 0.96f);
    private static readonly Color RowFallbackColor = new Color(0.28f, 0.16f, 0.08f, 1f);
    private static readonly Color TitleTextColor = new Color(0.18f, 0.10f, 0.045f, 1f);
    private static readonly Color RowLabelColor = new Color(0.86f, 0.74f, 0.52f, 1f);
    private static readonly Color RowValueColor = new Color(0.96f, 0.90f, 0.74f, 1f);
    private static readonly Color TitleRuleColor = new Color(0.45f, 0.30f, 0.12f, 0.55f);

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

    public void BindPlayer(Player target)
    {
        if (target != null)
            player = target;
    }

    private void ResolvePlayer()
    {
        if (player == null)
            player = Player.Resolve();
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
        panelSize.y = CalculatePanelHeight();
        rootRect.sizeDelta = panelSize;

        ConfigureImage(rootObject.GetComponent<Image>(), panelSprite, Image.Type.Sliced, PanelFallbackColor);

        Text title = CreateText(rootObject, "Title", "角色属性", titleFontSize, TextAnchor.MiddleLeft, FontStyle.Bold);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(padding, -28f);
        titleRect.sizeDelta = new Vector2(-padding * 2f, 32f);
        title.color = TitleTextColor;

        Image rule = CreateImage(rootObject, "TitleRule", TitleRuleColor);
        RectTransform ruleRect = rule.rectTransform;
        ruleRect.anchorMin = new Vector2(0f, 1f);
        ruleRect.anchorMax = new Vector2(1f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.anchoredPosition = new Vector2(0f, -headerHeight + 8f);
        ruleRect.sizeDelta = new Vector2(-padding * 2f, 2f);

        float y = -headerHeight;
        levelRow = CreateStatRow(rootObject, "LevelRow", "等级", ref y);
        expRow = CreateStatRow(rootObject, "ExpRow", "经验", ref y);
        hpRow = CreateStatRow(rootObject, "HealthRow", "生命", ref y);
        shieldRow = CreateStatRow(rootObject, "ShieldRow", "盾牌耐久", ref y);
        energyRow = CreateStatRow(rootObject, "EnergyRow", "能量", ref y);
        mentalRow = CreateStatRow(rootObject, "MentalRow", "精神", ref y);
        attackRow = CreateStatRow(rootObject, "AttackRow", "攻击", ref y);
        damageBonusRow = CreateStatRow(rootObject, "DamageBonusRow", "增伤", ref y);
        critRow = CreateStatRow(rootObject, "CritRow", "暴击率", ref y);
        critDamageRow = CreateStatRow(rootObject, "CritDamageRow", "暴击伤害", ref y);
        lifeStealRow = CreateStatRow(rootObject, "LifeStealRow", "吸血", ref y);
        armorRow = CreateStatRow(rootObject, "ArmorRow", "护甲", ref y);
        resistRow = CreateStatRow(rootObject, "ResistRow", "魔抗", ref y);

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

        Text labelText = CreateText(rowObject, "Label", label, labelFontSize, TextAnchor.MiddleLeft, FontStyle.Bold);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0.45f, 1f);
        labelRect.offsetMin = new Vector2(18f, 0f);
        labelRect.offsetMax = Vector2.zero;
        labelText.color = RowLabelColor;

        Text valueText = CreateText(rowObject, "Value", "0", valueFontSize, TextAnchor.MiddleRight, FontStyle.Bold);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = new Vector2(0.45f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = new Vector2(-14f, 0f);
        valueText.color = RowValueColor;
        valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
        valueText.resizeTextForBestFit = true;
        valueText.resizeTextMinSize = 10;
        valueText.resizeTextMaxSize = valueFontSize;

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

        SetValue(levelRow, player.Level + " 级");
        SetValue(expRow, player.Experience + " / " + player.ExperienceToNextLevel);
        SetValue(hpRow, player.CurrentHp + " / " + player.MaxHp);
        SetValue(shieldRow, player.CurrentShieldDurability + " / " + player.MaxShieldDurability);
        SetValue(energyRow, player.CurrentEnergy + " / " + player.MaxEnergy);
        SetValue(mentalRow, player.CurrentMental + " / " + player.MaxMental);
        string attackText = player.AttackPower.ToString();
        if (player.AttackBuffAmount > 0)
            attackText += " +" + player.AttackBuffAmount;
        SetValue(attackRow, attackText);
        SetValue(damageBonusRow, player.DamageBonusPercent.ToString("0.#") + "%");
        SetValue(critRow, (player.CritChance * 100f).ToString("0.#") + "%");
        SetValue(critDamageRow, (player.CritDamageMultiplier * 100f).ToString("0.#") + "%");
        SetValue(lifeStealRow, (player.LifeStealPercent * 100f).ToString("0.#") + "%");
        SetValue(armorRow, player.Armor.ToString());
        SetValue(resistRow, player.MagicResistance.ToString());
    }

    private static void SetValue(StatRow row, string value)
    {
        if (row?.ValueText != null)
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

    private static Text CreateText(GameObject parent, string name, string text, int fontSize, TextAnchor alignment, FontStyle style)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(Text));
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        ChineseUIFont.Apply(label, fontSize, style);
        return label;
    }

    private static GameObject CreateChild(GameObject parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private float CalculatePanelHeight()
    {
        float rowsHeight = StatRowCount * rowHeight + Mathf.Max(0, StatRowCount - 1) * rowSpacing;
        return headerHeight + rowsHeight + bottomPadding;
    }

    private void OnValidate()
    {
        panelSize.x = Mathf.Max(260f, panelSize.x);
        panelSize.y = CalculatePanelHeight();
        gapFromInventory = Mathf.Max(0f, gapFromInventory);
        headerHeight = Mathf.Max(56f, headerHeight);
        bottomPadding = Mathf.Max(12f, bottomPadding);
        padding = Mathf.Clamp(padding, 12f, 48f);
        rowHeight = Mathf.Max(36f, rowHeight);
        rowSpacing = Mathf.Max(0f, rowSpacing);
        titleFontSize = Mathf.Max(12, titleFontSize);
        labelFontSize = Mathf.Max(10, labelFontSize);
        valueFontSize = Mathf.Max(10, valueFontSize);
    }

    private sealed class StatRow
    {
        public Text ValueText;
    }
}
