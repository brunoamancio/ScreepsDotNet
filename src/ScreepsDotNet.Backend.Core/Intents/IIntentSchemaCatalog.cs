namespace ScreepsDotNet.Backend.Core.Intents;

/// <summary>
/// Provides the combined set of built-in and mod-defined intent schemas.
/// </summary>
public interface IIntentSchemaCatalog
{
    /// <summary>
    /// Returns the current intent schema dictionary. Implementations should cache values and refresh
    /// when the underlying mods manifest changes.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, IntentDefinition>> GetSchemasAsync(CancellationToken cancellationToken = default);
}
