using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using OnlineChat.Domain;
using OnlineChat.Services;

namespace OnlineChat;

public class PrivateChatHub : Hub
{
    private readonly PrivateRoomsService _roomsService;
    private ILogger<PrivateChatHub> _logger;

    public PrivateChatHub(PrivateRoomsService roomsService, ILogger<PrivateChatHub> logger)
    {
        _roomsService = roomsService;
        _logger = logger;
    }
    
    private static readonly ConcurrentDictionary<string, List<string>> UserRooms = new();

    public async Task<object> JoinRoom(string roomId, string userName, string roomName)
    {
        var room = _roomsService.GetOrCreate(roomId, roomName);

        return room.UsersNames.Count == 0
            ? JoinEmptyRoom(roomId, userName, room)
            : JoinNotEmptyRoom(roomId, userName, room);
    }

    public async Task AcceptUserToRoom(string roomId, string userId, Dictionary<string, string> keys)
    {
        var room = _roomsService.GetOrCreate(roomId, "not matter");
        
        if (!room.WantJoinUser.TryGetValue(userId, out var userName))
            return;
        room.WantJoinUser.Remove(userId);
        

        if (!UserRooms.TryGetValue(userId, out var listRooms))
            throw new HubException("he aren't user");
        if (!room.TryAddUser(userId, userName))
            throw new HubException("his name reserved");

        listRooms.Add(roomId);

        await Clients.Group(roomId).SendAsync("UserAccepted", room.Users[Context.ConnectionId], userName);
        await Clients.Client(userId).SendAsync("Accepted");
        
        await Groups.AddToGroupAsync(userId, roomId);
        await Clients.Group(roomId).SendAsync("UserJoined", userName);
        await UpdateUsersList(roomId);

        await StartKeyCreation(room, roomId, keys);
    }

    private async Task StartKeyCreation(PrivateChatRoom room, string roomId, Dictionary<string, string> keys)
    {
        var userIds = room.Users.Keys.ToArray();
        var circle = new Dictionary<string, string>();
        for (var i = 0; i < userIds.Length; i++)
        {
            circle.Add(userIds[i], userIds[(i + 1) % userIds.Length]);
        }

        await room.SemaphoreForKeyExchange.WaitAsync();
        room.Circle = circle;
        room.UsersNotFinished = room.Users.Select(k => k.Key).ToHashSet();

        await Clients.Group(roomId).SendAsync("StartCreatingToken", keys["p"]);
    }
    
    public class MyClass
    {
        public string p;
        public string g;
        public string publicKey;
    }

    public async Task SendPublicKey(string roomId, string publicKey, int iteration = 0)
    {
        var room = _roomsService.GetOrCreate(roomId, "not matter");

        if (iteration <= room.Circle.Count)
        {
            var nextUserId = room.Circle[Context.ConnectionId];
            await Clients.Client(nextUserId).SendAsync("ReceiveIterationKey", publicKey, iteration, roomId);
        }
    }
    
    public async Task IterationsEnded(string roomId)
    {
        var room = _roomsService.GetOrCreate(roomId, "not matter");
        room.UsersNotFinished.Remove(Context.ConnectionId);
        if (room.UsersNotFinished.Count == 0)
        {
            room.UsersNotFinished = null;
            room.SemaphoreForKeyExchange.Release();
            room.Circle = null;
            await Clients.Group(roomId).SendAsync("KeyExchangeCompleted");
        }
    }
    
    public async Task Decline(string roomId, string userId)
    {
        var room = _roomsService.GetOrCreate(roomId, "not matter");
        if (!room.CheckContainsUser(Context.ConnectionId))
            throw new HubException("!room.CheckContainsUser");
        if (!room.WantJoinUser.TryGetValue(userId, out var userName))
            return;
        room.WantJoinUser.Remove(userId);

        await Clients.Group(roomId).SendAsync("UserDeclined", Context.ConnectionId, userName, "sosal");
        await Clients.Client(userId).SendAsync("UserDeclined", "sosal");
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
    
    public async Task SendEncryptedMessage(string roomId, string encryptedMessage)
    {
        if (_roomsService.TryGetRoom(roomId, out var room) && 
            room.Users.TryGetValue(Context.ConnectionId, out var userName))
        {
            Console.WriteLine(encryptedMessage);
            await Clients.Group(roomId)
                .SendAsync("ReceiveEncryptedMessage", userName, encryptedMessage);
        }
    }
    
    private async Task<object> JoinEmptyRoom(string roomId, string userName, ChatRoom room)
    {
        if (!UserRooms.TryGetValue(Context.ConnectionId, out var listRooms))
            throw new HubException("you aren't hub user");
        
        if (!room.TryAddUser(Context.ConnectionId, userName))
            throw new HubException("name reserved");

        listRooms.Add(roomId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync("UserJoined", userName);
        await UpdateUsersList(roomId);

        return RoomMapper.ChatRoomToCreationResult(room);
    }
    
    private async Task<object> JoinNotEmptyRoom(string roomId, string userName, PrivateChatRoom room)
    {
        room.WantJoinUser.Add(Context.ConnectionId, userName);

        await Clients.Group(roomId).SendAsync("UserWantJoin", Context.ConnectionId, userName);
        
        return RoomMapper.ChatRoomToCreationResult(room);
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