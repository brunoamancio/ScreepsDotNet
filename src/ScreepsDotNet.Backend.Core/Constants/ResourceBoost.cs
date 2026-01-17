namespace ScreepsDotNet.Backend.Core.Constants;

public enum ResourceBoost
{
    LO,
    KO,
    ZH,
    UH
}

public static class ResourceBoostExtensions
{
    private static readonly Dictionary<ResourceBoost, string> ToDocumentValueMap = new()
    {
        [ResourceBoost.LO] = "LO",
        [ResourceBoost.KO] = "KO",
        [ResourceBoost.ZH] = "ZH",
        [ResourceBoost.UH] = "UH"
    };

    private static readonly Dictionary<string, ResourceBoost> FromDocumentValueMap = ToDocumentValueMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.Ordinal);

    public static string ToDocumentValue(this ResourceBoost boost)
        => ToDocumentValueMap.TryGetValue(boost, out var value) ? value : throw new ArgumentOutOfRangeException(nameof(boost), boost, null);

    public static ResourceBoost ToResourceBoost(this string value)
        => FromDocumentValueMap.TryGetValue(value, out var boost) ? boost : throw new ArgumentException($"Unknown resource boost: {value}", nameof(value));

    public static bool TryParseResourceBoost(this string value, out ResourceBoost boost)
        => FromDocumentValueMap.TryGetValue(value, out boost);
}
