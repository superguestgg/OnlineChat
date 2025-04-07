using System.Collections.Concurrent;

namespace OnlineChat;

public class RoomsService
{
    private readonly ILogger<RoomsService> _logger;
    private readonly ConcurrentDictionary<string, ChatRoom> _rooms = new();

    public RoomsService(ILogger<RoomsService> logger)
    {
        _logger = logger;
    }

    public ChatRoom GetOrCreate(string roomId, string roomName)
    {
        if (_rooms.TryGetValue(roomId, out var room)) 
            return room;

        room = new ChatRoom
        {
            Id = roomId,
            Name = $"{(string.IsNullOrEmpty(roomName) ? roomId.Substring(0, 4) : roomName)}"
        };
        _rooms.TryAdd(roomId, room);

        return room;
    }

    public bool TryGetRoom(string roomId, out ChatRoom room)
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

    public IEnumerable<ChatRoom> GetAllRooms()
    {
        return _rooms.Select(kv => kv.Value);
    }
}