using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace OnlineChat;

public class PrivateChatHub : Hub
{
    private readonly PrivateRoomsService _roomsService;

    public PrivateChatHub(PrivateRoomsService roomsService)
    {
        _roomsService = roomsService;
    }
    
    private static readonly ConcurrentDictionary<string, List<string>> UserRooms = new();

    public async Task<object> JoinRoom(string roomId, string userName, string roomName)
    {
        var room = _roomsService.GetOrCreate(roomId, roomName);

        return room.UsersNames.Count == 0
            ? JoinEmptyRoom(roomId, userName, room)
            : JoinNotEmptyRoom(roomId, userName, room);
    }
    
    private async Task<object> JoinEmptyRoom(string roomId, string userName, ChatRoom room)
    {
        if (!UserRooms.TryGetValue(Context.ConnectionId, out var listRooms))
            throw new HubException("you aren't user");
        
        if (!room.TryAddUser(Context.ConnectionId, userName))
            throw new HubException("name reserved");

        listRooms.Add(roomId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync("UserJoined", userName);
        await UpdateUsersList(roomId);
        
        return new { 
            roomId = room.Id,
            roomName = room.Name
        };
    }
    
    private async Task<object> JoinNotEmptyRoom(string roomId, string userName, PrivateChatRoom room)
    {
        room.WantJoinUser.Add(Context.ConnectionId, userName);

        await Clients.Group(roomId).SendAsync("UserWantJoin", Context.ConnectionId, userName);
        
        return new { 
            roomId = room.Id,
            roomName = room.Name
        };
    }

    public async Task Accept(string roomId, string userId, MyClass keys)
    {
        var room = _roomsService.GetOrCreate(roomId, "not matter");
        
        if (!room.WantJoinUser.TryGetValue(userId, out var userName))
            return;
        room.WantJoinUser.Remove(userId);
        
        room.IsLocked = true;

        if (!UserRooms.TryGetValue(userId, out var listRooms))
            throw new HubException("he aren't user");
        if (!room.TryAddUser(userId, userName))
            throw new HubException("his name reserved");

        listRooms.Add(roomId);

        await Clients.Group(roomId).SendAsync("UserAccepted", Context.ConnectionId, userName);
        await Clients.Client(userId).SendAsync("Accepted");
        
        await Groups.AddToGroupAsync(userId, roomId);
        await Clients.Group(roomId).SendAsync("UserJoined", userName);
        await UpdateUsersList(roomId);

        var keyss = room.Users.Keys.ToArray();
        var circle = new Dictionary<string, string>();
        for (int i = 0; i < keyss.Length; i++)
        {
            circle.Add(keyss[i], keyss[(i + 1) % keyss.Length]);
        }

        await Clients.Group(roomId).SendAsync("StartCreatingToken", keys.p);
    }   
    
    public class MyClass
    {
        public string p;
        public string g;
        public string publicKey;
    }

    public async Task<object> SendPublicKey(string publicKey, int iteration = 0)
    {
        
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
        if (_roomsService.TryGetValue(roomId, out var room) && 
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
        if (_roomsService.TryGetValue(roomId, out var room) && 
            room.Users.TryGetValue(Context.ConnectionId, out var userName))
        {
            await Clients.Group(roomId).SendAsync("ReceiveMessage", userName, message);
        }
    }

    private async Task UpdateUsersList(string roomId)
    {
        if (_roomsService.TryGetValue(roomId, out var room))
        {
            await Clients.Group(roomId).SendAsync("UpdateUsers", room.UsersNames.ToList());
        }
    }
}