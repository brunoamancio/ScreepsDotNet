namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Represents a single Screeps market order.
/// </summary>
/// <param name="Id">Mongo identifier as a string.</param>
/// <param name="UserId">User id if present.</param>
/// <param name="ResourceType">Resource traded.</param>
/// <param name="OrderType">"buy" or "sell".</param>
/// <param name="RoomName">Terminal room.</param>
/// <param name="PriceCredits">Human-readable price (credits).</param>
/// <param name="Amount">Initial amount.</param>
/// <param name="RemainingAmount">Unfilled amount.</param>
/// <param name="TotalAmount">Total order capacity.</param>
/// <param name="CreatedTick">Game tick recorded by the legacy backend.</param>
/// <param name="CreatedTimestamp">Unix time in milliseconds.</param>
public sealed record MarketOrder(string Id, string? UserId, string ResourceType, string OrderType, string? RoomName, decimal PriceCredits, int Amount, int RemainingAmount, int TotalAmount,
                                 int? CreatedTick, long? CreatedTimestamp);
