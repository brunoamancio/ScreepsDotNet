using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation.Validators;

/// <summary>
/// Validates intent payload structure.
/// Checks: required fields, field types, resource types, non-negative amounts.
/// Runs FIRST in validation pipeline to reject malformed payloads early.
/// </summary>
public sealed class SchemaValidator : IIntentValidator
{
    private static readonly HashSet<string> ValidResourceTypes = new(StringComparer.Ordinal)
    {
        // Basic resources
        ResourceTypes.Energy,
        ResourceTypes.Power,

        // Base minerals
        ResourceTypes.Hydrogen,
        ResourceTypes.Oxygen,
        ResourceTypes.Utrium,
        ResourceTypes.Lemergium,
        ResourceTypes.Keanium,
        ResourceTypes.Zynthium,
        ResourceTypes.Catalyst,
        ResourceTypes.Ghodium,

        // Tier 1 compounds
        ResourceTypes.Hydroxide,
        ResourceTypes.ZynthiumKeanite,
        ResourceTypes.UtriumLemergite,

        // Tier 2 compounds
        ResourceTypes.UtriumHydride,
        ResourceTypes.UtriumOxide,
        ResourceTypes.KeaniumHydride,
        ResourceTypes.KeaniumOxide,
        ResourceTypes.LemergiumHydride,
        ResourceTypes.LemergiumOxide,
        ResourceTypes.ZynthiumHydride,
        ResourceTypes.ZynthiumOxide,
        ResourceTypes.GhodiumHydride,
        ResourceTypes.GhodiumOxide,

        // Tier 3 compounds
        ResourceTypes.UtriumAcid,
        ResourceTypes.UtriumAlkalide,
        ResourceTypes.KeaniumAcid,
        ResourceTypes.KeaniumAlkalide,
        ResourceTypes.LemergiumAcid,
        ResourceTypes.LemergiumAlkalide,
        ResourceTypes.ZynthiumAcid,
        ResourceTypes.ZynthiumAlkalide,
        ResourceTypes.GhodiumAcid,
        ResourceTypes.GhodiumAlkalide,

        // Boosts
        ResourceTypes.CatalyzedGhodiumAlkalide,
        ResourceTypes.CatalyzedGhodiumAcid,
        ResourceTypes.CatalyzedKeaniumAlkalide,
        ResourceTypes.CatalyzedKeaniumAcid,
        ResourceTypes.CatalyzedLemergiumAlkalide,
        ResourceTypes.CatalyzedLemergiumAcid,
        ResourceTypes.CatalyzedUtriumAlkalide,
        ResourceTypes.CatalyzedUtriumAcid,
        ResourceTypes.CatalyzedZynthiumAlkalide,
        ResourceTypes.CatalyzedZynthiumAcid,

        // Commodities
        ResourceTypes.Metal,
        ResourceTypes.Biomass,
        ResourceTypes.Silicon,
        ResourceTypes.Mist,

        ResourceTypes.Alloy,
        ResourceTypes.Cell,
        ResourceTypes.Wire,
        ResourceTypes.Condensate,

        ResourceTypes.Composite,
        ResourceTypes.Crystal,
        ResourceTypes.Liquid,

        ResourceTypes.Tube,
        ResourceTypes.Fixtures,
        ResourceTypes.Frame,
        ResourceTypes.Hydraulics,
        ResourceTypes.Machine,

        ResourceTypes.Battery,
        ResourceTypes.Concentrate,
        ResourceTypes.Extract,
        ResourceTypes.Spirit,
        ResourceTypes.Emanation,
        ResourceTypes.Essence,

        // Final products
        ResourceTypes.Organism,
        ResourceTypes.Organoid,
        ResourceTypes.Muscle,
        ResourceTypes.Tissue,
        ResourceTypes.Phlegm,

        // Special
        ResourceTypes.Ops
    };

    /// <summary>
    /// Validate intent schema (payload structure).
    /// </summary>
    public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
    {
        // Allow intents with no arguments (other validators will handle logic)
        if (intent.Arguments.Count == 0)
            return ValidationResult.Success;

        var fields = intent.Arguments[0].Fields;

        // Validate based on intent type
        var intentValidation = intent.Name switch
        {
            IntentKeys.Attack or IntentKeys.RangedAttack or IntentKeys.Heal or IntentKeys.RangedHeal
                => ValidateTargetIdOnly(fields),

            IntentKeys.Harvest or IntentKeys.Build or IntentKeys.Repair or IntentKeys.UpgradeController
            or IntentKeys.ReserveController or IntentKeys.AttackController or IntentKeys.Pickup
                => ValidateTargetIdOnly(fields),

            IntentKeys.Transfer or IntentKeys.Withdraw
                => ValidateResourceTransfer(fields),

            IntentKeys.Drop
                => ValidateResourceDrop(fields),

            IntentKeys.TransferEnergy or IntentKeys.ProcessPower or IntentKeys.RunReaction
            or IntentKeys.BoostCreep or IntentKeys.UnboostCreep
                => ValidateTargetIdOnly(fields), // Structure intents

            _ => ValidationResult.Success // Unknown intent types pass schema validation
        };

        return intentValidation;
    }

    private static ValidationResult ValidateTargetIdOnly(IReadOnlyDictionary<string, IntentFieldValue> fields)
    {
        // Check required field "id"
        if (!fields.TryGetValue(IntentKeys.TargetId, out var idField))
        {
            var missingFieldResult = ValidationResult.Failure(ValidationErrorCode.MissingRequiredField);
            return missingFieldResult;
        }

        // Validate type: must be Text
        if (idField.Kind != IntentFieldValueKind.Text)
        {
            var invalidTypeResult = ValidationResult.Failure(ValidationErrorCode.InvalidFieldType);
            return invalidTypeResult;
        }

        // Validate non-null, non-empty
        if (string.IsNullOrEmpty(idField.TextValue))
        {
            var invalidFieldResult = ValidationResult.Failure(ValidationErrorCode.InvalidFieldType);
            return invalidFieldResult;
        }

        return ValidationResult.Success;
    }

    private static ValidationResult ValidateResourceTransfer(IReadOnlyDictionary<string, IntentFieldValue> fields)
    {
        // Check required field "id"
        if (!fields.TryGetValue(IntentKeys.TargetId, out var idField))
        {
            var missingIdResult = ValidationResult.Failure(ValidationErrorCode.MissingRequiredField);
            return missingIdResult;
        }

        // Validate id type
        if (idField.Kind != IntentFieldValueKind.Text || string.IsNullOrEmpty(idField.TextValue))
        {
            var invalidIdTypeResult = ValidationResult.Failure(ValidationErrorCode.InvalidFieldType);
            return invalidIdTypeResult;
        }

        // Check required field "resourceType"
        if (!fields.TryGetValue(IntentKeys.ResourceType, out var resourceTypeField))
        {
            var missingResourceTypeResult = ValidationResult.Failure(ValidationErrorCode.MissingRequiredField);
            return missingResourceTypeResult;
        }

        // Validate resourceType type
        if (resourceTypeField.Kind != IntentFieldValueKind.Text || string.IsNullOrEmpty(resourceTypeField.TextValue))
        {
            var invalidResourceTypeTypeResult = ValidationResult.Failure(ValidationErrorCode.InvalidFieldType);
            return invalidResourceTypeTypeResult;
        }

        // Validate resourceType value
        if (!ValidResourceTypes.Contains(resourceTypeField.TextValue!))
        {
            var invalidResourceResult = ValidationResult.Failure(ValidationErrorCode.InvalidResourceType);
            return invalidResourceResult;
        }

        // Check required field "amount"
        if (!fields.TryGetValue(IntentKeys.Amount, out var amountField))
        {
            var missingAmountResult = ValidationResult.Failure(ValidationErrorCode.MissingRequiredField);
            return missingAmountResult;
        }

        // Validate amount type
        if (amountField.Kind != IntentFieldValueKind.Number || !amountField.NumberValue.HasValue)
        {
            var invalidAmountTypeResult = ValidationResult.Failure(ValidationErrorCode.InvalidFieldType);
            return invalidAmountTypeResult;
        }

        // Validate amount >= 0
        if (amountField.NumberValue.Value < 0)
        {
            var negativeAmountResult = ValidationResult.Failure(ValidationErrorCode.NegativeAmount);
            return negativeAmountResult;
        }

        return ValidationResult.Success;
    }

    private static ValidationResult ValidateResourceDrop(IReadOnlyDictionary<string, IntentFieldValue> fields)
    {
        // Check required field "resourceType"
        if (!fields.TryGetValue(IntentKeys.ResourceType, out var resourceTypeField))
        {
            var missingResourceTypeResult = ValidationResult.Failure(ValidationErrorCode.MissingRequiredField);
            return missingResourceTypeResult;
        }

        // Validate resourceType type
        if (resourceTypeField.Kind != IntentFieldValueKind.Text || string.IsNullOrEmpty(resourceTypeField.TextValue))
        {
            var invalidResourceTypeTypeResult = ValidationResult.Failure(ValidationErrorCode.InvalidFieldType);
            return invalidResourceTypeTypeResult;
        }

        // Validate resourceType value
        if (!ValidResourceTypes.Contains(resourceTypeField.TextValue!))
        {
            var invalidResourceResult = ValidationResult.Failure(ValidationErrorCode.InvalidResourceType);
            return invalidResourceResult;
        }

        // Check required field "amount"
        if (!fields.TryGetValue(IntentKeys.Amount, out var amountField))
        {
            var missingAmountResult = ValidationResult.Failure(ValidationErrorCode.MissingRequiredField);
            return missingAmountResult;
        }

        // Validate amount type
        if (amountField.Kind != IntentFieldValueKind.Number || !amountField.NumberValue.HasValue)
        {
            var invalidAmountTypeResult = ValidationResult.Failure(ValidationErrorCode.InvalidFieldType);
            return invalidAmountTypeResult;
        }

        // Validate amount >= 0
        if (amountField.NumberValue.Value < 0)
        {
            var negativeAmountResult = ValidationResult.Failure(ValidationErrorCode.NegativeAmount);
            return negativeAmountResult;
        }

        return ValidationResult.Success;
    }
}
