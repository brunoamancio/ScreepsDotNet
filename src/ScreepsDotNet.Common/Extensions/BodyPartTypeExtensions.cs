using ScreepsDotNet.Common.Types;

namespace ScreepsDotNet.Common.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;

public static class BodyPartTypeExtensions
{
    private static readonly Dictionary<BodyPartType, string> ToDocumentValueMap = new()
    {
        [BodyPartType.Move] = "move",
        [BodyPartType.Work] = "work",
        [BodyPartType.Carry] = "carry",
        [BodyPartType.Attack] = "attack",
        [BodyPartType.RangedAttack] = "ranged_attack",
        [BodyPartType.Tough] = "tough",
        [BodyPartType.Heal] = "heal",
        [BodyPartType.Claim] = "claim"
    };

    private static readonly Dictionary<string, BodyPartType> FromDocumentValueMap =
        ToDocumentValueMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    public static string ToDocumentValue(this BodyPartType bodyPartType)
        => ToDocumentValueMap.TryGetValue(bodyPartType, out var value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(bodyPartType), bodyPartType, null);

    public static BodyPartType ToBodyPartType(this string value)
        => FromDocumentValueMap.TryGetValue(value, out var bodyPartType)
            ? bodyPartType
            : throw new ArgumentException($"Unknown body part type: {value}", nameof(value));

    public static bool TryParseBodyPartType(this string value, out BodyPartType bodyPartType)
        => FromDocumentValueMap.TryGetValue(value, out bodyPartType);
}
