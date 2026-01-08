namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Http.Endpoints.Models;

internal static class MarketResponseFactory
{
    public static MarketOrderSummaryListResponse CreateSummaryList(IReadOnlyList<MarketOrderSummary> summaries)
    {
        var payload = summaries.Select(summary => new MarketOrderSummaryResponse(summary.ResourceType,
                                                                                 summary.Count,
                                                                                 summary.Buying,
                                                                                 summary.Selling))
                               .ToList();
        return new MarketOrderSummaryListResponse(payload);
    }

    public static MarketOrderListResponse CreateOrderList(IReadOnlyList<MarketOrder> orders)
    {
        var payload = orders.Select(order => new MarketOrderResponse(order.Id,
                                                                     order.UserId,
                                                                     order.ResourceType,
                                                                     order.OrderType,
                                                                     order.RoomName,
                                                                     order.PriceCredits,
                                                                     order.Amount,
                                                                     order.RemainingAmount,
                                                                     order.TotalAmount,
                                                                     order.CreatedTick,
                                                                     order.CreatedTimestamp))
                            .ToList();
        return new MarketOrderListResponse(payload);
    }

    public static MarketStatsResponse CreateStats(IReadOnlyList<MarketStatsEntry> stats)
    {
        var payload = stats.Select(entry => new MarketStatsEntryResponse(entry.ResourceType,
                                                                         entry.Date,
                                                                         entry.Transactions,
                                                                         entry.Volume,
                                                                         entry.AveragePrice,
                                                                         entry.StandardDeviation))
                           .ToList();
        return new MarketStatsResponse(payload);
    }
}
