namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Aggregated counts used by the orders-index endpoint.
/// </summary>
public sealed record MarketOrderSummary(string ResourceType, int Count, int Buying, int Selling);
