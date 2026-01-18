namespace ScreepsDotNet.Driver.Contracts;

using System;

public sealed record TransactionLogEntry(
    DateTime TimestampUtc,
    int Tick,
    string? SenderId,
    string? RecipientId,
    string ResourceType,
    int Amount,
    string FromRoom,
    string ToRoom,
    string? OrderId = null,
    string? Description = null);
