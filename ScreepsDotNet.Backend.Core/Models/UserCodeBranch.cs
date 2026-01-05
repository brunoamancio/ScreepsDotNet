namespace ScreepsDotNet.Backend.Core.Models;

public sealed record UserCodeBranch(string Branch, IReadOnlyDictionary<string, string> Modules, DateTime? Timestamp, bool ActiveWorld, bool ActiveSim);
