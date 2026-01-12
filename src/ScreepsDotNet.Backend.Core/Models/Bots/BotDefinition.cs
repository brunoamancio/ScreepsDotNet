namespace ScreepsDotNet.Backend.Core.Models.Bots;

/// <summary>
/// Describes an installable bot AI bundle.
/// </summary>
public sealed record BotDefinition(
    string Name,
    string Description,
    IReadOnlyDictionary<string, string> Modules);
