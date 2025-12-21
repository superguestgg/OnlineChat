using OnlineChat.Domain;

namespace OnlineChat.Services;

public class UltraPrivateRoomsService : BaseRoomsService<UltraPrivateChatRoom>
{
    public UltraPrivateRoomsService(ILogger<UltraPrivateRoomsService> logger) : base(logger)
    {
    }
    
    protected override UltraPrivateChatRoom CreateEmptyRoom(string roomId, string roomName)
    {
        return new UltraPrivateChatRoom
        {
            Id = roomId,
            Name = $"{(string.IsNullOrEmpty(roomName) ? roomId.Substring(0, 4) : roomName)}"
        };
    }
}