namespace ScreepsDotNet.Backend.Core.Models.Bots;

/// <summary>
/// Additional knobs accepted by the legacy bot spawn CLI verb.
/// </summary>
public sealed record BotSpawnOptions(
    string? Username,
    int? Cpu,
    int? GlobalControlLevel,
    int? SpawnX,
    int? SpawnY);
