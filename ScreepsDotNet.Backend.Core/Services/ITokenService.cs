namespace ScreepsDotNet.Backend.Core.Services;

using System.Collections.Generic;
using ScreepsDotNet.Backend.Core.Models;

public interface ITokenService
{
    Task<string> IssueTokenAsync(string userId, CancellationToken cancellationToken = default);

    Task<string?> ResolveUserIdAsync(string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthTokenInfo>> ListTokensAsync(string? userId = null, CancellationToken cancellationToken = default);

    Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
}
