namespace ScreepsDotNet.Backend.Core.Models;

using ScreepsDotNet.Backend.Core.Constants;

public record PlaceConstructionRequest(string Room, int X, int Y, StructureType StructureType, string? Name);

public enum PlaceConstructionResultStatus
{
    Success,
    InvalidParams,
    InvalidLocation,
    NotControllerOwner,
    RclNotEnough,
    TooMany,
    InvalidRoom,
    UserNotFound
}

public record PlaceConstructionResult(PlaceConstructionResultStatus Status, string? Id = null, string? ErrorMessage = null);
