using UnityEngine;

/// <summary>
/// 读档后连续几帧重写玩家坐标。CharacterController 刚启用时可能把人顶开，需要多贴几次。
/// </summary>
[DisallowMultipleComponent]
public sealed class SavePositionApplier : MonoBehaviour
{
    private Player player;
    private SaveData data;
    private int framesRemaining;

    /// <summary>在玩家物体上挂本组件，按帧把存档坐标再套一遍。</summary>
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

        // 存档里没有镜头角时，最后一帧再把相机贴到玩家身上。
        if (data.camera == null || !data.camera.hasLook)
            SaveLoadService.SnapCameraToPlayer();
        Destroy(this);
    }
}
