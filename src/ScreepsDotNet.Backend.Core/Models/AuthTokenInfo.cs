namespace ScreepsDotNet.Backend.Core.Models;

public sealed record AuthTokenInfo(string Token, string UserId, TimeSpan? TimeToLive);
