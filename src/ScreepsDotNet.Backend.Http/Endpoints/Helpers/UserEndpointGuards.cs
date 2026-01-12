namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;

internal static class UserEndpointGuards
{
    public static UserProfile RequireUser(ICurrentUserAccessor accessor, string missingUserMessage)
        => accessor.CurrentUser ?? throw new InvalidOperationException(missingUserMessage);

    public static bool IsValidStatsInterval(int interval, IReadOnlyList<int> allowedIntervals)
        => allowedIntervals.Contains(interval);

    public static bool IsValidMemorySegment(int? segmentId, int minInclusive, int maxInclusive)
        => segmentId >= minInclusive && segmentId.Value <= maxInclusive;

    public static bool IsValidBranchName(string? branchName, int maxLength)
        => !string.IsNullOrWhiteSpace(branchName) && branchName.Length <= maxLength;

    public static bool IsValidActiveName(string? activeName, string activeWorldName, string activeSimName)
        => string.Equals(activeName, activeWorldName, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(activeName, activeSimName, StringComparison.OrdinalIgnoreCase);

    public static string ResolveBranchIdentifier(string? branch, string activeWorldIdentifier)
        => string.IsNullOrWhiteSpace(branch) ? activeWorldIdentifier : branch.Trim();

    public static bool IsAllowedBranchIdentifier(string branchIdentifier, string activeWorldIdentifier, int maxBranchLength)
        => string.Equals(branchIdentifier, activeWorldIdentifier, StringComparison.OrdinalIgnoreCase) ||
           IsValidBranchName(branchIdentifier, maxBranchLength);
}
