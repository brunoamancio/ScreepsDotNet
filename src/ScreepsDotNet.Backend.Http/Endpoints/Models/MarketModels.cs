namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal sealed record MarketOrderSummaryResponse(
    [property: JsonPropertyName("_id")] string ResourceType,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("buying")] int Buying,
    [property: JsonPropertyName("selling")] int Selling);

internal sealed record MarketOrderSummaryListResponse(
    [property: JsonPropertyName("list")] IReadOnlyList<MarketOrderSummaryResponse> List);

internal sealed record MarketOrderResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("user")] string? UserId,
    [property: JsonPropertyName("resourceType")] string ResourceType,
    [property: JsonPropertyName("type")] string OrderType,
    [property: JsonPropertyName("roomName")] string? RoomName,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("remainingAmount")] int RemainingAmount,
    [property: JsonPropertyName("totalAmount")] int TotalAmount,
    [property: JsonPropertyName("created")] int? CreatedTick,
    [property: JsonPropertyName("createdTimestamp")] long? CreatedTimestamp);

internal sealed record MarketOrderListResponse(
    [property: JsonPropertyName("list")] IReadOnlyList<MarketOrderResponse> List);

internal sealed record MarketStatsEntryResponse(
    [property: JsonPropertyName("resourceType")] string ResourceType,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("transactions")] int Transactions,
    [property: JsonPropertyName("volume")] double Volume,
    [property: JsonPropertyName("avgPrice")] double AveragePrice,
    [property: JsonPropertyName("stddevPrice")] double StandardDeviation);

internal sealed record MarketStatsResponse(
    [property: JsonPropertyName("stats")] IReadOnlyList<MarketStatsEntryResponse> Stats);
