namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models.Mods;

/// <summary>
/// Loads the mods manifest (mods.json) and exposes the parsed configuration.
/// </summary>
public interface IModManifestProvider
{
    Task<ModManifest> GetManifestAsync(CancellationToken cancellationToken = default);
}
