namespace ScreepsDotNet.Driver.Abstractions.Shared;

public sealed record CustomObjectPrototype(
    string ObjectType,
    string Name,
    CustomPrototypeOptions Options);

public sealed record CustomPrototypeOptions(
    string? Parent,
    IReadOnlyDictionary<string, string>? Properties,
    string? PrototypeExtender,
    bool UserOwned = false,
    string? FindConstant = null,
    string? LookConstant = null);

public sealed record CustomIntentDefinition(string Name, string SchemaJson);
