using Microsoft.AspNetCore.SignalR;
using OnlineChat.DTO;
using OnlineChat.Services;

namespace OnlineChat;

public class RoomHub : Hub
{
    private readonly RoomsService _roomsService;

    public RoomHub(RoomsService roomsService)
    {
        _roomsService = roomsService;
    }
    
    /// <summary>
    ///     Получить все комнаты с количеством пользователей
    /// </summary>
    public IEnumerable<RoomInfo> GetAllRooms()
    {
        return _roomsService.GetAllRooms().Select(RoomMapper.ChatRoomToRoomInfo).ToList();
    }

    /// <summary>
    ///     Создать новую комнату
    /// </summary>
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
