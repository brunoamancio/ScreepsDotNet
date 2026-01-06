namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Represents a single historical market stats entry.
/// </summary>
public sealed record MarketStatsEntry(string ResourceType, string Date, int Transactions, double Volume, double AveragePrice, double StandardDeviation);
