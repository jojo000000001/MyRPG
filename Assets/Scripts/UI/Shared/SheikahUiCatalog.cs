using UnityEngine;

/// <summary>
/// Resources 目录下的希卡 UI 贴图引用，避免 Resources.Load&lt;Sprite&gt; 对 UI 子目录失效。
/// </summary>
[CreateAssetMenu(menuName = "MyRPG/Sheikah UI Catalog", fileName = "SheikahUiCatalog")]
public sealed class SheikahUiCatalog : ScriptableObject
{
    public Sprite panelSprite;
    public Sprite slotSprite;
}
