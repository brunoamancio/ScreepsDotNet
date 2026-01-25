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

    /// <summary>
    /// Compares multi-room .NET Engine global processor output with Node.js engine multi-room output.
    /// Validates cross-room operations like Terminal.send, Observer.observeRoom, etc.
    /// </summary>
    public static ParityComparisonResult CompareMultiRoom(MultiRoomParityTestOutput dotnetOutput, JsonDocument nodeOutput)
    {
        var divergences = new List<ParityDivergence>();

        // Extract Node.js mutations (multi-room format: mutations[roomName])
        if (!nodeOutput.RootElement.TryGetProperty("mutations", out var nodeMutations)) {
            divergences.Add(new ParityDivergence(
                "mutations",
                null,
                dotnetOutput.GlobalMutationWriter,
                $"Node.js output missing 'mutations' property. Found properties: {string.Join(", ", nodeOutput.RootElement.EnumerateObject().Select(p => p.Name))}",
                DivergenceCategory.Mutation
            ));
            return new ParityComparisonResult(divergences);
        }

        // Compare room object patches across all rooms
        divergences.AddRange(CompareMultiRoomPatches(dotnetOutput.GlobalMutationWriter, nodeMutations));

        // Compare transactions
        divergences.AddRange(CompareTransactions(dotnetOutput.GlobalMutationWriter, nodeOutput));

        // Compare user money adjustments (if present in Node.js output)
        if (nodeOutput.RootElement.TryGetProperty("userMoney", out var nodeUserMoney)) {
            divergences.AddRange(CompareUserMoney(dotnetOutput.GlobalMutationWriter, nodeUserMoney));
        }

        return new ParityComparisonResult(divergences);
    }

    private static IEnumerable<ParityDivergence> CompareMultiRoomPatches(CapturingGlobalMutationWriter dotnetWriter, JsonElement nodeMutations)
    {
        // Build .NET room object patch map (objectId -> patch)
        var dotnetPatchMap = new Dictionary<string, GlobalRoomObjectPatch>(StringComparer.Ordinal);
        foreach (var (objectId, patch) in dotnetWriter.RoomObjectPatches) {
            dotnetPatchMap[objectId] = patch;
        }

        // Build Node.js room object patch map (aggregate across all rooms)
        var nodePatchMap = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var roomProp in nodeMutations.EnumerateObject()) {
            if (roomProp.Value.TryGetProperty("patches", out var roomPatches)) {
                foreach (var patch in roomPatches.EnumerateArray()) {
                    if (patch.TryGetProperty("objectId", out var objectId)) {
                        nodePatchMap[objectId.GetString()!] = patch;
                    }
                }
            }
        }

        // Check for patches in .NET but not in Node.js
        foreach (var (objectId, _) in dotnetPatchMap) {
            if (!nodePatchMap.ContainsKey(objectId)) {
                yield return new ParityDivergence(
                    $"mutations.*.patches[{objectId}]",
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
                    $"mutations.*.patches[{objectId}]",
                    objectId,
                    null,
                    "Patch exists in Node.js but not in .NET",
                    DivergenceCategory.Mutation
                );
                continue;
            }

            // Compare store (most common for Terminal.send)
            if (nodePatch.TryGetProperty("store", out var nodeStore) && dotnetPatch.Store is not null) {
                foreach (var storeProp in nodeStore.EnumerateObject()) {
                    var resource = storeProp.Name;
                    var nodeAmount = storeProp.Value.ValueKind == JsonValueKind.Null ? 0 : storeProp.Value.GetInt32();
                    var dotnetAmount = dotnetPatch.Store.GetValueOrDefault(resource, 0);

                    if (nodeAmount != dotnetAmount) {
                        yield return new ParityDivergence(
                            $"mutations.*.patches[{objectId}].store.{resource}",
                            nodeAmount,
                            dotnetAmount,
                            $"Store amount differs: Node.js={nodeAmount}, .NET={dotnetAmount}",
                            DivergenceCategory.Mutation
                        );
                    }
                }

                // Check for resources in .NET store but not in Node.js
                foreach (var (resource, dotnetAmount) in dotnetPatch.Store) {
                    if (!nodeStore.TryGetProperty(resource, out _) && dotnetAmount != 0) {
                        yield return new ParityDivergence(
                            $"mutations.*.patches[{objectId}].store.{resource}",
                            0,
                            dotnetAmount,
                            $"Store resource exists in .NET but not in Node.js: .NET={dotnetAmount}",
                            DivergenceCategory.Mutation
                        );
                    }
                }
            }

            // Compare cooldownTime (Terminal.send sets this)
            if (nodePatch.TryGetProperty("cooldownTime", out var nodeCooldown) && dotnetPatch.CooldownTime.HasValue) {
                var nodeCooldownValue = nodeCooldown.GetInt32();
                var dotnetCooldownValue = dotnetPatch.CooldownTime.Value;

                if (nodeCooldownValue != dotnetCooldownValue) {
                    yield return new ParityDivergence(
                        $"mutations.*.patches[{objectId}].cooldownTime",
                        nodeCooldownValue,
                        dotnetCooldownValue,
                        $"CooldownTime differs: Node.js={nodeCooldownValue}, .NET={dotnetCooldownValue}",
                        DivergenceCategory.Mutation
                    );
                }
            }
        }
    }

    private static IEnumerable<ParityDivergence> CompareTransactions(CapturingGlobalMutationWriter dotnetWriter, JsonDocument nodeOutput)
    {
        // Extract Node.js transactions (if present)
        if (!nodeOutput.RootElement.TryGetProperty("transactions", out var nodeTransactions)) {
            if (dotnetWriter.Transactions.Count > 0) {
                yield return new ParityDivergence(
                    "transactions",
                    null,
                    dotnetWriter.Transactions,
                    $"Node.js output missing transactions, .NET has {dotnetWriter.Transactions.Count}",
                    DivergenceCategory.Mutation
                );
            }
            yield break;
        }

        var nodeTransactionList = nodeTransactions.EnumerateArray().ToList();

        // Compare transaction counts
        if (dotnetWriter.Transactions.Count != nodeTransactionList.Count) {
            yield return new ParityDivergence(
                "transactions",
                nodeTransactionList.Count,
                dotnetWriter.Transactions.Count,
                $"Transaction count differs: Node.js={nodeTransactionList.Count}, .NET={dotnetWriter.Transactions.Count}",
                DivergenceCategory.Mutation
            );
        }

        // For now, just compare counts. Field-by-field comparison can be added later if needed.
        // Terminal.send creates one transaction per transfer, so count comparison is sufficient for basic validation.
    }

    private static IEnumerable<ParityDivergence> CompareUserMoney(CapturingGlobalMutationWriter dotnetWriter, JsonElement nodeUserMoney)
    {
        // Build .NET user money map
        var dotnetMoneyMap = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (userId, newBalance) in dotnetWriter.UserMoneyAdjustments) {
            dotnetMoneyMap[userId] = newBalance;
        }

        // Compare with Node.js user money
        foreach (var userProp in nodeUserMoney.EnumerateObject()) {
            var userId = userProp.Name;
            var nodeBalance = userProp.Value.GetDouble();

            if (!dotnetMoneyMap.TryGetValue(userId, out var dotnetBalance)) {
                yield return new ParityDivergence(
                    $"userMoney.{userId}",
                    nodeBalance,
                    null,
                    "User money adjustment exists in Node.js but not in .NET",
                    DivergenceCategory.Mutation
                );
                continue;
            }

            if (Math.Abs(nodeBalance - dotnetBalance) > 0.01) {
                yield return new ParityDivergence(
                    $"userMoney.{userId}",
                    nodeBalance,
                    dotnetBalance,
                    $"User money differs: Node.js={nodeBalance}, .NET={dotnetBalance}",
                    DivergenceCategory.Mutation
                );
            }
        }

        // Check for .NET user money not in Node.js
        foreach (var (userId, dotnetBalance) in dotnetMoneyMap) {
            if (!nodeUserMoney.TryGetProperty(userId, out _)) {
                yield return new ParityDivergence(
                    $"userMoney.{userId}",
                    null,
                    dotnetBalance,
                    "User money adjustment exists in .NET but not in Node.js",
                    DivergenceCategory.Mutation
                );
            }
        }
    }
}
