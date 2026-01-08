namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models.Bots;

/// <summary>
/// Supplies metadata about the available bot AI bundles.
/// </summary>
public interface IBotDefinitionProvider
{
    Task<IReadOnlyList<BotDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default);

    Task<BotDefinition?> FindDefinitionAsync(string name, CancellationToken cancellationToken = default);
}
