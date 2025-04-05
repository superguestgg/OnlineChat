using System.Collections.Concurrent;

namespace OnlineChat;

public class RoomsService
{
    private readonly ILogger<RoomsService> _logger;
    public readonly ConcurrentDictionary<string, ChatRoom> _rooms = new();

    public RoomsService(ILogger<RoomsService> logger)
    {
        _logger = logger;
    }

    public ChatRoom GetOrCreate(string roomId, string? name)
    {
        if (_rooms.TryGetValue(roomId, out var room)) 
            return room;
        // Если комнаты нет, создаем новую
        room = new ChatRoom
        {
            Id = roomId,
            Name = $"{name ?? roomId.Substring(0, 4)}"
        };
        _rooms.TryAdd(roomId, room);

        return room;
    }

    public bool TryGetValue(string roomId, out ChatRoom room)
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
}