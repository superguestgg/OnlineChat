using Microsoft.AspNetCore.SignalR;

namespace OnlineChat.Hubs;

public class CallHub : Hub
{
    public async Task JoinRoom(string room)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, room);
        await Clients.GroupExcept(room, Context.ConnectionId)
            .SendAsync("UserJoined", Context.ConnectionId);
    }

    public Task SendOffer(string id, string offer)
        => Clients.Client(id).SendAsync("ReceiveOffer", Context.ConnectionId, offer);

    public Task SendAnswer(string id, string answer)
        => Clients.Client(id).SendAsync("ReceiveAnswer", Context.ConnectionId, answer);

    public Task SendCandidate(string id, string candidate)
        => Clients.Client(id).SendAsync("ReceiveCandidate", Context.ConnectionId, candidate);
}