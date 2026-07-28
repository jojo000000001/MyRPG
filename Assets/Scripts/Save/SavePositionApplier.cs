using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies saved player transform over several frames to survive CharacterController startup.
/// </summary>
[DisallowMultipleComponent]
public sealed class SavePositionApplier : MonoBehaviour
{
    private Player player;
    private SaveData data;
    private int framesRemaining;

    public static void Schedule(Player targetPlayer, SaveData saveData, int frames = 5)
    {
        if (targetPlayer == null || saveData == null)
            return;

        SavePositionApplier applier = targetPlayer.GetComponent<SavePositionApplier>();
        if (applier == null)
            applier = targetPlayer.gameObject.AddComponent<SavePositionApplier>();

        applier.Begin(targetPlayer, saveData, frames);
    }

    private void Begin(Player targetPlayer, SaveData saveData, int frames)
    {
        player = targetPlayer;
        data = saveData;
        framesRemaining = Mathf.Max(1, frames);
        enabled = true;
    }

    private void LateUpdate()
    {
        if (player == null || data == null)
        {
            Destroy(this);
            return;
        }

        SaveSystem.ApplyPlayerTransform(player, data);
        framesRemaining--;

        if (framesRemaining > 0)
            return;

        SaveLoadService.SnapCameraToPlayer();
        Destroy(this);
    }
}
