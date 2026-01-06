namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IMarketOrderRepository
{
    Task<IReadOnlyList<MarketOrderSummary>> GetActiveOrderIndexAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarketOrder>> GetActiveOrdersByResourceAsync(string resourceType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarketOrder>> GetOrdersByUserAsync(string userId, CancellationToken cancellationToken = default);
}
