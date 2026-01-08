namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Constants;

public sealed record CreateFlagRequest(string Room, int X, int Y, string Name, Color Color, Color SecondaryColor);

public enum FlagResultStatus
{
    Success,
    InvalidParams,
    TooManyFlags,
    FlagNotFound
}

public sealed record FlagResult(FlagResultStatus Status, string? ErrorMessage = null);

public interface IFlagService
{
    Task<FlagResult> CreateFlagAsync(string userId, CreateFlagRequest request, CancellationToken cancellationToken = default);
    Task<FlagResult> ChangeFlagColorAsync(string userId, string room, string name, Color color, Color secondaryColor, CancellationToken cancellationToken = default);
    Task<FlagResult> RemoveFlagAsync(string userId, string room, string name, CancellationToken cancellationToken = default);
}
