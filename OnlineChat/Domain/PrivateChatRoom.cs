namespace OnlineChat;

public class PrivateChatRoom : ChatRoom
{
    public bool IsLocked { get; set; } = false;

    public Dictionary<string, string> WantJoinUser { get; set; } = new();

    public bool CheckContainsUser(string UserId)
    {
        return Users.ContainsKey(UserId);
    }
}