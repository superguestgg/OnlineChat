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
        return _roomsService._rooms.Select(r => new RoomInfo
        {
            Id = r.Value.Id,
            Name = r.Value.Name,
            UserCount = r.Value.UsersNames.Count
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

// DTO для передачи информации о комнате
public class RoomInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int UserCount { get; set; }
}

public class RoomCreationResult
{
    public string RoomId { get; set; }
    public string RoomName { get; set; }
}