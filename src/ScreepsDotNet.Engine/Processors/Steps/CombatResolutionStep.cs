namespace ScreepsDotNet.Engine.Processors.Steps;

using System.Collections.Generic;
using System.Text.Json;
using ScreepsDotNet.Common;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Resolves simple attack/ranged attack intents by applying flat damage to targets.
/// </summary>
internal sealed class CombatResolutionStep : IRoomProcessorStep
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var hitsUpdates = new Dictionary<string, int>();
        var removals = new HashSet<string>(StringComparer.Ordinal);

        foreach (var envelope in intents.Users.Values)
        {
            if (envelope?.ObjectsManualJson is null)
                continue;

            foreach (var payloadJson in envelope.ObjectsManualJson.Values)
            {
                if (string.IsNullOrWhiteSpace(payloadJson))
                    continue;

                using var doc = JsonDocument.Parse(payloadJson);
                ApplyAttack(doc.RootElement, IntentActionType.Attack.ToKey(), hitsUpdates, removals);
                ApplyAttack(doc.RootElement, IntentActionType.RangedAttack.ToKey(), hitsUpdates, removals);
            }
        }

        foreach (var (objectId, hits) in hitsUpdates)
        {
            if (!context.State.Objects.TryGetValue(objectId, out var obj))
                continue;

            var remaining = Math.Max((obj.Hits ?? 0) - hits, 0);
            if (remaining == 0)
            {
                removals.Add(objectId);
                continue;
            }

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["hits"] = remaining
            }, _jsonOptions);
            context.MutationWriter.PatchJson(objectId, json);
        }

        foreach (var id in removals)
            context.MutationWriter.Remove(id);

        return Task.CompletedTask;
    }

    private static void ApplyAttack(JsonElement root, string key, IDictionary<string, int> hitsUpdates, ISet<string> removals)
    {
        if (!root.TryGetProperty(key, out var attackElement))
            return;

        if (!attackElement.TryGetProperty("targetId", out var targetElement))
            return;

        var targetId = targetElement.GetString();
        if (string.IsNullOrWhiteSpace(targetId))
            return;

        var damage = attackElement.TryGetProperty("damage", out var dmgElement) ? dmgElement.GetInt32() : 30;
        hitsUpdates[targetId!] = hitsUpdates.TryGetValue(targetId!, out var existing) ? existing + damage : damage;
    }
}
