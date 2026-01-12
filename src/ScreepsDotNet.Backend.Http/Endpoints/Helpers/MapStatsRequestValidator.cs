namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System.Text.RegularExpressions;
using ScreepsDotNet.Backend.Http.Endpoints.Models;

internal static partial class MapStatsRequestValidator
{
    public static bool IsValid(MapStatsRequestModel? request)
    {
        if (request is null || request.Rooms is null || request.Rooms.Count == 0)
            return false;

        if (string.IsNullOrWhiteSpace(request.StatName))
            return false;

        return StatNameRegex.IsMatch(request.StatName);
    }

    private static readonly Regex StatNameRegex = CreateStatNameRegex();

    [GeneratedRegex(@"^(.*?)(\d+)$", RegexOptions.Compiled)]
    private static partial Regex CreateStatNameRegex();
}
