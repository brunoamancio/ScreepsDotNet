namespace ScreepsDotNet.Common.Utilities;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Helpers for computing aggregate values from structure stores.
/// </summary>
public static class StoreMath
{
    public static int Sum(IReadOnlyDictionary<string, int>? store)
        => store is null ? 0 : store.Values.Sum();
}
