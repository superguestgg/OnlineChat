using OnlineChat.Domain;
using OnlineChat.DTO;
using Riok.Mapperly.Abstractions;

namespace OnlineChat;

[Mapper]
public static partial class RoomMapper
{
    [MapProperty(nameof(ChatRoom.Users.Count), nameof(RoomInfo.UserCount))]
    public static partial RoomInfo ChatRoomToRoomInfo(ChatRoom chatRoom);

    [MapProperty(nameof(ChatRoom.Id), nameof(RoomCreationResult.RoomId))]
    [MapProperty(nameof(ChatRoom.Name), nameof(RoomCreationResult.RoomName))]
    public static partial RoomCreationResult ChatRoomToCreationResult(ChatRoom chatRoom);
}