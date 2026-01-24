namespace ScreepsDotNet.Engine.Data.Bulk;

using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Centralized logic for finding pending patches in mutation writers.
/// Implements "last wins" semantics when multiple patches exist for the same object.
/// </summary>
internal static class PendingPatchHelper
{
    /// <summary>
    /// Finds the last pending patch for a given object ID.
    /// Searches backwards to implement "last wins" semantics - if multiple patches exist for the same object,
    /// the most recent one takes precedence.
    /// </summary>
    /// <param name="patches">List of patches to search</param>
    /// <param name="objectId">Object ID to search for</param>
    /// <param name="patch">Output parameter receiving the found patch, or default if not found</param>
    /// <returns>True if a patch was found, false otherwise</returns>
    /// <remarks>
    /// This pattern emulates Node.js in-place mutation behavior. In Node.js, earlier processor steps
    /// modify objects directly, so later steps see the modified values. In .NET, we queue patches
    /// and need to check pending patches to see modifications from earlier steps within the same tick.
    /// </remarks>
    public static bool TryFindLastPatch(
        IReadOnlyList<RoomObjectPatch> patches,
        string objectId,
        out RoomObjectPatchPayload patch)
    {
        // Search backwards for last-wins semantics
        for (var i = patches.Count - 1; i >= 0; i--) {
            if (patches[i].ObjectId != objectId || patches[i].Payload is not RoomObjectPatchPayload payload) continue;

            patch = payload;
            return true;
        }

        patch = new RoomObjectPatchPayload();
        return false;
    }

    /// <summary>
    /// Finds the last pending patch for a given object ID (tuple list variant).
    /// Used by test infrastructure where patches are stored as tuples.
    /// </summary>
    public static bool TryFindLastPatch(
        IReadOnlyList<(string ObjectId, RoomObjectPatchPayload Payload)> patches,
        string objectId,
        out RoomObjectPatchPayload patch)
    {
        // Search backwards for last-wins semantics
        for (var i = patches.Count - 1; i >= 0; i--) {
            var (patchObjectId, payload) = patches[i];
            if (patchObjectId != objectId) continue;

            patch = payload;
            return true;
        }

        patch = new RoomObjectPatchPayload();
        return false;
    }
}
