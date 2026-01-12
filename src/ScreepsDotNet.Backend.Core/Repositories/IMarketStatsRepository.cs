namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IMarketStatsRepository
{
    Task<IReadOnlyList<MarketStatsEntry>> GetStatsAsync(string resourceType, CancellationToken cancellationToken = default);
}
