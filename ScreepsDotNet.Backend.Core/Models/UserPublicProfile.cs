namespace ScreepsDotNet.Backend.Core.Models;

public sealed record UserPublicProfile(string Id, string? Username, object? Badge, object? Gcl, double Power, string? SteamId);
