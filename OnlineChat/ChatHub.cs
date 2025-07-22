using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using OnlineChat.Services;

namespace OnlineChat;

public class ChatHub : Hub
{
    private readonly RoomsService _roomsService;
    private ILogger<ChatHub> _logger;

    public ChatHub(RoomsService roomsService, ILogger<ChatHub> logger)
    {
        _roomsService = roomsService;
        _logger = logger;
    }
    
    private static readonly ConcurrentDictionary<string, List<string>> UserRooms = new();

    public async Task<object> JoinRoom(string roomId, string userName, string roomName)
    {
        var room = _roomsService.GetOrCreate(roomId, roomName);
        
        if (!UserRooms.TryGetValue(Context.ConnectionId, out var listRooms))
            throw new HubException("you aren't user");
        
        if (!room.TryAddUser(Context.ConnectionId, userName))
            throw new HubException("name reserved");

        listRooms.Add(roomId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync("UserJoined", userName);
        await UpdateUsersList(roomId);

        return RoomMapper.ChatRoomToCreationResult(room);
    }

    public async Task LeaveRoom(string roomId)
    {
        if (_roomsService.TryGetRoom(roomId, out var room) && 
            room.Users.TryGetValue(Context.ConnectionId, out var userName))
        {
            room.RemoveUser(Context.ConnectionId);
            
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).SendAsync("UserLeft", userName);
            await UpdateUsersList(roomId);
            
            if (room.Users.Count == 0)
                _roomsService.RemoveRoom(roomId);
        }
    }
    
    public override Task OnConnectedAsync()
    {
        UserRooms.TryAdd(Context.ConnectionId, new List<string>());
        return base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (UserRooms.TryGetValue(Context.ConnectionId, out var roomsIds))
        {
            foreach (var roomId in roomsIds)
            {
                await LeaveRoom(roomId);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
    
    public async Task SendMessage(string roomId, string message)
    {
        if (_roomsService.TryGetRoom(roomId, out var room) && 
            room.Users.TryGetValue(Context.ConnectionId, out var userName))
        {
            await Clients.Group(roomId).SendAsync("ReceiveMessage", userName, message);
        }
    }

    private async Task UpdateUsersList(string roomId)
    {
        if (_roomsService.TryGetRoom(roomId, out var room))
        {
            await Clients.Group(roomId).SendAsync("UpdateUsers", room.UsersNames.ToList());
        }
    }
    
    public async IAsyncEnumerable<int> StreamFile(
        string roomId,
        string fileId,  
        string fileName,
        IAsyncEnumerable<byte[]> fileStream)
    {
        _logger.LogInformation("StreamFile");
        if (!_roomsService.TryGetRoom(roomId, out var room) ||
            !room.Users.TryGetValue(Context.ConnectionId, out var userName)) yield break;
        
        await Clients.OthersInGroup(roomId)
            .SendAsync("ReceiveFileStart", userName, fileId, fileName);

        await foreach (var chunk in fileStream)
        {
            await Clients.OthersInGroup(roomId)
                .SendAsync("ReceiveFileChunk", fileId, chunk);
            yield return chunk.Length;
        }

        await Clients.OthersInGroup(roomId)
            .SendAsync("ReceiveFileEnd", fileId);
    }
}