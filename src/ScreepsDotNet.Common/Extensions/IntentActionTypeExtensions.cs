using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;

namespace ScreepsDotNet.Common.Extensions;

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
