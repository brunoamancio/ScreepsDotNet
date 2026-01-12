namespace ScreepsDotNet.Backend.Core.Cli;

/// <summary>
/// Manages lifecycle and messaging for CLI sessions.
/// </summary>
public interface ICliSessionManager
{
    Task<CliSession> CreateSessionAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> SendCommandAsync(Guid sessionId, string command, CancellationToken cancellationToken = default);

    Task CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
