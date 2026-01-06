using MongoDB.Bson;

namespace ScreepsDotNet.Storage.MongoRedis.Extensions;

internal static class ModuleDictionaryExtensions
{
    public static IDictionary<string, string> ToMutableModuleDictionary(this IReadOnlyDictionary<string, string> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        return modules as IDictionary<string, string> ?? new Dictionary<string, string>(modules, StringComparer.Ordinal);
    }

    public static BsonDocument ToModulesDocument(this IDictionary<string, string> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var document = new BsonDocument();
        foreach (var module in modules)
            document[module.Key] = module.Value;

        return document;
    }
}
