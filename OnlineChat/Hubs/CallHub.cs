using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace OnlineChat.Hubs;

/// <summary>
/// Отдельный хаб для голосовых звонков — отдельное WS-подключение от ChatHub.
/// Звонок привязан к roomId той же комнаты, где идёт чат: заходя в звонок,
/// пользователь передаёт тот же roomId, что и в ChatHub.JoinRoom.
/// Аудио идёт как бинарные чанки через SignalR (без WebRTC), сервер только релеит
/// их остальным участникам комнаты через Groups.
///
/// ВАЖНО: участники маршрутизируются по Context.ConnectionId, а не по userName.
/// userName используется только как отображаемая подпись — два разных подключения
/// с одинаковым именем (например, случайно продублированная вкладка браузера)
/// не должны схлопываться в одного "участника", иначе их аудио-потоки (два разных
/// WebM-контейнера) будут писаться в один и тот же SourceBuffer на приёмнике
/// и поломают декодирование.
/// </summary>
public class CallHub(ILogger<CallHub> logger) : Hub
{
    // roomId -> (connectionId -> userName)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> RoomParticipants = new();

    // connectionId -> roomId, чтобы на дисконнекте знать, откуда выходить
    private static readonly ConcurrentDictionary<string, string> ConnectionRoom = new();

    public async Task JoinCall(string roomId, string userName)
    {
        var participants = RoomParticipants.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, string>());
        participants[Context.ConnectionId] = userName;
        ConnectionRoom[Context.ConnectionId] = roomId;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Остальным участникам — что подключился новый (передаём connectionId как
        // стабильный ключ для их клиентского remotePlayers, userName — только для подписи)
        await Clients.OthersInGroup(roomId)
            .SendAsync("CallUserJoined", Context.ConnectionId, userName);

        // Новому участнику — список уже присутствующих (тоже connectionId + userName)
        var others = participants
            .Where(p => p.Key != Context.ConnectionId)
            .Select(p => new { connectionId = p.Key, userName = p.Value })
            .ToArray();

        await Clients.Caller.SendAsync("CallJoined", others);
    }

    public async Task LeaveCall(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await RemoveFromCall(roomId, Context.ConnectionId);
    }

    /// <summary>
    /// Приём аудио-чанка и рассылка всем остальным участникам звонка в этой комнате.
    /// Собеседникам сообщается connectionId отправителя — им маршрутизируется чанк
    /// к правильному плееру независимо от того, совпадают ли у кого-то имена.
    /// </summary>
    public async Task SendAudioChunk(string roomId, byte[] chunk)
    {
        if (!RoomParticipants.ContainsKey(roomId))
        {
            return;
        }

        await Clients.OthersInGroup(roomId)
            .SendAsync("ReceiveAudioChunk", Context.ConnectionId, chunk);
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
            participants.TryRemove(connectionId, out _))
        {
            if (participants.IsEmpty)
            {
                RoomParticipants.TryRemove(roomId, out _);
            }

            await Clients.Group(roomId).SendAsync("CallUserLeft", connectionId);
        }

        ConnectionRoom.TryRemove(connectionId, out _);
    }
}