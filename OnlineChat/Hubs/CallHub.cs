using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace OnlineChat.Hubs;

/// <summary>
/// Отдельный хаб для голосовых звонков — отдельное WS-подключение от ChatHub.
/// Звонок привязан к roomId той же комнаты, где идёт чат: заходя в звонок,
/// пользователь передаёт тот же roomId, что и в ChatHub.JoinRoom.
/// Аудио идёт как бинарные чанки через SignalR (без WebRTC), сервер только релеит
/// их остальным участникам комнаты через Groups — ровно та же механика, что у тебя в ChatHub.
/// </summary>
public class CallHub : Hub
{
    // roomId -> (connectionId -> userName), для списка участников звонка и рассылки чанков
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> RoomParticipants = new();

    // connectionId -> roomId, чтобы на дисконнекте знать, откуда выходить
    private static readonly ConcurrentDictionary<string, string> ConnectionRoom = new();

    public async Task JoinCall(string roomId, string userName)
    {
        var participants = RoomParticipants.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, string>());
        participants[Context.ConnectionId] = userName;
        ConnectionRoom[Context.ConnectionId] = roomId;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Остальным участникам звонка — что подключился новый
        await Clients.OthersInGroup(roomId).SendAsync("CallUserJoined", userName);

        // Новому участнику — кто уже в звонке (для создания audio-плееров под каждого)
        var others = participants
            .Where(p => p.Key != Context.ConnectionId)
            .Select(p => p.Value)
            .ToArray();

        await Clients.Caller.SendAsync("CallJoined", others);
    }

    public async Task LeaveCall(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await RemoveFromCall(roomId, Context.ConnectionId);
    }

    /// <summary>
    /// Приём аудио-чанка и рассылка всем остальным участникам звонка в этой комнате
    /// </summary>
    public async Task SendAudioChunk(string roomId, byte[] chunk)
    {
        if (!RoomParticipants.TryGetValue(roomId, out var participants) ||
            !participants.TryGetValue(Context.ConnectionId, out var userName))
        {
            return;
        }

        await Clients.OthersInGroup(roomId).SendAsync("ReceiveAudioChunk", userName, chunk);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionRoom.TryRemove(Context.ConnectionId, out var roomId))
        {
            await RemoveFromCall(roomId, Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveFromCall(string roomId, string connectionId)
    {
        if (RoomParticipants.TryGetValue(roomId, out var participants) &&
            participants.TryRemove(connectionId, out var userName))
        {
            if (participants.IsEmpty)
            {
                RoomParticipants.TryRemove(roomId, out _);
            }

            await Clients.Group(roomId).SendAsync("CallUserLeft", userName);
        }

        ConnectionRoom.TryRemove(connectionId, out _);
    }
}