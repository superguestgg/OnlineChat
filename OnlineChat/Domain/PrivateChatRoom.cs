namespace OnlineChat.Domain;

public class PrivateChatRoom : ChatRoom
{
    /// <summary>
    ///     Комната блокируется на время обмена ключами
    /// </summary>
    public SemaphoreSlim SemaphoreForKeyExchange { get; set; } = new SemaphoreSlim(1);
    
    /// <summary>
    ///      Круг для обмена ключами
    ///      Id to Id
    /// </summary>
    public Dictionary<string, string> Circle { get; set; } = null;
    
    /// <summary>
    ///     Еще не закончили обмен ключами
    ///     Ids
    /// </summary>
    public HashSet<string> UsersNotFinished { get; set; } = null;

    /// <summary>
    ///     От них есть заявки на вступление в чат
    ///     Id to Name
    /// </summary>
    public Dictionary<string, string> WantJoinUser { get; set; } = new();
}