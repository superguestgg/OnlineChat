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
/// с одинаковым именем не должны схлопываться в одного "участника", иначе их
/// аудио-потоки (два разных WebM-контейнера) будут писаться в один и тот же
/// SourceBuffer на приёмнике и поломают декодирование.
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

        logger.LogInformation(
            "JoinCall: room={RoomId} user={UserName} connectionId={ConnectionId} totalParticipantsNow={Count} allParticipants=[{Participants}]",
            roomId, userName, Context.ConnectionId, participants.Count,
            string.Join(", ", participants.Select(p => $"{p.Value}:{p.Key}")));

        await Clients.OthersInGroup(roomId)
            .SendAsync("CallUserJoined", Context.ConnectionId, userName);

        var others = participants
            .Where(p => p.Key != Context.ConnectionId)
            .Select(p => new { connectionId = p.Key, userName = p.Value })
            .ToArray();

        logger.LogInformation(
            "JoinCall: отправляю {UserName} ({ConnectionId}) список из {OthersCount} уже присутствующих: [{Others}]",
            userName, Context.ConnectionId, others.Length,
            string.Join(", ", others.Select(o => $"{o.userName}:{o.connectionId}")));

        await Clients.Caller.SendAsync("CallJoined", others);
    }

    public async Task LeaveCall(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await RemoveFromCall(roomId, Context.ConnectionId);
    }

    /// <summary>
    /// Приём аудио-чанка и рассылка всем остальным участникам звонка в этой комнате.
    /// </summary>
    public async Task SendAudioChunk(string roomId, byte[] chunk)
    {
        if (!RoomParticipants.ContainsKey(roomId))
        {
            logger.LogWarning(
                "SendAudioChunk: комната {RoomId} не найдена для connectionId={ConnectionId}, чанк проигнорирован",
                roomId, Context.ConnectionId);
            return;
        }

        await Clients.OthersInGroup(roomId)
            .SendAsync("ReceiveAudioChunk", Context.ConnectionId, chunk);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionRoom.TryRemove(Context.ConnectionId, out var roomId))
        {
            logger.LogInformation(
                "OnDisconnected: connectionId={ConnectionId} room={RoomId} exception={Exception}",
                Context.ConnectionId, roomId, exception?.Message);
            await RemoveFromCall(roomId, Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveFromCall(string roomId, string connectionId)
    {
        if (RoomParticipants.TryGetValue(roomId, out var participants) &&
            participants.TryRemove(connectionId, out var userName))
        {
            logger.LogInformation(
                "RemoveFromCall: room={RoomId} user={UserName} connectionId={ConnectionId} осталось участников={Count}",
                roomId, userName, connectionId, participants.Count);

            if (participants.IsEmpty)
            {
                RoomParticipants.TryRemove(roomId, out _);
            }

            await Clients.Group(roomId).SendAsync("CallUserLeft", connectionId);
        }

        ConnectionRoom.TryRemove(connectionId, out _);
    }
}