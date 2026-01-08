namespace ScreepsDotNet.Backend.Core.Models.Strongholds;

/// <summary>
/// Parameters supported by the legacy stronghold spawn CLI command.
/// </summary>
public sealed record StrongholdSpawnOptions(
    string? TemplateName,
    int? X,
    int? Y,
    string? OwnerUserId,
    int? DeployDelayTicks);
