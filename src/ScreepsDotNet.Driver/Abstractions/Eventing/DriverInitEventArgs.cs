namespace ScreepsDotNet.Driver.Abstractions.Eventing;

public sealed class DriverInitEventArgs(DriverProcessType processType) : EventArgs
{
    public DriverProcessType ProcessType { get; } = processType;
}
