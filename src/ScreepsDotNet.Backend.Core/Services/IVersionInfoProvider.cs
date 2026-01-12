using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Services;

public interface IVersionInfoProvider
{
    Task<VersionInfo> GetAsync(CancellationToken cancellationToken = default);
}
