namespace ScreepsDotNet.Backend.Core.Models;

public record PlaceSpawnRequest(string Room, int X, int Y, string? Name);

public enum PlaceSpawnResultStatus
{
    Success,
    InvalidParams,
    Blocked,
    NoCpu,
    TooSoonAfterLastRespawn,
    AlreadyPlaying,
    InvalidRoom,
    InvalidPosition,
    UserNotFound
}

public record PlaceSpawnResult(PlaceSpawnResultStatus Status, string? ErrorMessage = null);
