namespace OnlineChat.Domain;

public class ChatRoom
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    /// <summary>
    ///     Id to Name
    /// </summary>
    public Dictionary<string, string> Users { get; set; } = new();
    public HashSet<string> UsersNames { get; set; } = new();

    public bool TryAddUser(string userId, string userName)
    {
        if (UsersNames.Contains(userName))
            return false;
        
        UsersNames.Add(userName);
        Users[userId] = userName;
        return true;
    }

    public void RemoveUser(string userId)
    {
        UsersNames.Remove(Users[userId]);
        Users.Remove(userId);
    }
    
    public bool CheckContainsUser(string userId)
    {
        return Users.ContainsKey(userId);
    }
}