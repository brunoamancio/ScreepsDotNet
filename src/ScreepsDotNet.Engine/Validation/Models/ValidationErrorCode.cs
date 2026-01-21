namespace ScreepsDotNet.Engine.Validation.Models;

/// <summary>
/// Error codes for intent validation failures.
/// Maps to silent failure conditions in Node.js intent processors.
/// </summary>
public enum ValidationErrorCode
{
    // Schema Validation (Payload Structure)
    MissingRequiredField,
    InvalidFieldType,
    InvalidEnumValue,
    NegativeAmount,
    InvalidResourceType,

    // State Validation (Object Existence/State)
    ActorNotFound,
    ActorSpawning,
    ActorDead,
    ActorNoStore,
    TargetNotFound,
    TargetSpawning,
    TargetDead,
    TargetNoHits,
    TargetNoStore,
    TargetSameAsActor,
    InvalidTargetType,
    InvalidActorType,
    ControllerUpgradeBlocked,

    // Range Validation (Chebyshev Distance)
    NotInRange,
    ExceedsMaxRange,

    // Permission Validation (Ownership/Access Control)
    NotOwned,
    NotOwnedOrReserved,
    SafeModeActive,
    RampartBlocking,
    HostileRoom,
    ControllerNotOwned,
    ControllerNotReservedByActor,
    PublicRampartRequired,

    // Resource Validation (Availability/Capacity)
    InsufficientEnergy,
    InsufficientResource,
    InsufficientCapacity,
    InsufficientBodyParts,
    TargetCapacityFull,
    TargetHasNoCapacity,
    ActorCapacityFull,
    InvalidBoostMineral,
    LabComponentMismatch,
    FactoryComponentMissing,
    PowerSpawnInsufficientPower,

    // Special Cases
    ControllerLevel8UpgradeLimitReached,
    LinkCooldownActive,
    LabCooldownActive,
    FactoryCooldownActive,
    ConstructionSiteNotFound,
    SourceDepleted,
    MineralDepleted,
    DepositDepleted
}
