namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

using System;
using ScreepsDotNet.Engine.Data.Models;

/// <summary>
/// Shared context passed to each global processor step for the current tick.
/// </summary>
public sealed class GlobalProcessorContext(GlobalState state)
{
    public GlobalState State { get; } = state ?? throw new ArgumentNullException(nameof(state));
    public int GameTime => State.GameTime;
}
