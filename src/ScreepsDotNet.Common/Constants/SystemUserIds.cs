namespace ScreepsDotNet.Common.Constants;

using System;

public static class SystemUserIds
{
    public const string LegacyInvader = "2";
    public const string LegacySourceKeeper = "3";
    public const string NamedInvader = "Invader";
    public const string NamedSourceKeeper = "SourceKeeper";

    public static bool IsNpcUser(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return string.Equals(userId, LegacyInvader, StringComparison.Ordinal) ||
               string.Equals(userId, LegacySourceKeeper, StringComparison.Ordinal) ||
               string.Equals(userId, NamedInvader, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(userId, NamedSourceKeeper, StringComparison.OrdinalIgnoreCase);
    }
}
