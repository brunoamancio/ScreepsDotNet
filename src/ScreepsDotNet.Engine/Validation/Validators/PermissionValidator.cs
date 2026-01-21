using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation.Validators;

/// <summary>
/// Validates ownership and access control permissions for intent execution.
/// Checks: controller ownership/reservation, safe mode, rampart access, harvest permissions.
/// Extracted from Node.js engine inline permission checks.
/// </summary>
public sealed class PermissionValidator : IIntentValidator
{
    /// <summary>
    /// Validate intent permission requirements.
    /// </summary>
    public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
    {
        // Extract actor and target IDs from intent arguments
        if (!TryGetActorAndTargetIds(intent, out var actorId, out var targetId))
        {
            // No IDs found - let other validators handle this
            return ValidationResult.Success;
        }

        // Get actor (validation already done by StateValidator)
        if (!roomSnapshot.Objects.TryGetValue(actorId, out var actor))
            return ValidationResult.Success; // StateValidator will catch this

        // Get target if present
        RoomObjectSnapshot? target = null;
        if (!string.IsNullOrEmpty(targetId) && !roomSnapshot.Objects.TryGetValue(targetId, out target))
            return ValidationResult.Success; // StateValidator will catch this

        // Get room controller
        var controller = FindRoomController(roomSnapshot);

        // Validate based on intent type
        var permissionValidation = intent.Name switch
        {
            IntentKeys.UpgradeController => ValidateUpgradeController(actor, target, controller),
            IntentKeys.AttackController => ValidateAttackController(actor, target),
            IntentKeys.ReserveController => ValidateReserveController(target),
            IntentKeys.Attack or IntentKeys.RangedAttack => ValidateAttackIntent(actor, target, controller, roomSnapshot.GameTime),
            IntentKeys.Dismantle => ValidationResult.Success, // Safe mode doesn't block dismantle
            IntentKeys.Repair or IntentKeys.Transfer or IntentKeys.Withdraw => ValidateRampartAccess(actor, target, roomSnapshot),
            IntentKeys.Harvest => ValidateHarvestPermission(actor, controller),
            _ => ValidationResult.Success // Unknown intent types pass permission validation
        };

        return permissionValidation;
    }

    /// <summary>
    /// Validate upgradeController intent - requires controller ownership or reservation by actor.
    /// </summary>
    private static ValidationResult ValidateUpgradeController(RoomObjectSnapshot actor, RoomObjectSnapshot? controller, RoomObjectSnapshot? roomController)
    {
        if (controller is null)
            return ValidationResult.Success;

        // Check if controller is owned by actor's user
        if (controller.UserId == actor.UserId)
            return ValidationResult.Success;

        // Check if controller is reserved by actor's user
        if (controller.Reservation?.UserId == actor.UserId)
            return ValidationResult.Success;

        // Not owned or reserved by actor
        if (controller.Reservation?.UserId is not null)
        {
            var reservationResult = ValidationResult.Failure(ValidationErrorCode.ControllerNotReservedByActor);
            return reservationResult;
        }

        var ownershipResult = ValidationResult.Failure(ValidationErrorCode.ControllerNotOwned);
        return ownershipResult;
    }

    /// <summary>
    /// Validate attackController intent - requires controller to NOT be owned by actor.
    /// </summary>
    private static ValidationResult ValidateAttackController(RoomObjectSnapshot actor, RoomObjectSnapshot? controller)
    {
        if (controller is null)
            return ValidationResult.Success;

        // Can only attack controllers not owned by actor
        if (controller.UserId != actor.UserId)
            return ValidationResult.Success;

        var cannotAttackOwnResult = ValidationResult.Failure(ValidationErrorCode.ControllerNotOwned);
        return cannotAttackOwnResult;
    }

    /// <summary>
    /// Validate reserveController intent - requires neutral controller.
    /// </summary>
    private static ValidationResult ValidateReserveController(RoomObjectSnapshot? controller)
    {
        if (controller is null)
            return ValidationResult.Success;

        // Can only reserve neutral controllers (not owned, not reserved)
        if (controller.UserId is null && controller.Reservation is null)
            return ValidationResult.Success;

        var notNeutralResult = ValidationResult.Failure(ValidationErrorCode.NotOwnedOrReserved);
        return notNeutralResult;
    }

    /// <summary>
    /// Validate attack/rangedAttack intent - blocked by safe mode (unless attacking own creeps).
    /// </summary>
    private static ValidationResult ValidateAttackIntent(RoomObjectSnapshot actor, RoomObjectSnapshot? target, RoomObjectSnapshot? controller, int gameTime)
    {
        if (controller is null || target is null)
            return ValidationResult.Success;

        // Check if room has active safe mode
        if (controller.SafeMode.HasValue && controller.SafeMode.Value > gameTime)
        {
            // Safe mode is active - check if attacking own structures
            if (controller.UserId == actor.UserId)
            {
                // Room owner can attack own creeps even in safe mode
                return ValidationResult.Success;
            }

            // Safe mode blocks attacks from non-owners
            if (controller.UserId != actor.UserId)
            {
                var safeModeResult = ValidationResult.Failure(ValidationErrorCode.SafeModeActive);
                return safeModeResult;
            }
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validate rampart access - requires rampart to be public or owned by actor.
    /// </summary>
    private static ValidationResult ValidateRampartAccess(RoomObjectSnapshot actor, RoomObjectSnapshot? target, RoomSnapshot roomSnapshot)
    {
        if (target is null)
            return ValidationResult.Success;

        // Find rampart at target position
        var rampart = FindRampartAtPosition(roomSnapshot, target.X, target.Y);

        if (rampart is null)
            return ValidationResult.Success; // No rampart blocking

        // Check if rampart is public
        if (rampart.IsPublic == true)
            return ValidationResult.Success;

        // Check if actor owns the rampart
        if (rampart.UserId == actor.UserId)
            return ValidationResult.Success;

        // Rampart is private and not owned by actor
        var rampartBlockingResult = ValidationResult.Failure(ValidationErrorCode.RampartBlocking);
        return rampartBlockingResult;
    }

    /// <summary>
    /// Validate harvest permission - requires owned or reserved room.
    /// </summary>
    private static ValidationResult ValidateHarvestPermission(RoomObjectSnapshot actor, RoomObjectSnapshot? controller)
    {
        if (controller is null)
            return ValidationResult.Success; // No controller - neutral room, allow harvest

        // Check if controller is owned by actor's user
        if (controller.UserId == actor.UserId)
            return ValidationResult.Success;

        // Check if controller is reserved by actor's user
        if (controller.Reservation?.UserId == actor.UserId)
            return ValidationResult.Success;

        // Room is owned or reserved by another player
        var hostileRoomResult = ValidationResult.Failure(ValidationErrorCode.HostileRoom);
        return hostileRoomResult;
    }

    /// <summary>
    /// Try to extract actor and target IDs from intent arguments.
    /// </summary>
    private static bool TryGetActorAndTargetIds(IntentRecord intent, out string actorId, out string? targetId)
    {
        actorId = string.Empty;
        targetId = null;

        // Check if intent has arguments
        if (intent.Arguments.Count == 0)
            return false;

        var fields = intent.Arguments[0].Fields;

        // Try to get actor ID
        if (!fields.TryGetValue(IntentKeys.ActorId, out var actorIdField) || actorIdField.TextValue is null)
            return false;

        actorId = actorIdField.TextValue;

        // Try to get target ID (optional)
        if (fields.TryGetValue(IntentKeys.TargetId, out var targetIdField) && targetIdField.TextValue is not null)
        {
            targetId = targetIdField.TextValue;
        }

        return true;
    }

    /// <summary>
    /// Find the room controller in the room snapshot.
    /// </summary>
    private static RoomObjectSnapshot? FindRoomController(RoomSnapshot roomSnapshot)
    {
        var controller = roomSnapshot.Objects.Values.FirstOrDefault(o => o.Type == RoomObjectTypes.Controller);
        return controller;
    }

    /// <summary>
    /// Find a rampart at the specified position.
    /// </summary>
    private static RoomObjectSnapshot? FindRampartAtPosition(RoomSnapshot roomSnapshot, int x, int y)
    {
        var rampart = roomSnapshot.Objects.Values
            .FirstOrDefault(o => o.Type == RoomObjectTypes.Rampart && o.X == x && o.Y == y);
        return rampart;
    }
}
