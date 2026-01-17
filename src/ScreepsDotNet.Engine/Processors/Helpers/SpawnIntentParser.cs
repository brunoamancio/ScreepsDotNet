namespace ScreepsDotNet.Engine.Processors.Helpers;

using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

internal interface ISpawnIntentParser
{
    SpawnIntentParseResult Parse(SpawnIntentEnvelope? envelope);
}

internal sealed class SpawnIntentParser(IBodyAnalysisHelper bodyAnalysisHelper) : ISpawnIntentParser
{
    public SpawnIntentParseResult Parse(SpawnIntentEnvelope? envelope)
    {
        if (envelope is null)
            return SpawnIntentParseResult.CreateSuccess();

        ParsedCreateCreepIntent? create = null;
        ParsedRenewIntent? renew = null;
        ParsedRecycleIntent? recycle = null;
        ParsedSetDirectionsIntent? setDirections = null;
        var cancel = envelope.CancelSpawning;

        if (envelope.CreateCreep is not null) {
            var createResult = ValidateCreate(envelope.CreateCreep);
            if (!createResult.Success)
                return SpawnIntentParseResult.Failure(createResult.Error!);

            create = createResult.CreateIntent!;
        }

        if (envelope.RenewCreep is not null) {
            if (string.IsNullOrWhiteSpace(envelope.RenewCreep.TargetId))
                return SpawnIntentParseResult.Failure("Renew intent missing target id.");

            renew = new ParsedRenewIntent(envelope.RenewCreep.TargetId);
        }

        if (envelope.RecycleCreep is not null) {
            if (string.IsNullOrWhiteSpace(envelope.RecycleCreep.TargetId))
                return SpawnIntentParseResult.Failure("Recycle intent missing target id.");

            recycle = new ParsedRecycleIntent(envelope.RecycleCreep.TargetId);
        }

        if (envelope.SetSpawnDirections is not null) {
            var directions = SanitizeDirections(envelope.SetSpawnDirections.Directions);
            if (directions.Count == 0)
                return SpawnIntentParseResult.Failure("Spawn directions must contain values between 1 and 8.");

            setDirections = new ParsedSetDirectionsIntent(directions);
        }

        return SpawnIntentParseResult.CreateSuccess(create, renew, recycle, setDirections, cancel);
    }

    private CreateValidationResult ValidateCreate(CreateCreepIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.Name))
            return CreateValidationResult.Failure("Creep name is required.");

        if (intent.Name.Length > 100)
            return CreateValidationResult.Failure("Creep name exceeds 100 characters.");

        var analysis = bodyAnalysisHelper.Analyze(intent.BodyParts ?? []);
        if (!analysis.Success)
            return CreateValidationResult.Failure(analysis.Error ?? "Invalid body parts.");

        var directions = SanitizeDirections(intent.Directions);
        var structureIds = SanitizeIds(intent.EnergyStructureIds);

        var createIntent = new ParsedCreateCreepIntent(
            intent.Name,
            analysis,
            directions,
            structureIds);

        return CreateValidationResult.CreateSuccess(createIntent);
    }

    private static IReadOnlyList<Direction> SanitizeDirections(IReadOnlyList<Direction>? directions)
    {
        if (directions is null || directions.Count == 0)
            return [];

        var filtered = directions.Distinct().ToArray();
        return filtered.Length == 0 ? [] : filtered;
    }

    private static IReadOnlyList<string> SanitizeIds(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
            return [];

        var filtered = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return filtered.Length == 0 ? [] : filtered;
    }

    private sealed record CreateValidationResult(bool Success, string? Error, ParsedCreateCreepIntent? CreateIntent)
    {
        public static CreateValidationResult Failure(string error) => new(false, error, null);
        public static CreateValidationResult CreateSuccess(ParsedCreateCreepIntent create) => new(true, null, create);
    }
}

internal sealed record SpawnIntentParseResult(
    bool Success,
    string? Error,
    ParsedCreateCreepIntent? CreateIntent,
    ParsedRenewIntent? RenewIntent,
    ParsedRecycleIntent? RecycleIntent,
    ParsedSetDirectionsIntent? DirectionsIntent,
    bool CancelSpawning)
{
    public bool HasIntents =>
        CreateIntent is not null ||
        RenewIntent is not null ||
        RecycleIntent is not null ||
        DirectionsIntent is not null ||
        CancelSpawning;

    public static SpawnIntentParseResult Failure(string error)
        => new(false, error, null, null, null, null, false);

    public static SpawnIntentParseResult CreateSuccess(
        ParsedCreateCreepIntent? create = null,
        ParsedRenewIntent? renew = null,
        ParsedRecycleIntent? recycle = null,
        ParsedSetDirectionsIntent? directions = null,
        bool cancel = false)
        => new(true, null, create, renew, recycle, directions, cancel);
}

internal sealed record ParsedCreateCreepIntent(
    string Name,
    BodyAnalysisResult Body,
    IReadOnlyList<Direction> Directions,
    IReadOnlyList<string> EnergyStructureIds);

internal sealed record ParsedRenewIntent(string TargetId);

internal sealed record ParsedRecycleIntent(string TargetId);

internal sealed record ParsedSetDirectionsIntent(IReadOnlyList<Direction> Directions);
