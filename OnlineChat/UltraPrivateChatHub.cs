using Microsoft.AspNetCore.SignalR;
using OnlineChat.Services;

namespace OnlineChat;

public class UltraPrivateChatHub : Hub
{
    private readonly UltraPrivateRoomsService _roomsService;
    
    public UltraPrivateChatHub(UltraPrivateRoomsService roomsService)
    {
        _roomsService = roomsService;
    }
}