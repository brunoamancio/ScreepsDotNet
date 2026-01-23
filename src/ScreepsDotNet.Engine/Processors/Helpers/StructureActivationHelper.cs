namespace ScreepsDotNet.Engine.Processors.Helpers;

using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Helper for validating structure activation based on controller ownership and RCL limits.
/// Implements the same logic as Node.js checkStructureAgainstController function.
/// </summary>
internal static class StructureActivationHelper
{
    /// <summary>
    /// Checks if a structure is active and allowed to operate based on controller state and RCL limits.
    /// </summary>
    /// <param name="structure">The structure to validate.</param>
    /// <param name="roomObjects">All objects in the room.</param>
    /// <param name="controller">The room controller (null if none exists).</param>
    /// <returns>True if structure is active, false otherwise.</returns>
    public static bool IsStructureActive(RoomObjectSnapshot structure, IReadOnlyDictionary<string, RoomObjectSnapshot> roomObjects, RoomObjectSnapshot? controller)
    {
        // Owner-less structures are always active (roads, containers, walls)
        if (string.IsNullOrWhiteSpace(structure.UserId))
            return true;

        // Structure requires controller to be active
        if (controller is null)
            return false;

        // Controller must be level 1+ and owned by same user
        var controllerLevel = controller.Level ?? 0;
        if (controllerLevel < 1)
            return false;

        if (!string.Equals(controller.UserId, structure.UserId, StringComparison.Ordinal))
            return false;

        // Get structure type enum
        if (!TryParseStructureType(structure.Type, out var structureType))
            return false;

        // Check if structure is allowed at this RCL level
        if (!GameConstants.ControllerStructures.TryGetValue(structureType, out var limitsPerLevel))
            return false;

        var allowedCount = limitsPerLevel[controllerLevel];
        if (allowedCount == 0)
            return false;

        // If only one of this structure is ever allowed (e.g., spawn, observer, power spawn at RCL 8),
        // then this structure is active as long as any are allowed at current RCL
        var maxAllowedAtRcl8 = limitsPerLevel[8];
        if (maxAllowedAtRcl8 == 1)
            return allowedCount > 0;

        // For multi-instance structures (extensions, towers, etc.), use distance-based priority.
        // Count how many structures of same type and user are closer to controller.
        var structureDistance = CalculateChebyshevDistance(structure, controller);
        var closerStructuresCount = 0;

        foreach (var obj in roomObjects.Values) {
            if (!string.Equals(obj.Type, structure.Type, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.UserId, structure.UserId, StringComparison.Ordinal))
                continue;

            if (string.Equals(obj.Id, structure.Id, StringComparison.Ordinal))
                continue;

            var objDistance = CalculateChebyshevDistance(obj, controller);

            // If another structure is closer, increment counter
            if (objDistance < structureDistance) {
                closerStructuresCount++;
            }
            // If same distance, use ID comparison for deterministic ordering
            else if (objDistance == structureDistance) {
                var comparison = string.Compare(obj.Id, structure.Id, StringComparison.Ordinal);
                if (comparison < 0)
                    closerStructuresCount++;
            }
        }

        // Structure is active if there are slots remaining after accounting for closer structures
        var result = closerStructuresCount < allowedCount;
        return result;
    }

    /// <summary>
    /// Finds the controller in the room.
    /// </summary>
    /// <param name="roomObjects">All objects in the room.</param>
    /// <returns>The controller, or null if none exists.</returns>
    public static RoomObjectSnapshot? FindController(IReadOnlyDictionary<string, RoomObjectSnapshot> roomObjects)
    {
        foreach (var obj in roomObjects.Values) {
            if (string.Equals(obj.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
                return obj;
        }

        return null;
    }

    /// <summary>
    /// Calculates Chebyshev distance (max of dx, dy) between two objects.
    /// </summary>
    private static int CalculateChebyshevDistance(RoomObjectSnapshot a, RoomObjectSnapshot b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        var result = Math.Max(dx, dy);
        return result;
    }

    /// <summary>
    /// Parses structure type string to enum (only for structures tracked in ControllerStructures).
    /// </summary>
    private static bool TryParseStructureType(string type, out StructureType structureType)
    {
        structureType = type switch
        {
            RoomObjectTypes.Spawn => StructureType.Spawn,
            RoomObjectTypes.Extension => StructureType.Extension,
            RoomObjectTypes.Road => StructureType.Road,
            RoomObjectTypes.ConstructedWall => StructureType.Wall,
            RoomObjectTypes.Rampart => StructureType.Rampart,
            RoomObjectTypes.Link => StructureType.Link,
            RoomObjectTypes.Storage => StructureType.Storage,
            RoomObjectTypes.Tower => StructureType.Tower,
            RoomObjectTypes.Observer => StructureType.Observer,
            RoomObjectTypes.PowerSpawn => StructureType.PowerSpawn,
            RoomObjectTypes.Extractor => StructureType.Extractor,
            RoomObjectTypes.Lab => StructureType.Lab,
            RoomObjectTypes.Terminal => StructureType.Terminal,
            RoomObjectTypes.Container => StructureType.Container,
            RoomObjectTypes.Nuker => StructureType.Nuker,
            RoomObjectTypes.Factory => StructureType.Factory,
            _ => StructureType.Spawn
        };

        var result = type switch
        {
            RoomObjectTypes.Spawn or RoomObjectTypes.Extension or RoomObjectTypes.Road or
            RoomObjectTypes.ConstructedWall or RoomObjectTypes.Rampart or RoomObjectTypes.Link or
            RoomObjectTypes.Storage or RoomObjectTypes.Tower or RoomObjectTypes.Observer or
            RoomObjectTypes.PowerSpawn or RoomObjectTypes.Extractor or RoomObjectTypes.Lab or
            RoomObjectTypes.Terminal or RoomObjectTypes.Container or RoomObjectTypes.Nuker or
            RoomObjectTypes.Factory => true,
            _ => false
        };

        return result;
    }
}
