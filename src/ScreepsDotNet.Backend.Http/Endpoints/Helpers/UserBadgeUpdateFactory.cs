namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Http.Endpoints.Models;

internal static partial class UserBadgeUpdateFactory
{
    public const string InvalidBadgeParamsMessage = "invalid params";

    private static readonly Regex BadgeColorRegex = BuildBadgeColorRegex();

    public static bool TryCreate(UserBadgePayload? payload, UserProfile user, out UserBadgeUpdate? badgeUpdate, out string? errorMessage)
    {
        badgeUpdate = null;
        errorMessage = InvalidBadgeParamsMessage;

        if (payload?.Param == null || double.IsNaN(payload.Param.Value) || payload.Param.Value < -100 || payload.Param.Value > 100)
            return false;

        if (!IsValidBadgeColor(payload.Color1) || !IsValidBadgeColor(payload.Color2) || !IsValidBadgeColor(payload.Color3))
            return false;

        var typeElement = payload.Type;
        if (typeElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return false;

        var typeValue = typeElement.ValueKind switch
        {
            JsonValueKind.Number when typeElement.TryGetInt32(out var typeInt) && typeInt is >= 1 and <= 24 => typeInt,
            JsonValueKind.Object when TryCreateCustomBadge(typeElement, user.CustomBadge, out var badgeObject) => badgeObject,
            JsonValueKind.Undefined => throw new NotImplementedException(),
            JsonValueKind.Array => throw new NotImplementedException(),
            JsonValueKind.String => throw new NotImplementedException(),
            JsonValueKind.True => throw new NotImplementedException(),
            JsonValueKind.False => throw new NotImplementedException(),
            JsonValueKind.Null => throw new NotImplementedException(),
            _ => null
        };

        if (typeValue is null)
            return false;

        badgeUpdate = new UserBadgeUpdate(typeValue, payload.Color1!, payload.Color2!, payload.Color3!, payload.Param.Value, payload.Flip ?? false);
        errorMessage = null;
        return true;
    }

    private static bool IsValidBadgeColor(string? color)
        => !string.IsNullOrWhiteSpace(color) && BadgeColorRegex.IsMatch(color);

    private static bool TryCreateCustomBadge(JsonElement element, object? customBadge, out object? badge)
    {
        badge = ConvertJsonElementToDotNet(element);
        if (badge is null || customBadge is null)
            return false;

        var candidateJson = JsonSerializer.Serialize(badge);
        var customBadgeJson = JsonSerializer.Serialize(customBadge);
        return string.Equals(candidateJson, customBadgeJson, StringComparison.Ordinal);
    }

    private static object? ConvertJsonElementToDotNet(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonObject(element),
            JsonValueKind.Array => ConvertJsonArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Undefined => throw new NotImplementedException(),
            JsonValueKind.Null => throw new NotImplementedException(),
            _ => null
        };

    private static IDictionary<string, object?> ConvertJsonObject(JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            dictionary[property.Name] = ConvertJsonElementToDotNet(property.Value);
        return dictionary;
    }

    private static IList<object?> ConvertJsonArray(JsonElement element)
        => element.EnumerateArray().Select(ConvertJsonElementToDotNet).ToList();

    [GeneratedRegex("^#[0-9a-f]{6}$", RegexOptions.IgnoreCase)]
    private static partial Regex BuildBadgeColorRegex();
}
