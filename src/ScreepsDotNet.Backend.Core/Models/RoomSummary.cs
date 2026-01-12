namespace ScreepsDotNet.Backend.Core.Models;

public sealed record RoomSummary(string Name, string? Owner, int ControllerLevel, int EnergyAvailable);
