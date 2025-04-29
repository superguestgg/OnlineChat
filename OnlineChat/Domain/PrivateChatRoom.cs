namespace OnlineChat.Domain;

public class PrivateChatRoom : ChatRoom
{
    /// <summary>
    ///     Комната блокируется для начала обмена ключами
    /// </summary>
    public SemaphoreSlim IsLocked { get; set; } = new SemaphoreSlim(1);
    
    /// <summary>
    ///      Круг для обмена ключами
    /// </summary>
    public Dictionary<string, string> Circle { get; set; } = null;
    
    /// <summary>
    ///     еще не закончили обмен ключами
    /// </summary>
    public HashSet<string> UsersNotFinished { get; set; } = null;

    /// <summary>
    ///     От них есть заявки на вступление в чат
    /// </summary>
    public Dictionary<string, string> WantJoinUser { get; set; } = new();
}