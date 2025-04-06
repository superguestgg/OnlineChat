using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace OnlineChat;

public class RoomHub : Hub
{
    private readonly RoomsService _roomsService;

    public RoomHub(RoomsService roomsService)
    {
        _roomsService = roomsService;
    }
    
    // Получить все комнаты с количеством пользователей
    public IEnumerable<RoomInfo> GetAllRooms()
    {
        return _roomsService.GetAllRooms().Select(r => new RoomInfo
        {
            Id = r.Id,
            Name = r.Name,
            UserCount = r.UsersNames.Count
        }).ToList();
    }

    // Создать новую комнату
    public async Task<RoomCreationResult> CreateRoom(string roomName, string creatorName)
    {
        var roomId = Guid.NewGuid().ToString();

        return new RoomCreationResult
        {
            RoomId = roomId,
            RoomName = roomName
        };
    }
}
