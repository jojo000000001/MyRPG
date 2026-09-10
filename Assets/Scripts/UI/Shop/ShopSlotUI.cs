using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 商店格子，结构和背包 Slot 一样，点击后选中货物。
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopSlotUI : MonoBehaviour, IPointerClickHandler
{
    private ShopUI owner;
    private int slotIndex;

    public void Configure(ShopUI owner, int slotIndex)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null)
            owner.SelectVisibleSlot(slotIndex);
    }
}
