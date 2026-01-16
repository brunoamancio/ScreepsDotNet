namespace ScreepsDotNet.Common.Structures;

using System;
using System.Collections.Generic;

public interface IStructureBlueprintProvider
{
    bool TryGet(string? type, out StructureBlueprint? blueprint);
    StructureBlueprint GetRequired(string type);
    IReadOnlyDictionary<string, StructureBlueprint> GetAll();
}

public sealed class StructureBlueprintProvider : IStructureBlueprintProvider
{
    public bool TryGet(string? type, out StructureBlueprint? blueprint)
        => StructureBlueprintRegistry.TryGetBlueprint(type, out blueprint);

    public StructureBlueprint GetRequired(string type)
    {
        if (TryGet(type, out var blueprint) && blueprint is not null)
            return blueprint;

        throw new ArgumentException($"Unknown structure type '{type}'.", nameof(type));
    }

    public IReadOnlyDictionary<string, StructureBlueprint> GetAll()
        => StructureBlueprintRegistry.GetAll();
}
