using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation.Validators;

/// <summary>
/// Validates resource availability, capacity constraints, and costs for intent execution.
/// Checks: energy costs (build/repair/upgrade), mineral costs (boost), transfer amounts, capacity limits.
/// Extracted from Node.js engine inline resource checks.
/// </summary>
public sealed class ResourceValidator : IIntentValidator
{
    /// <summary>
    /// Validate intent resource requirements.
    /// </summary>
    public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
    {
        // Extract actor and target IDs from intent arguments
        if (!TryGetActorAndTargetIds(intent, out var actorId, out var targetId)) {
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

        // Validate based on intent type
        var resourceValidation = intent.Name switch
        {
            IntentKeys.Build => ValidateBuild(actor),
            IntentKeys.Repair => ValidateRepair(actor),
            IntentKeys.UpgradeController => ValidateUpgradeController(actor),
            IntentKeys.BoostCreep => ValidateBoostCreep(actor, target, intent),
            IntentKeys.UnboostCreep => ValidateUnboostCreep(actor, target),
            IntentKeys.Transfer => ValidateTransfer(actor, target, intent),
            IntentKeys.Withdraw => ValidateWithdraw(actor, target, intent),
            _ => ValidationResult.Success // Unknown intent types pass resource validation
        };

        return resourceValidation;
    }

    /// <summary>
    /// Validate build intent - requires energy (workParts * BUILD_POWER * 0.2 = workParts * 1).
    /// </summary>
    private static ValidationResult ValidateBuild(RoomObjectSnapshot actor)
    {
        var workParts = CountBodyParts(actor, BodyPartType.Work);
        if (workParts == 0)
            return ValidationResult.Success; // No work parts - StateValidator handles this

        var energyCost = workParts; // BUILD_POWER (5) * 0.2 = 1 energy per work part
        var energyAvailable = actor.Store.GetValueOrDefault(ResourceTypes.Energy, 0);

        if (energyAvailable < energyCost) {
            var result = ValidationResult.Failure(ValidationErrorCode.InsufficientEnergy);
            return result;
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validate repair intent - requires energy (workParts * REPAIR_POWER * REPAIR_COST = workParts * 1).
    /// </summary>
    private static ValidationResult ValidateRepair(RoomObjectSnapshot actor)
    {
        var workParts = CountBodyParts(actor, BodyPartType.Work);
        if (workParts == 0)
            return ValidationResult.Success;

        var energyCost = workParts; // REPAIR_POWER (100) * REPAIR_COST (0.01) = 1 energy per work part
        var energyAvailable = actor.Store.GetValueOrDefault(ResourceTypes.Energy, 0);

        if (energyAvailable < energyCost) {
            var result = ValidationResult.Failure(ValidationErrorCode.InsufficientEnergy);
            return result;
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validate upgradeController intent - requires energy (workParts * 1, boosted up to 2x).
    /// </summary>
    private static ValidationResult ValidateUpgradeController(RoomObjectSnapshot actor)
    {
        var workParts = CountBodyParts(actor, BodyPartType.Work);
        if (workParts == 0)
            return ValidationResult.Success;

        // Base energy cost is 1 per work part
        // Boosted creeps consume more (GH = 1.5x, GH2O = 1.8x, XGH2O = 2x)
        // For validation, we check if they have at least 1 energy per work part (minimum cost)
        var energyCost = workParts;
        var energyAvailable = actor.Store.GetValueOrDefault(ResourceTypes.Energy, 0);

        if (energyAvailable < energyCost) {
            var result = ValidationResult.Failure(ValidationErrorCode.InsufficientEnergy);
            return result;
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validate boostCreep intent - requires energy (20 per body part) and mineral (30 per body part).
    /// </summary>
    private static ValidationResult ValidateBoostCreep(RoomObjectSnapshot actor, RoomObjectSnapshot? target, IntentRecord intent)
    {
        if (target is null)
            return ValidationResult.Success;

        // Get boost mineral type and body part type
        if (!TryGetBoostParams(intent, out var mineralType, out var bodyPartType))
            return ValidationResult.Success; // Schema validation will handle this

        // Count unboosted body parts of the specified type
        var bodyPartsToBoost = target.Body.Count(p => p.Type == bodyPartType && p.Boost is null);
        if (bodyPartsToBoost == 0)
            return ValidationResult.Success; // No parts to boost

        // Calculate costs
        var energyCost = bodyPartsToBoost * ScreepsGameConstants.LabBoostEnergy; // 20 per part
        var mineralCost = bodyPartsToBoost * ScreepsGameConstants.LabBoostMineral; // 30 per part

        // Check energy availability
        var energyAvailable = actor.Store.GetValueOrDefault(ResourceTypes.Energy, 0);
        if (energyAvailable < energyCost) {
            var result = ValidationResult.Failure(ValidationErrorCode.InsufficientEnergy);
            return result;
        }

        // Check mineral availability
        var mineralAvailable = actor.Store.GetValueOrDefault(mineralType, 0);
        if (mineralAvailable < mineralCost) {
            var result = ValidationResult.Failure(ValidationErrorCode.InsufficientResource);
            return result;
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validate unboostCreep intent - requires lab to have capacity for returned mineral (15 per boosted part).
    /// </summary>
    private static ValidationResult ValidateUnboostCreep(RoomObjectSnapshot actor, RoomObjectSnapshot? target)
    {
        if (target is null)
            return ValidationResult.Success;

        // Count boosted body parts (will return 15 mineral per part)
        var boostedParts = target.Body.Count(p => p.Boost is not null);
        if (boostedParts == 0)
            return ValidationResult.Success; // No boosted parts

        var mineralReturned = boostedParts * ScreepsGameConstants.LabUnboostMineral; // 15 per part

        // Check if lab has capacity for returned mineral
        var labCapacity = actor.StoreCapacity ?? 0;
        var labStoreUsed = actor.Store.Values.Sum();
        var labCapacityRemaining = labCapacity - labStoreUsed;

        if (labCapacityRemaining < mineralReturned) {
            var result = ValidationResult.Failure(ValidationErrorCode.TargetCapacityFull);
            return result;
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validate transfer intent - requires actor to have resource amount and target to have capacity.
    /// </summary>
    private static ValidationResult ValidateTransfer(RoomObjectSnapshot actor, RoomObjectSnapshot? target, IntentRecord intent)
    {
        if (target is null)
            return ValidationResult.Success;

        // Get resource type and amount
        if (!TryGetTransferParams(intent, out var resourceType, out var amount))
            return ValidationResult.Success; // Schema validation will handle this

        // Check if actor has enough resource
        var actorResourceAvailable = actor.Store.GetValueOrDefault(resourceType, 0);
        if (actorResourceAvailable < amount) {
            var result = ValidationResult.Failure(ValidationErrorCode.InsufficientResource);
            return result;
        }

        // Check if target has capacity
        var targetCapacity = target.StoreCapacity ?? 0;
        var targetStoreUsed = target.Store.Values.Sum();
        var targetCapacityRemaining = targetCapacity - targetStoreUsed;

        if (targetCapacityRemaining < amount) {
            var result = ValidationResult.Failure(ValidationErrorCode.TargetCapacityFull);
            return result;
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validate withdraw intent - requires target to have resource amount and actor to have capacity.
    /// </summary>
    private static ValidationResult ValidateWithdraw(RoomObjectSnapshot actor, RoomObjectSnapshot? target, IntentRecord intent)
    {
        if (target is null)
            return ValidationResult.Success;

        // Get resource type and amount
        if (!TryGetTransferParams(intent, out var resourceType, out var amount))
            return ValidationResult.Success; // Schema validation will handle this

        // Check if target has enough resource
        var targetResourceAvailable = target.Store.GetValueOrDefault(resourceType, 0);
        if (targetResourceAvailable < amount) {
            var result = ValidationResult.Failure(ValidationErrorCode.InsufficientResource);
            return result;
        }

        // Check if actor has capacity
        var actorCapacity = actor.StoreCapacity ?? 0;
        var actorStoreUsed = actor.Store.Values.Sum();
        var actorCapacityRemaining = actorCapacity - actorStoreUsed;

        if (actorCapacityRemaining < amount) {
            var result = ValidationResult.Failure(ValidationErrorCode.ActorCapacityFull);
            return result;
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Count body parts of a specific type on a creep.
    /// </summary>
    private static int CountBodyParts(RoomObjectSnapshot actor, BodyPartType partType)
    {
        var count = actor.Body.Count(p => p.Type == partType);
        return count;
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
        if (fields.TryGetValue(IntentKeys.TargetId, out var targetIdField) && targetIdField.TextValue is not null) {
            targetId = targetIdField.TextValue;
        }

        return true;
    }

    /// <summary>
    /// Try to get boost parameters (mineral type and body part type) from intent.
    /// </summary>
    private static bool TryGetBoostParams(IntentRecord intent, out string mineralType, out BodyPartType bodyPartType)
    {
        mineralType = string.Empty;
        bodyPartType = default;

        if (intent.Arguments.Count == 0)
            return false;

        var fields = intent.Arguments[0].Fields;

        // Get mineral type (resourceType field)
        if (!fields.TryGetValue(IntentKeys.ResourceType, out var resourceField) || resourceField.TextValue is null)
            return false;

        mineralType = resourceField.TextValue;

        // Get body part type
        if (!fields.TryGetValue("bodyPartType", out var bodyPartField) || !bodyPartField.NumberValue.HasValue)
            return false;

        bodyPartType = (BodyPartType)bodyPartField.NumberValue.Value;

        return true;
    }

    /// <summary>
    /// Try to get transfer/withdraw parameters (resource type and amount) from intent.
    /// </summary>
    private static bool TryGetTransferParams(IntentRecord intent, out string resourceType, out int amount)
    {
        resourceType = string.Empty;
        amount = 0;

        if (intent.Arguments.Count == 0)
            return false;

        var fields = intent.Arguments[0].Fields;

        // Get resource type
        if (!fields.TryGetValue(IntentKeys.ResourceType, out var resourceField) || resourceField.TextValue is null)
            return false;

        resourceType = resourceField.TextValue;

        // Get amount
        if (!fields.TryGetValue(IntentKeys.Amount, out var amountField) || !amountField.NumberValue.HasValue)
            return false;

        amount = amountField.NumberValue.Value;

        return true;
    }
}
