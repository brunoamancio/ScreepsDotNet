namespace ScreepsDotNet.Common;

public static class IntentActionTypeExtensions
{
    public static string ToKey(this IntentActionType type) =>
        type switch
        {
            IntentActionType.Attack => IntentKeys.Attack,
            IntentActionType.RangedAttack => IntentKeys.RangedAttack,
            IntentActionType.Heal => IntentKeys.Heal,
            IntentActionType.RangedHeal => IntentKeys.RangedHeal,
            IntentActionType.TransferEnergy => IntentKeys.TransferEnergy,
            IntentActionType.RunReaction => IntentKeys.RunReaction,
            _ => string.Empty
        };
}
