using System.Collections.Concurrent;
using OnlineChat.Domain;

namespace OnlineChat.Services;

public abstract class BaseRoomsService<TRoom> where TRoom : ChatRoom
{
    protected readonly ILogger<BaseRoomsService<TRoom>> _logger;
    protected readonly ConcurrentDictionary<string, TRoom> _rooms = new();

    protected BaseRoomsService(ILogger<BaseRoomsService<TRoom>> logger)
    {
        _logger = logger;
    }

    public TRoom GetOrCreate(string roomId, string roomName)
    {
        if (_rooms.TryGetValue(roomId, out var room)) 
            return room;

        room = CreateEmptyRoom(roomId, roomName);
        _rooms.TryAdd(roomId, room);

        return room;
    }

    protected virtual TRoom CreateEmptyRoom(string roomId, string roomName)
    {
        return (TRoom)new ChatRoom
        {
            Id = roomId,
            Name = $"{(string.IsNullOrEmpty(roomName) ? roomId.Substring(0, 4) : roomName)}"
        };
    }

    public bool TryGetRoom(string roomId, out TRoom room)
    {
        return _rooms.TryGetValue(roomId, out room);
    }

    public void RemoveRoom(string roomId)
    {
        if (!_rooms.TryRemove(roomId, out _))
        {
            _logger.LogError("Error RemoveRoom {roomId}", roomId);
        }
    }

    public IEnumerable<TRoom> GetAllRooms()
    {
        return _rooms.Select(kv => kv.Value);
    }
}