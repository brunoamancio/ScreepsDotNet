namespace ScreepsDotNet.Backend.Core.Models;

using System;

public sealed record AuthTokenInfo(string Token, string UserId, TimeSpan? TimeToLive);
