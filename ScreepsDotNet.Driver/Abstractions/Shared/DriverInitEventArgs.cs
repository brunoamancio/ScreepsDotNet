namespace ScreepsDotNet.Driver.Abstractions.Shared;

public sealed class DriverInitEventArgs(DriverProcessType processType) : EventArgs
{
    public DriverProcessType ProcessType { get; } = processType;
}
