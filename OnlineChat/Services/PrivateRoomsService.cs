using System.Collections.Concurrent;
using OnlineChat.Domain;

namespace OnlineChat.Services;

public class PrivateRoomsService : BaseRoomsService<PrivateChatRoom>
{
    public PrivateRoomsService(ILogger<PrivateRoomsService> logger) : base(logger)
    {
    }
    
    protected override PrivateChatRoom CreateEmptyRoom(string roomId, string roomName)
    {
        return new PrivateChatRoom
        {
            Id = roomId,
            Name = $"{(string.IsNullOrEmpty(roomName) ? roomId.Substring(0, 4) : roomName)}"
        };
    }
}