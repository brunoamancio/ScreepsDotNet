namespace ScreepsDotNet.Driver.Contracts;

using System;
using System.Collections.Generic;

public sealed record UserResourceMutation(
    string UserId,
    string ResourceType,
    int NewBalance);

public sealed record UserResourceLogEntry(
    DateTime TimestampUtc,
    string UserId,
    string ResourceType,
    int Change,
    int Balance,
    string? MarketOrderId = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
