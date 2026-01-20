namespace ScreepsDotNet.Backend.Core.Models;

using ScreepsDotNet.Common.Types;

public sealed record RoomSummary(string Name, string? Owner, ControllerLevel ControllerLevel, int EnergyAvailable);
