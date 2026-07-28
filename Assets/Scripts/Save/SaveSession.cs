/// <summary>
/// Tracks which save slot is active and pending menu-driven load/new-game requests.
/// </summary>
public static class SaveSession
{
    public const int InvalidSlot = -1;

    public static int ActiveSlot { get; private set; } = InvalidSlot;

    public static bool HasValidActiveSlot => ActiveSlot >= 0 && ActiveSlot < SaveSystem.SlotCount;

    public static int? PendingLoadSlot { get; private set; }

    public static bool PendingNewGame { get; private set; }

    public static void BeginLoad(int slotIndex)
    {
        ActiveSlot = slotIndex;
        PendingLoadSlot = slotIndex;
        PendingNewGame = false;
    }

    public static void BeginNewGame(int slotIndex)
    {
        ActiveSlot = slotIndex;
        PendingLoadSlot = null;
        PendingNewGame = true;
    }

    public static void ClearPending()
    {
        PendingLoadSlot = null;
        PendingNewGame = false;
    }
}
