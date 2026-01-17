namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

/// <summary>
/// Canonical names for global intent payloads coming from driver snapshots.
/// </summary>
public static class GlobalIntentTypes
{
    public const string CreateOrder = "createOrder";
    public const string CancelOrder = "cancelOrder";
    public const string ChangeOrderPrice = "changeOrderPrice";
    public const string ExtendOrder = "extendOrder";
    public const string Deal = "deal";

    public const string SpawnPowerCreep = "spawnPowerCreep";
    public const string SuicidePowerCreep = "suicidePowerCreep";
    public const string DeletePowerCreep = "deletePowerCreep";
    public const string UpgradePowerCreep = "upgradePowerCreep";
    public const string CreatePowerCreep = "createPowerCreep";
    public const string RenamePowerCreep = "renamePowerCreep";
}
