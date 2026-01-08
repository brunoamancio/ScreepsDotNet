namespace ScreepsDotNet.Backend.Core.Models.Strongholds;

/// <summary>
/// Result information returned after invoking a stronghold operation.
/// </summary>
public sealed record StrongholdSpawnResult(
    string RoomName,
    string TemplateName,
    string InvaderCoreId);
