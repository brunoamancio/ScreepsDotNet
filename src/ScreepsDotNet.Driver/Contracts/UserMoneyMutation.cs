namespace ScreepsDotNet.Driver.Contracts;

using System;
using System.Collections.Generic;

public sealed record UserMoneyMutation(string UserId, double NewMoney);

public sealed record UserMoneyLogEntry(
    string UserId,
    DateTime TimestampUtc,
    int Tick,
    string Type,
    double Balance,
    double Change,
    IReadOnlyDictionary<string, object?> Metadata);
