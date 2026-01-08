namespace ScreepsDotNet.Backend.Core.Services;

public interface ITokenService
{
    Task<string> IssueTokenAsync(string userId, CancellationToken cancellationToken = default);

    Task<string?> ResolveUserIdAsync(string token, CancellationToken cancellationToken = default);
}
