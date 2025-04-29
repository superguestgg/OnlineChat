
using OnlineChat.Domain;

namespace OnlineChat.Services;

public class RoomsService : BaseRoomsService<ChatRoom>
{
    public RoomsService(ILogger<RoomsService> logger) : base(logger)
    {
    }
}