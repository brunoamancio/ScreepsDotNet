namespace ScreepsDotNet.Backend.Core.Models;

public sealed record MoneyHistoryPage(int Page, bool HasMore, IReadOnlyList<IReadOnlyDictionary<string, object?>> Entries);
