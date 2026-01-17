using System.Text.Json;

namespace ScreepsDotNet.Driver.Extensions;

public static class JsonElementExtensions
{
    public static bool TryGetBooleanProperty(this JsonElement element, string propertyName, out bool value)
    {
        var propertyExists = element.TryGetProperty(propertyName, out var child);
        if (propertyExists) return TryGetBooleanCompat(child, out value);

        value = false;
        return false;

    }

    private static bool TryGetBooleanCompat(JsonElement element, out bool value)
    {
        switch (element.ValueKind) {
            case JsonValueKind.True:
                value = true;
                return true;
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.String:
                if (bool.TryParse(element.GetString(), out value))
                    return true;
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var numeric)) {
                    value = numeric != 0;
                    return true;
                }
                break;
            case JsonValueKind.Undefined:
                break;
            case JsonValueKind.Object:
                break;
            case JsonValueKind.Array:
                break;
            case JsonValueKind.Null:
                break;
            default:
                break;
        }

        value = false;
        return false;
    }
}
