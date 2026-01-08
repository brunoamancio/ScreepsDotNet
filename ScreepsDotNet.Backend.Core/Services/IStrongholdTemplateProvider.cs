namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models.Strongholds;

/// <summary>
/// Supplies metadata describing NPC stronghold templates.
/// </summary>
public interface IStrongholdTemplateProvider
{
    Task<IReadOnlyList<StrongholdTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<StrongholdTemplate?> FindTemplateAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetDepositTypesAsync(CancellationToken cancellationToken = default);
}
