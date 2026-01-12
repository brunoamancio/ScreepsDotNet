namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class MemoryUpdateRequest
{
    public string? Path { get; init; }
    public JsonElement Value { get; init; }
}

public sealed class MemorySegmentRequest
{
    public int Segment { get; init; }
    public string? Data { get; init; }
}

public sealed class ConsoleExpressionRequest
{
    public string? Expression { get; init; }
}

public sealed class UserCodeUpdateRequest
{
    public IDictionary<string, string>? Modules { get; init; }
    public string? Branch { get; init; }
    [JsonPropertyName("_hash")]
    public string? Hash { get; init; }
}

public sealed class SetActiveBranchRequest
{
    public string? Branch { get; init; }
    public string? ActiveName { get; init; }
}

public sealed class CloneBranchRequest
{
    public string? Branch { get; init; }
    public string? NewName { get; init; }
    public IDictionary<string, string>? DefaultModules { get; init; }
}

public sealed class DeleteBranchRequest
{
    public string? Branch { get; init; }
}
