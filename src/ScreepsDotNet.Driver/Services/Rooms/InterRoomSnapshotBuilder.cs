namespace ScreepsDotNet.Driver.Services.Rooms;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal sealed class InterRoomSnapshotBuilder(IRoomDataService roomDataService) : IInterRoomSnapshotBuilder
{
    public async Task<GlobalSnapshot> BuildAsync(int gameTime, CancellationToken token = default)
    {
        var snapshot = await roomDataService.GetInterRoomSnapshotAsync(gameTime, token).ConfigureAwait(false);

        var movingCreeps = MapObjects(snapshot.MovingCreeps);
        var accessibleRooms = MapAccessibleRooms(snapshot.AccessibleRooms);
        var specialObjects = MapObjects(snapshot.SpecialRoomObjects);
        var market = MapMarket(snapshot.Market);

        var roomIntents = BuildRoomIntentsFromTerminals(specialObjects);
        return new GlobalSnapshot(snapshot.GameTime, movingCreeps, accessibleRooms, snapshot.ExitTopology, specialObjects, market, roomIntents);
    }

    private static IReadOnlyList<RoomObjectSnapshot> MapObjects(IReadOnlyList<RoomObjectDocument> documents)
    {
        if (documents.Count == 0)
            return [];

        var result = new RoomObjectSnapshot[documents.Count];
        for (var i = 0; i < documents.Count; i++) {
            var document = documents[i];
            result[i] = RoomContractMapper.MapRoomObject(document);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, RoomInfoSnapshot> MapAccessibleRooms(IReadOnlyDictionary<string, RoomDocument> documents)
    {
        if (documents.Count == 0)
            return new Dictionary<string, RoomInfoSnapshot>(0, StringComparer.Ordinal);

        var result = new Dictionary<string, RoomInfoSnapshot>(documents.Count, StringComparer.Ordinal);
        foreach (var (id, document) in documents) {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var info = RoomContractMapper.MapRoomInfo(document);
            if (info is not null)
                result[id] = info;
        }

        return result;
    }

    private static GlobalMarketSnapshot MapMarket(InterRoomMarketSnapshot snapshot)
        => new(
            MapOrders(snapshot.Orders),
            MapUsers(snapshot.Users),
            MapPowerCreeps(snapshot.UserPowerCreeps),
            MapUserIntents(snapshot.UserIntents),
            snapshot.ShardName);

    private static IReadOnlyList<MarketOrderSnapshot> MapOrders(IReadOnlyList<MarketOrderDocument> documents)
    {
        if (documents.Count == 0)
            return [];

        var result = new MarketOrderSnapshot[documents.Count];
        for (var i = 0; i < documents.Count; i++) {
            var doc = documents[i];
            result[i] = new MarketOrderSnapshot(
                doc.Id.ToString(),
                doc.UserId,
                doc.Type,
                doc.RoomName,
                doc.ResourceType,
                doc.Price,
                doc.Amount,
                doc.RemainingAmount,
                doc.TotalAmount,
                doc.CreatedTick,
                doc.CreatedTimestamp,
                doc.Active);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, UserState> MapUsers(IReadOnlyList<UserDocument> users)
    {
        if (users.Count == 0)
            return new Dictionary<string, UserState>(0, StringComparer.Ordinal);

        var dictionary = new Dictionary<string, UserDocument>(users.Count, StringComparer.Ordinal);
        foreach (var document in users) {
            if (string.IsNullOrWhiteSpace(document.Id))
                continue;

            dictionary[document.Id!] = document;
        }

        return RoomContractMapper.MapUsers(dictionary);
    }

    private static IReadOnlyList<PowerCreepSnapshot> MapPowerCreeps(IReadOnlyList<PowerCreepDocument> documents)
    {
        if (documents.Count == 0)
            return [];

        var result = new PowerCreepSnapshot[documents.Count];
        for (var i = 0; i < documents.Count; i++) {
            var doc = documents[i];
            result[i] = new PowerCreepSnapshot(
                doc.Id.ToString(),
                doc.UserId,
                doc.Name,
                doc.ClassName,
                doc.Level,
                doc.HitsMax,
                CopyDictionary(doc.Store),
                doc.StoreCapacity,
                doc.SpawnCooldownTime,
                doc.DeleteTime,
                doc.Shard,
                MapPowers(doc.Powers));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, int> CopyDictionary(Dictionary<string, int>? source)
        => source is null or { Count: 0 }
            ? []
            : new Dictionary<string, int>(source, StringComparer.Ordinal);

    private static IReadOnlyDictionary<PowerTypes, PowerCreepPowerSnapshot> MapPowers(Dictionary<PowerTypes, PowerCreepPowerDocument>? powers)
    {
        if (powers is null || powers.Count == 0)
            return new Dictionary<PowerTypes, PowerCreepPowerSnapshot>();

        var result = new Dictionary<PowerTypes, PowerCreepPowerSnapshot>(powers.Count);
        foreach (var (powerType, document) in powers)
            result[powerType] = new PowerCreepPowerSnapshot(document.Level);

        return result;
    }


    private static IReadOnlyList<GlobalUserIntentSnapshot> MapUserIntents(IReadOnlyList<UserIntentDocument> documents)
    {
        if (documents.Count == 0)
            return [];

        var result = new GlobalUserIntentSnapshot[documents.Count];
        for (var i = 0; i < documents.Count; i++) {
            var doc = documents[i];
            result[i] = new GlobalUserIntentSnapshot(
                doc.Id.ToString(),
                doc.UserId,
                IntentDocumentMapper.MapIntentRecords(doc.Intents));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, RoomIntentSnapshot> BuildRoomIntentsFromTerminals(IReadOnlyList<RoomObjectSnapshot> specialObjects)
    {
        var terminalsByRoom = new Dictionary<string, List<RoomObjectSnapshot>>(StringComparer.Ordinal);

        foreach (var obj in specialObjects) {
            if (obj.Send is null)
                continue;

            if (string.IsNullOrWhiteSpace(obj.RoomName))
                continue;

            if (!terminalsByRoom.TryGetValue(obj.RoomName, out var terminals)) {
                terminals = [];
                terminalsByRoom[obj.RoomName] = terminals;
            }

            terminals.Add(obj);
        }

        if (terminalsByRoom.Count == 0)
            return new Dictionary<string, RoomIntentSnapshot>(StringComparer.Ordinal);

        var roomIntents = new Dictionary<string, RoomIntentSnapshot>(terminalsByRoom.Count, StringComparer.Ordinal);

        foreach (var (roomName, terminals) in terminalsByRoom) {
            var userIntents = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal);

            var terminalsByUser = terminals.GroupBy(t => t.UserId ?? string.Empty, StringComparer.Ordinal);

            foreach (var userGroup in terminalsByUser) {
                var userId = userGroup.Key;
                if (string.IsNullOrWhiteSpace(userId))
                    continue;

                var terminalIntents = new Dictionary<string, TerminalIntentEnvelope>(StringComparer.Ordinal);

                foreach (var terminal in userGroup) {
                    if (terminal.Send is null)
                        continue;

                    var sendIntent = new TerminalSendIntent(
                        terminal.Send.TargetRoomName,
                        terminal.Send.ResourceType,
                        terminal.Send.Amount,
                        terminal.Send.Description);

                    terminalIntents[terminal.Id] = new TerminalIntentEnvelope(sendIntent);
                }

                if (terminalIntents.Count > 0) {
                    var envelope = new IntentEnvelope(
                        userId,
                        new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
                        new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                        new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal),
                        terminalIntents);

                    userIntents[userId] = envelope;
                }
            }

            if (userIntents.Count > 0) {
                var snapshot = new RoomIntentSnapshot(
                    roomName,
                    terminals[0].Shard,
                    userIntents);

                roomIntents[roomName] = snapshot;
            }
        }

        return roomIntents;
    }
}
