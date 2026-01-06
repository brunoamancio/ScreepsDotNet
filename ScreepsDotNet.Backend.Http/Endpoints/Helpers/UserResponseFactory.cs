namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Backend.Http.Routing;

internal static class UserResponseFactory
{
    public static Dictionary<string, object?> CreateEmpty()
        => new(StringComparer.Ordinal);

    public static Dictionary<string, object?> CreateTimestamp()
        => new(StringComparer.Ordinal)
        {
            [UserResponseFields.Timestamp] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
}
