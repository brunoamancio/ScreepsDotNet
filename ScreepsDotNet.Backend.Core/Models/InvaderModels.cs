namespace ScreepsDotNet.Backend.Core.Models;

using ScreepsDotNet.Backend.Core.Constants;

public record CreateInvaderRequest(string Room, int X, int Y, InvaderType Type, InvaderSize Size, bool Boosted);

public enum CreateInvaderResultStatus
{
    Success,
    InvalidParams,
    TooManyInvaders,
    HostilesPresent,
    NotOwned,
    InvalidRoom,
    UserNotFound
}

public record CreateInvaderResult(CreateInvaderResultStatus Status, string? Id = null, string? ErrorMessage = null);

public record RemoveInvaderRequest(string Id);

public enum RemoveInvaderResultStatus
{
    Success,
    InvalidObject,
    UserNotFound
}

public record RemoveInvaderResult(RemoveInvaderResultStatus Status);
