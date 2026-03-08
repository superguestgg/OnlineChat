using Microsoft.AspNetCore.SignalR;
using OnlineChat.Services;

namespace OnlineChat.Hubs;

public class UltraPrivateChatHub(UltraPrivateRoomsService roomsService) : Hub
{
}