/// <summary>
/// 跨场景保存当前登录玩家信息（本地 Demo，无网络验证）。
/// </summary>
public static class GameSession
{
    public static string Username { get; private set; }

    public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(Username);

    public static void SetUsername(string username)
    {
        Username = username?.Trim();
    }

    public static void Clear()
    {
        Username = null;
    }
}
