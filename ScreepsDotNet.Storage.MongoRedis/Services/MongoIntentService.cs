namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Intents;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoIntentService : IIntentService
{
    private const string ActivateSafeModeIntent = "activateSafeMode";

    private static readonly HashSet<string> SupportedRoomPrefixes = new(StringComparer.OrdinalIgnoreCase) { "W", "E" };
    private static readonly HashSet<string> BodyPartNames = new(StringComparer.OrdinalIgnoreCase)
    {
        BodyPartType.Move.ToDocumentValue(),
        BodyPartType.Work.ToDocumentValue(),
        BodyPartType.Carry.ToDocumentValue(),
        BodyPartType.Attack.ToDocumentValue(),
        BodyPartType.RangedAttack.ToDocumentValue(),
        BodyPartType.Tough.ToDocumentValue(),
        BodyPartType.Heal.ToDocumentValue(),
        BodyPartType.Claim.ToDocumentValue()
    };

    private readonly IMongoCollection<RoomIntentDocument> _roomIntentsCollection;
    private readonly IMongoCollection<UserIntentDocument> _userIntentsCollection;
    private readonly IMongoCollection<RoomDocument> _roomsCollection;
    private readonly IMongoCollection<RoomObjectDocument> _roomObjectsCollection;
    private readonly IWorldMetadataRepository _worldMetadataRepository;
    private readonly ILogger<MongoIntentService> _logger;

    public MongoIntentService(IMongoDatabaseProvider databaseProvider,
                              IWorldMetadataRepository worldMetadataRepository,
                              ILogger<MongoIntentService> logger)
    {
        ArgumentNullException.ThrowIfNull(databaseProvider);
        ArgumentNullException.ThrowIfNull(worldMetadataRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _roomIntentsCollection = databaseProvider.GetCollection<RoomIntentDocument>(databaseProvider.Settings.RoomsIntentsCollection);
        _userIntentsCollection = databaseProvider.GetCollection<UserIntentDocument>(databaseProvider.Settings.UsersIntentsCollection);
        _roomsCollection = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);
        _roomObjectsCollection = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
        _worldMetadataRepository = worldMetadataRepository;
        _logger = logger;
    }

    public async Task AddObjectIntentAsync(string roomName,
                                           string objectId,
                                           string intentName,
                                           JsonElement payload,
                                           string userId,
                                           CancellationToken cancellationToken = default)
    {
        ValidateCommonArguments(roomName, objectId, intentName, payload, userId);

        var normalizedRoom = roomName.Trim();
        await EnsureRoomIsPlayableAsync(normalizedRoom, cancellationToken).ConfigureAwait(false);

        if (string.Equals(intentName, ActivateSafeModeIntent, StringComparison.Ordinal))
            await EnsureSafeModeAvailableAsync(userId, cancellationToken).ConfigureAwait(false);

        var sanitized = SanitizeIntent(intentName, payload, forceArray: false);

        var filter = Builders<RoomIntentDocument>.Filter.Eq(document => document.Room, normalizedRoom);
        var update = Builders<RoomIntentDocument>.Update
                                                 .Set($"users.{userId}.objectsManual.{objectId}", sanitized)
                                                 .SetOnInsert(document => document.Room, normalizedRoom);
        await _roomIntentsCollection.UpdateOneAsync(filter,
                                                    update,
                                                    new UpdateOptions { IsUpsert = true },
                                                    cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Queued object intent {Intent} for user {UserId} in room {Room} (object {ObjectId}).",
            intentName, userId, normalizedRoom, objectId);
    }

    public async Task AddGlobalIntentAsync(string intentName,
                                           JsonElement payload,
                                           string userId,
                                           CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(intentName))
            throw new IntentValidationException("invalid params");
        if (string.IsNullOrWhiteSpace(userId))
            throw new IntentValidationException("unauthorized");
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new IntentValidationException("invalid params");

        var sanitized = SanitizeIntent(intentName, payload, forceArray: true);

        var document = new UserIntentDocument
        {
            UserId = userId,
            Intents = sanitized
        };

        await _userIntentsCollection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Queued global intent {Intent} for user {UserId}.", intentName, userId);
    }

    private async Task EnsureRoomIsPlayableAsync(string roomName, CancellationToken cancellationToken)
    {
        var normalizedRoom = roomName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRoom))
            throw new IntentValidationException("invalid room");

        var prefix = normalizedRoom[..1];
        if (!SupportedRoomPrefixes.Contains(prefix))
            throw new IntentValidationException("not supported");

        var room = await _roomsCollection.Find(document => document.Id == normalizedRoom)
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false) ?? throw new IntentValidationException("invalid room");
        var isOutOfBorders = string.Equals(room.Status, "out of borders", StringComparison.OrdinalIgnoreCase);
        var stillClosed = room.OpenTime.HasValue && room.OpenTime.Value > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (isOutOfBorders || stillClosed)
            throw new IntentValidationException("out of borders");
    }

    private async Task EnsureSafeModeAvailableAsync(string userId, CancellationToken cancellationToken)
    {
        var gameTime = await _worldMetadataRepository.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);
        var filter = Builders<RoomObjectDocument>.Filter.And(
            Builders<RoomObjectDocument>.Filter.Eq(document => document.Type, RoomObjectType.Controller.ToDocumentValue()),
            Builders<RoomObjectDocument>.Filter.Eq(document => document.UserId, userId),
            Builders<RoomObjectDocument>.Filter.Gt(document => document.SafeMode, gameTime)
        );

        var existing = await _roomObjectsCollection.Find(filter)
                                                   .Limit(1)
                                                   .FirstOrDefaultAsync(cancellationToken)
                                                   .ConfigureAwait(false);

        if (existing is not null)
            throw new IntentValidationException("safe mode active already");
    }

    private static void ValidateCommonArguments(string roomName, string objectId, string intentName, JsonElement payload, string userId)
    {
        if (string.IsNullOrWhiteSpace(roomName) ||
            string.IsNullOrWhiteSpace(objectId) ||
            string.IsNullOrWhiteSpace(intentName))
            throw new IntentValidationException("invalid params");

        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new IntentValidationException("invalid params");

        if (string.IsNullOrWhiteSpace(userId))
            throw new IntentValidationException("unauthorized");
    }

    private static BsonDocument SanitizeIntent(string intentName, JsonElement payload, bool forceArray)
    {
        if (!IntentSchemas.All.TryGetValue(intentName, out var definition))
            throw new IntentValidationException("invalid intent name");

        var value = SanitizeIntentValue(definition, payload, forceArray);
        return new BsonDocument(intentName, value);
    }

    private static BsonValue SanitizeIntentValue(IntentDefinition definition, JsonElement payload, bool forceArray)
    {
        if (forceArray || payload.ValueKind == JsonValueKind.Array) {
            var array = new BsonArray();
            var items = payload.ValueKind == JsonValueKind.Array ? payload.EnumerateArray() : EnumerateSingleton(payload);
            foreach (var item in items)
                array.Add(SanitizeIntentObject(definition, item));
            return array;
        }

        return SanitizeIntentObject(definition, payload);
    }

    private static IEnumerable<JsonElement> EnumerateSingleton(JsonElement element)
    {
        yield return element;
    }

    private static BsonDocument SanitizeIntentObject(IntentDefinition definition, JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw new IntentValidationException("intent must be an object");

        var document = new BsonDocument();
        foreach (var field in definition.Fields) {
            if (!payload.TryGetProperty(field.Key, out var property))
                throw new IntentValidationException($"missing field '{field.Key}' for intent '{definition.Name}'");

            document[field.Key] = ConvertField(field.Value, property);
        }

        return document;
    }

    private static BsonValue ConvertField(IntentFieldType type, JsonElement element)
        => type switch
        {
            IntentFieldType.ScalarString => BsonValue.Create(ConvertToFlexibleString(element)),
            IntentFieldType.ScalarNumber => new BsonInt32(ConvertToInt(element)),
            IntentFieldType.ScalarBoolean => BsonValue.Create(ConvertToBoolean(element)),
            IntentFieldType.Price => new BsonInt32(ConvertToPrice(element)),
            IntentFieldType.StringArray => ConvertToStringArray(element),
            IntentFieldType.NumberArray => ConvertToNumberArray(element),
            IntentFieldType.BodyPartArray => ConvertToBodyPartArray(element),
            IntentFieldType.UserString => BsonValue.Create(ConvertToUserString(element)),
            IntentFieldType.UserText => BsonValue.Create(ConvertToUserText(element)),
            _ => throw new IntentValidationException("invalid field type")
        };

    private static string ConvertToFlexibleString(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };

    private static string ConvertToUserString(JsonElement element)
    {
        var value = ConvertToFlexibleString(element);
        return value.Length <= 100 ? value : value[..100];
    }

    private static string ConvertToUserText(JsonElement element)
    {
        var value = ConvertToFlexibleString(element);
        return value.Length <= 1000 ? value : value[..1000];
    }

    private static int ConvertToInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number) {
            if (element.TryGetInt32(out var intValue))
                return intValue;

            var doubleValue = element.GetDouble();
            return (int)Math.Truncate(doubleValue);
        }

        var text = ConvertToFlexibleString(element);
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        throw new IntentValidationException("invalid number");
    }

    private static bool ConvertToBoolean(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => Math.Abs(element.GetDouble()) > double.Epsilon,
            JsonValueKind.String => !string.IsNullOrEmpty(element.GetString()),
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.Object or JsonValueKind.Array => true,
            _ => false
        };

    private static int ConvertToPrice(JsonElement element)
    {
        double value;
        if (element.ValueKind == JsonValueKind.Number)
            value = element.GetDouble();
        else if (!double.TryParse(ConvertToFlexibleString(element), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            throw new IntentValidationException("invalid price");

        return (int)Math.Round(value * 1000, MidpointRounding.AwayFromZero);
    }

    private static BsonArray ConvertToStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new IntentValidationException("expected string array");

        var result = new BsonArray();
        foreach (var item in element.EnumerateArray())
            result.Add(ConvertToFlexibleString(item));
        return result;
    }

    private static BsonArray ConvertToNumberArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new IntentValidationException("expected number array");

        var result = new BsonArray();
        foreach (var item in element.EnumerateArray())
            result.Add(ConvertToInt(item));
        return result;
    }

    private static BsonArray ConvertToBodyPartArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return [];

        var result = new BsonArray();
        foreach (var item in element.EnumerateArray()) {
            var part = ConvertToFlexibleString(item);
            if (BodyPartNames.Contains(part))
                result.Add(part);
        }

        return result;
    }
}
