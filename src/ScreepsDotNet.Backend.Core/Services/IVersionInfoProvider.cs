namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models;

public interface IVersionInfoProvider
{
    Task<VersionInfo> GetAsync(CancellationToken cancellationToken = default);
}
