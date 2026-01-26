namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using System.Text.Json.Serialization;

/// <summary>
/// JSON schema for parity test fixtures shared between .NET and Node.js harness.
/// This format matches the structure expected by tools/parity-harness/engine/test-runner/fixture-loader.js
/// </summary>
public sealed record JsonFixture(
    [property: JsonPropertyName("gameTime")] int GameTime,
    [property: JsonPropertyName("room")] string Room,
    [property: JsonPropertyName("shard")] string Shard,
    [property: JsonPropertyName("terrain")] string Terrain,
    [property: JsonPropertyName("objects")] List<JsonRoomObject> Objects,
    [property: JsonPropertyName("intents")] Dictionary<string, Dictionary<string, List<JsonIntent>>> Intents,
    [property: JsonPropertyName("users")] Dictionary<string, JsonUserState> Users);

public sealed record JsonRoomObject(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("user")] string? User = null,
    [property: JsonPropertyName("body")] List<JsonBodyPart>? Body = null,
    [property: JsonPropertyName("store")] Dictionary<string, int>? Store = null,
    [property: JsonPropertyName("storeCapacity")] int? StoreCapacity = null,
    [property: JsonPropertyName("hits")] int? Hits = null,
    [property: JsonPropertyName("hitsMax")] int? HitsMax = null,
    [property: JsonPropertyName("fatigue")] int? Fatigue = null,
    [property: JsonPropertyName("ticksToLive")] int? TicksToLive = null,
    [property: JsonPropertyName("energy")] int? Energy = null,
    [property: JsonPropertyName("energyCapacity")] int? EnergyCapacity = null,
    [property: JsonPropertyName("ticksToRegeneration")] int? TicksToRegeneration = null,
    [property: JsonPropertyName("nextDecayTime")] int? NextDecayTime = null,
    [property: JsonPropertyName("level")] int? Level = null,
    [property: JsonPropertyName("progress")] int? Progress = null,
    [property: JsonPropertyName("progressTotal")] int? ProgressTotal = null,
    [property: JsonPropertyName("cooldown")] int? Cooldown = null,
    [property: JsonPropertyName("cooldownTime")] int? CooldownTime = null,
    [property: JsonPropertyName("mineralAmount")] int? MineralAmount = null,
    [property: JsonPropertyName("mineralType")] string? MineralType = null,
    [property: JsonPropertyName("density")] int? Density = null,
    [property: JsonPropertyName("structureType")] string? StructureType = null);

public sealed record JsonBodyPart(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("hits")] int? Hits = null,
    [property: JsonPropertyName("boost")] string? Boost = null);

public sealed record JsonIntent(
    [property: JsonPropertyName("intent")] string Intent,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("x")] int? X = null,
    [property: JsonPropertyName("y")] int? Y = null,
    [property: JsonPropertyName("amount")] int? Amount = null,
    [property: JsonPropertyName("resourceType")] string? ResourceType = null,
    [property: JsonPropertyName("direction")] int? Direction = null,
    [property: JsonPropertyName("lab1Id")] string? Lab1Id = null,
    [property: JsonPropertyName("lab2Id")] string? Lab2Id = null,
    [property: JsonPropertyName("bodyPartsCount")] int? BodyPartsCount = null,
    [property: JsonPropertyName("targetRoomName")] string? TargetRoomName = null,
    [property: JsonPropertyName("description")] string? Description = null);

public sealed record JsonUserState(
    [property: JsonPropertyName("gcl")] JsonGclState Gcl,
    [property: JsonPropertyName("power")] int Power,
    [property: JsonPropertyName("cpu")] int Cpu,
    [property: JsonPropertyName("cpuAvailable")] int CpuAvailable,
    [property: JsonPropertyName("powerExperimentationTime")] long? PowerExperimentationTime = null);

public sealed record JsonGclState(
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("progress")] int Progress,
    [property: JsonPropertyName("progressTotal")] int ProgressTotal);

/// <summary>
/// Multi-room fixture format for testing cross-room operations like Terminal.send.
/// Uses a dictionary of rooms instead of a single room.
/// </summary>
public sealed record JsonMultiRoomFixture(
    [property: JsonPropertyName("gameTime")] int GameTime,
    [property: JsonPropertyName("shard")] string Shard,
    [property: JsonPropertyName("rooms")] Dictionary<string, JsonRoomData> Rooms,
    [property: JsonPropertyName("intents")] Dictionary<string, Dictionary<string, Dictionary<string, List<JsonIntent>>>> Intents,
    [property: JsonPropertyName("users")] Dictionary<string, JsonUserState> Users,
    [property: JsonPropertyName("globalIntents")] Dictionary<string, JsonGlobalUserIntent>? GlobalIntents = null,
    [property: JsonPropertyName("powerCreeps")] Dictionary<string, JsonPowerCreep>? PowerCreeps = null);

/// <summary>
/// Room data for a single room in a multi-room fixture.
/// Contains terrain and objects for that room.
/// </summary>
public sealed record JsonRoomData(
    [property: JsonPropertyName("terrain")] string Terrain,
    [property: JsonPropertyName("objects")] List<JsonRoomObject> Objects);

/// <summary>
/// Global user intent for power creep operations (create, delete, rename, spawn, etc.)
/// </summary>
public sealed record JsonGlobalUserIntent(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("user")] string User,
    [property: JsonPropertyName("intents")] List<JsonGlobalIntent> Intents);

/// <summary>
/// Global intent record (for power creeps)
/// </summary>
public sealed record JsonGlobalIntent(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("args")] List<JsonGlobalIntentArgs> Args);

/// <summary>
/// Arguments for a global intent
/// </summary>
public sealed record JsonGlobalIntentArgs(
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("className")] string? ClassName = null,
    [property: JsonPropertyName("cancel")] bool? Cancel = null,
    [property: JsonPropertyName("spawnId")] string? SpawnId = null,
    [property: JsonPropertyName("power")] string? Power = null);

/// <summary>
/// Power creep state for fixtures
/// </summary>
public sealed record JsonPowerCreep(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("user")] string User,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("className")] string ClassName,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("hitsMax")] int HitsMax,
    [property: JsonPropertyName("store")] Dictionary<string, int> Store,
    [property: JsonPropertyName("storeCapacity")] int StoreCapacity,
    [property: JsonPropertyName("spawnCooldownTime")] int? SpawnCooldownTime = null,
    [property: JsonPropertyName("deleteTime")] long? DeleteTime = null,
    [property: JsonPropertyName("shard")] string? Shard = null,
    [property: JsonPropertyName("powers")] Dictionary<string, JsonPowerCreepPower>? Powers = null);

/// <summary>
/// Power creep power/ability
/// </summary>
public sealed record JsonPowerCreepPower(
    [property: JsonPropertyName("level")] int Level);
