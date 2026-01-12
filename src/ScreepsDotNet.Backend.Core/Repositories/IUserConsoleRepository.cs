namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserConsoleRepository
{
    Task EnqueueExpressionAsync(string userId, string expression, bool hidden, CancellationToken cancellationToken = default);
}
