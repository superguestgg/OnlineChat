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
///
/// ВАЖНО #2 — про "init chunk": каждый WebM-поток от MediaRecorder начинается
/// со служебного заголовка (EBML+Segment+Tracks), без которого демуксер браузера
/// не может разобрать вообще ничего из последующих чанков. Если участник заходит
/// в звонок ПОЗЖЕ, чем кто-то другой начал говорить, он этот заголовок пропускает
/// и получает только "голые" Cluster-чанки — SourceBuffer ломается с ошибкой
/// CHUNK_DEMUXER_ERROR_APPEND_FAILED. Поэтому сервер кэширует первый чанк каждого
/// отправителя в рамках звонка и "проигрывает" его заново каждому новому участнику
/// перед тем, как продолжить транслировать живой поток.
/// </summary>
public class CallHub(ILogger<CallHub> logger) : Hub
{
    // roomId -> (connectionId -> userName)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> RoomParticipants = new();

    // roomId -> (connectionId -> первый полученный от него чанк — WebM init segment)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte[]>> RoomInitChunks = new();

    // connectionId -> roomId, чтобы на дисконнекте знать, откуда выходить
    private static readonly ConcurrentDictionary<string, string> ConnectionRoom = new();

    public async Task JoinCall(string roomId, string userName)
    {
        var participants = RoomParticipants.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, string>());

        // Снимок существующих участников ДО того, как добавим себя —
        // ровно то состояние, которое было в комнате до нашего входа.
        var others = participants
            .Select(p => new { connectionId = p.Key, userName = p.Value })
            .ToArray();

        participants[Context.ConnectionId] = userName;
        ConnectionRoom[Context.ConnectionId] = roomId;

        logger.LogInformation(
            "JoinCall: room={RoomId} user={UserName} connectionId={ConnectionId} totalParticipantsNow={Count}",
            roomId, userName, Context.ConnectionId, participants.Count);

        // Сначала полностью догоняем новичка (список участников + replay их init-чанков),
        // и ТОЛЬКО ПОСЛЕ ЭТОГО подключаем его к группе. Если сделать наоборот — с момента
        // AddToGroupAsync ему уже могут начать прилетать живые чанки от существующих
        // участников РАНЬШЕ, чем дойдёт replay их заголовка, и SourceBuffer получит
        // "мясо" без "костей" (та самая Unexpected element ID). Пока не в группе,
        // никакой Clients.OthersInGroup/Clients.Group физически до него не долетит.
        await Clients.Caller.SendAsync("CallJoined", others);

        if (RoomInitChunks.TryGetValue(roomId, out var initChunks))
        {
            foreach (var other in others)
            {
                if (initChunks.TryGetValue(other.connectionId, out var cachedChunk))
                {
                    await Clients.Caller.SendAsync("ReceiveAudioChunk", other.connectionId, cachedChunk);
                }
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        await Clients.OthersInGroup(roomId)
            .SendAsync("CallUserJoined", Context.ConnectionId, userName);
    }

    public async Task LeaveCall(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await RemoveFromCall(roomId, Context.ConnectionId);
    }

    /// <summary>
    /// Приём аудио-чанка и рассылка всем остальным участникам звонка в этой комнате.
    /// Первый чанк от каждого отправителя дополнительно кэшируется как init segment.
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

        var initChunks = RoomInitChunks.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, byte[]>());
        if (initChunks.TryAdd(Context.ConnectionId, chunk))
        {
            logger.LogInformation(
                "SendAudioChunk: закэширован init-chunk ({Size} байт) для connectionId={ConnectionId} в комнате {RoomId}",
                chunk.Length, Context.ConnectionId, roomId);
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
                RoomInitChunks.TryRemove(roomId, out _);
            }

            await Clients.Group(roomId).SendAsync("CallUserLeft", connectionId);
        }

        if (RoomInitChunks.TryGetValue(roomId, out var initChunks))
        {
            initChunks.TryRemove(connectionId, out _);
        }

        ConnectionRoom.TryRemove(connectionId, out _);
    }
}