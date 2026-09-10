using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PlayerHotbarSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private PlayerHotbar owner;
    private int slotIndex;

    public void Configure(PlayerHotbar owner, int slotIndex)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null || eventData == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
            owner.HandleSlotClick(slotIndex);
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                owner.HandleSlotClear(slotIndex);
            else
                owner.HandleSlotClick(slotIndex);
        }
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
