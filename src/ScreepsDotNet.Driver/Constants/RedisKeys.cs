namespace ScreepsDotNet.Driver.Constants;

internal static class RedisKeys
{
    public const string AccessibleRooms = "accessibleRooms";
    public const string RoomStatusData = "roomStatusData";
    public const string Memory = "memory:";
    public const string GameTime = "gameTime";
    public const string MapView = "mapView:";
    public const string TerrainData = "terrainData";
    public const string ScriptCachedData = "scriptCachedData:";
    public const string UserOnline = "userOnline:";
    public const string MainLoopPaused = "mainLoopPaused";
    public const string RoomHistory = "roomHistory:";
    public const string RoomVisual = "roomVisual:";
    public const string MemorySegments = "memorySegments:";
    public const string PublicMemorySegments = "publicMemorySegments:";
    public const string RoomEventLog = "roomEventLog:";
    public const string ActiveRooms = "activeRooms";
    public const string MainLoopMinDuration = "tickRate";
    public const string MainLoopResetInterval = "mainLoopResetInterval";
    public const string CpuMaxPerTick = "cpuMaxPerTick";
    public const string CpuBucketSize = "cpuBucketSize";
    public const string HistoryChunkSize = "historyChunkSize";
    public const string UseSigintTimeout = "useSigintTimeout";
    public const string EnableInspector = "enableInspector";
}
