namespace ScreepsDotNet.Engine.Tests.Parity.Comparison;

using System.Text.Json;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Compares Node.js and .NET engine outputs to detect behavioral divergences
/// </summary>
public static class ParityComparator
{
    public static ParityComparisonResult Compare(ParityTestOutput dotnetOutput, JsonDocument nodeOutput)
    {
        var divergences = new List<ParityDivergence>();

        // Phase 3.1: Compare mutations
        divergences.AddRange(CompareMutations(dotnetOutput, nodeOutput));

        // Phase 3.1: Compare stats (TEMPORARILY DISABLED - stats recording deferred in Node.js harness)
        // Stats comparison is deferred per tools/parity-harness/README.md line 42
        // TODO: Re-enable once Node.js harness implements stats capture
        // When re-enabling, also update ParityComparatorTests.cs:
        //   - Line 92: Change Assert.Equal(2, ...) to Assert.Equal(3, ...)
        //   - Lines 103-106: Uncomment stat divergence assertions
        //   - Line 184: Change "Divergences (2)" to "Divergences (3)"
        //   - Line 223: Change "2 divergence(s)" to "3 divergence(s)"
        //   - Line 226: Uncomment Assert.Contains("Stats: 1", ...)
        // divergences.AddRange(CompareStats(dotnetOutput, nodeOutput));

        // Phase 3+: Compare action logs (future)
        // Phase 3+: Compare final state (future)

        return new ParityComparisonResult(divergences);
    }

    private static IEnumerable<ParityDivergence> CompareMutations(ParityTestOutput dotnetOutput, JsonDocument nodeOutput)
    {
        // Extract Node.js mutations
        if (!nodeOutput.RootElement.TryGetProperty("mutations", out var nodeMutations)) {
            yield return new ParityDivergence(
                "mutations",
                null,
                dotnetOutput.MutationWriter,
                "Node.js output missing 'mutations' property",
                DivergenceCategory.Mutation
            );
            yield break;
        }

        // Compare patches
        if (nodeMutations.TryGetProperty("patches", out var nodePatches)) {
            var nodePatchMap = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var patch in nodePatches.EnumerateArray()) {
                if (patch.TryGetProperty("objectId", out var objectId)) {
                    nodePatchMap[objectId.GetString()!] = patch;
                }
            }

            // Build .NET patch map manually to handle duplicates (last write wins)
            var dotnetPatchMap = new Dictionary<string, RoomObjectPatchPayload>(StringComparer.Ordinal);
            foreach (var (objectId, payload) in dotnetOutput.MutationWriter.Patches) {
                dotnetPatchMap[objectId] = payload;
            }

            // Check for patches in .NET but not in Node.js
            foreach (var (objectId, _) in dotnetPatchMap) {
                if (!nodePatchMap.ContainsKey(objectId)) {
                    yield return new ParityDivergence(
                        $"mutations.patches[{objectId}]",
                        null,
                        objectId,
                        "Patch exists in .NET but not in Node.js",
                        DivergenceCategory.Mutation
                    );
                }
            }

            // Check for patches in Node.js but not in .NET
            foreach (var (objectId, nodePatch) in nodePatchMap) {
                if (!dotnetPatchMap.TryGetValue(objectId, out var dotnetPatch)) {
                    yield return new ParityDivergence(
                        $"mutations.patches[{objectId}]",
                        objectId,
                        null,
                        "Patch exists in Node.js but not in .NET",
                        DivergenceCategory.Mutation
                    );
                    continue;
                }

                // Compare store (most common mutation)
                if (nodePatch.TryGetProperty("store", out var nodeStore) && dotnetPatch.Store is not null) {
                    foreach (var storeProp in nodeStore.EnumerateObject()) {
                        var resource = storeProp.Name;
                        // Handle null values from Node.js (represents 0 or absent resource)
                        var nodeAmount = storeProp.Value.ValueKind == JsonValueKind.Null ? 0 : storeProp.Value.GetInt32();
                        var dotnetAmount = dotnetPatch.Store.GetValueOrDefault(resource, 0);

                        if (nodeAmount != dotnetAmount) {
                            yield return new ParityDivergence(
                                $"mutations.patches[{objectId}].store.{resource}",
                                nodeAmount,
                                dotnetAmount,
                                $"Store amount differs: Node.js={nodeAmount}, .NET={dotnetAmount}",
                                DivergenceCategory.Mutation
                            );
                        }
                    }
                }

                // Compare energy
                if (nodePatch.TryGetProperty("energy", out var nodeEnergy) && dotnetPatch.Energy.HasValue) {
                    var nodeEnergyValue = nodeEnergy.GetInt32();
                    var dotnetEnergyValue = dotnetPatch.Energy.Value;

                    if (nodeEnergyValue != dotnetEnergyValue) {
                        yield return new ParityDivergence(
                            $"mutations.patches[{objectId}].energy",
                            nodeEnergyValue,
                            dotnetEnergyValue,
                            $"Energy differs: Node.js={nodeEnergyValue}, .NET={dotnetEnergyValue}",
                            DivergenceCategory.Mutation
                        );
                    }
                }

                // Compare hits
                if (nodePatch.TryGetProperty("hits", out var nodeHits) && dotnetPatch.Hits.HasValue) {
                    var nodeHitsValue = nodeHits.GetInt32();
                    var dotnetHitsValue = dotnetPatch.Hits.Value;

                    if (nodeHitsValue != dotnetHitsValue) {
                        yield return new ParityDivergence(
                            $"mutations.patches[{objectId}].hits",
                            nodeHitsValue,
                            dotnetHitsValue,
                            $"Hits differs: Node.js={nodeHitsValue}, .NET={dotnetHitsValue}",
                            DivergenceCategory.Mutation
                        );
                    }
                }
            }
        }
    }

    private static IEnumerable<ParityDivergence> CompareStats(ParityTestOutput dotnetOutput, JsonDocument nodeOutput)
    {
        if (!nodeOutput.RootElement.TryGetProperty("stats", out var nodeStats)) {
            yield return new ParityDivergence(
                "stats",
                null,
                dotnetOutput.StatsSink,
                "Node.js output missing 'stats' property",
                DivergenceCategory.Stats
            );
            yield break;
        }

        // Build flat stat dictionary from Node.js output (format: "userId.statName" => value)
        var nodeStatsMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var stat in nodeStats.EnumerateObject()) {
            nodeStatsMap[stat.Name] = stat.Value.GetInt32();
        }

        // Build flat stat dictionary from .NET output
        var dotnetStatsMap = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (userId, amount) in dotnetOutput.StatsSink.EnergyHarvested) {
            dotnetStatsMap[$"{userId}.energyHarvested"] = amount;
        }

        foreach (var (userId, amount) in dotnetOutput.StatsSink.EnergyControl) {
            dotnetStatsMap[$"{userId}.energyControl"] = amount;
        }

        foreach (var (userId, amount) in dotnetOutput.StatsSink.EnergyCreeps) {
            dotnetStatsMap[$"{userId}.energyCreeps"] = amount;
        }

        foreach (var (userId, amount) in dotnetOutput.StatsSink.EnergyConstruction) {
            dotnetStatsMap[$"{userId}.energyConstruction"] = amount;
        }

        foreach (var (userId, count) in dotnetOutput.StatsSink.CreepsLost) {
            dotnetStatsMap[$"{userId}.creepsLost"] = count;
        }

        foreach (var (userId, count) in dotnetOutput.StatsSink.CreepsProduced) {
            dotnetStatsMap[$"{userId}.creepsProduced"] = count;
        }

        foreach (var (userId, count) in dotnetOutput.StatsSink.SpawnRenewals) {
            dotnetStatsMap[$"{userId}.spawnRenewals"] = count;
        }

        foreach (var (userId, count) in dotnetOutput.StatsSink.SpawnRecycles) {
            dotnetStatsMap[$"{userId}.spawnRecycles"] = count;
        }

        foreach (var (userId, count) in dotnetOutput.StatsSink.SpawnCreates) {
            dotnetStatsMap[$"{userId}.spawnCreates"] = count;
        }

        foreach (var (userId, count) in dotnetOutput.StatsSink.TombstonesCreated) {
            dotnetStatsMap[$"{userId}.tombstonesCreated"] = count;
        }

        foreach (var (userId, amount) in dotnetOutput.StatsSink.PowerProcessed) {
            dotnetStatsMap[$"{userId}.powerProcessed"] = amount;
        }

        // Compare stats
        var allStatKeys = nodeStatsMap.Keys.Concat(dotnetStatsMap.Keys).Distinct(StringComparer.Ordinal);

        foreach (var statKey in allStatKeys) {
            var nodeValue = nodeStatsMap.GetValueOrDefault(statKey, 0);
            var dotnetValue = dotnetStatsMap.GetValueOrDefault(statKey, 0);

            if (nodeValue != dotnetValue) {
                yield return new ParityDivergence(
                    $"stats.{statKey}",
                    nodeValue,
                    dotnetValue,
                    $"Stat differs: Node.js={nodeValue}, .NET={dotnetValue}",
                    DivergenceCategory.Stats
                );
            }
        }
    }
}
