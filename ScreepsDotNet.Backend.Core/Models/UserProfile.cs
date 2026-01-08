namespace ScreepsDotNet.Backend.Core.Models;

public sealed record UserProfile(
    string Id,
    string? Username,
    string? Email,
    bool EmailDirty,
    bool HasPassword,
    double Cpu,
    object? Badge,
    DateTime? LastRespawnDate,
    object? NotifyPrefs,
    object? Gcl,
    DateTime? LastChargeTime,
    bool Blocked,
    object? CustomBadge,
    double Power,
    double Money,
    UserSteamProfile? Steam,
    double PowerExperimentations,
    double PowerExperimentationTime);

public sealed record UserSteamProfile(string? Id, string? DisplayName, object? Ownership, bool? SteamProfileLinkHidden);
