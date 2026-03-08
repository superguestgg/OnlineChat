using Microsoft.AspNetCore.SignalR;
using OnlineChat.DTO;
using OnlineChat.Mapping;
using OnlineChat.Services;

namespace OnlineChat.Hubs;

public class RoomHub(RoomsService roomsService) : Hub
{
    /// <summary>
    ///     Получить все комнаты с количеством пользователей
    /// </summary>
    public IEnumerable<RoomInfo> GetAllRooms()
    {
        return roomsService.GetAllRooms().Select(RoomMapper.ChatRoomToRoomInfo).ToList();
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
