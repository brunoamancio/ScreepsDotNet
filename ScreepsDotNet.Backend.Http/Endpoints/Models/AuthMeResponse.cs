namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System;
using System.Text.Json.Serialization;
using ScreepsDotNet.Backend.Core.Models;

internal sealed record AuthMeResponse
{
    [JsonPropertyName("_id")]
    public required string Id { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("emailDirty")]
    public bool EmailDirty { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("cpu")]
    public double Cpu { get; init; }

    [JsonPropertyName("badge")]
    public object? Badge { get; init; }

    [JsonPropertyName("password")]
    public bool HasPassword { get; init; }

    [JsonPropertyName("lastRespawnDate")]
    public DateTime? LastRespawnDate { get; init; }

    [JsonPropertyName("notifyPrefs")]
    public object? NotifyPrefs { get; init; }

    [JsonPropertyName("gcl")]
    public object? Gcl { get; init; }

    [JsonPropertyName("lastChargeTime")]
    public DateTime? LastChargeTime { get; init; }

    [JsonPropertyName("blocked")]
    public bool Blocked { get; init; }

    [JsonPropertyName("customBadge")]
    public object? CustomBadge { get; init; }

    [JsonPropertyName("power")]
    public double Power { get; init; }

    [JsonPropertyName("money")]
    public double Money { get; init; }

    [JsonPropertyName("steam")]
    public AuthSteamResponse? Steam { get; init; }

    [JsonPropertyName("powerExperimentations")]
    public double PowerExperimentations { get; init; }

    [JsonPropertyName("powerExperimentationTime")]
    public double PowerExperimentationTime { get; init; }

    public static AuthMeResponse From(UserProfile profile)
        => new()
        {
            Id = profile.Id,
            Email = profile.Email,
            EmailDirty = profile.EmailDirty,
            Username = profile.Username,
            Cpu = profile.Cpu,
            Badge = profile.Badge,
            HasPassword = profile.HasPassword,
            LastRespawnDate = profile.LastRespawnDate,
            NotifyPrefs = profile.NotifyPrefs,
            Gcl = profile.Gcl,
            LastChargeTime = profile.LastChargeTime,
            Blocked = profile.Blocked,
            CustomBadge = profile.CustomBadge,
            Power = profile.Power,
            Money = profile.Money,
            Steam = profile.Steam is null
                ? null
                : new AuthSteamResponse(profile.Steam.Id,
                                        profile.Steam.DisplayName,
                                        profile.Steam.Ownership,
                                        profile.Steam.SteamProfileLinkHidden),
            PowerExperimentations = profile.PowerExperimentations,
            PowerExperimentationTime = profile.PowerExperimentationTime
        };
}

internal sealed record AuthSteamResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("ownership")] object? Ownership,
    [property: JsonPropertyName("steamProfileLinkHidden")] bool? SteamProfileLinkHidden);
