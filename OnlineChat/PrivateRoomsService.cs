using System.Collections.Concurrent;

namespace OnlineChat;

public class PrivateRoomsService
{
    private readonly ILogger<PrivateRoomsService> _logger;
    private readonly ConcurrentDictionary<string, PrivateChatRoom> _rooms = new();

    public PrivateRoomsService(ILogger<PrivateRoomsService> logger)
    {
        _logger = logger;
    }

    public PrivateChatRoom GetOrCreate(string roomId, string? name)
    {
        if (_rooms.TryGetValue(roomId, out var room)) 
            return room;
        // Если комнаты нет, создаем новую
        room = new PrivateChatRoom
        {
            Id = roomId,
            Name = $"{(string.IsNullOrEmpty(name) ? roomId.Substring(0, 4) : name)}"
        };
        _rooms.TryAdd(roomId, room);

        return room;
    }

    public bool TryGetValue(string roomId, out PrivateChatRoom room)
    {
        return _rooms.TryGetValue(roomId, out room);
    }

    public void RemoveRoom(string roomId)
    {
        if (!_rooms.TryRemove(roomId, out _))
        {
            _logger.LogError("RemoveRoom {roomId}", roomId);
        }
    }

    public PrivateChatRoom[] GetAllRooms()
    {
        return _rooms.Select(kv => kv.Value).ToArray();
    }
}