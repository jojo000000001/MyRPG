using UnityEngine;
using UnityEngine.EventSystems;

public sealed class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    private InventoryUI owner;
    private int slotIndex;

    public void Configure(InventoryUI owner, int slotIndex)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner != null)
            owner.HandleSlotPointerDown(slotIndex, eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (owner != null)
            owner.HandleSlotPointerUp(slotIndex, eventData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
            owner.HandleSlotPointerEnter(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
            owner.HandleSlotPointerExit(slotIndex);
    }
}
