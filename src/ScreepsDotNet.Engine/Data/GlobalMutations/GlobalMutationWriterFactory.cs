namespace ScreepsDotNet.Engine.Data.GlobalMutations;

using ScreepsDotNet.Driver.Abstractions.GlobalProcessing;

internal sealed class GlobalMutationWriterFactory(IGlobalMutationDispatcher dispatcher) : IGlobalMutationWriterFactory
{
    public IGlobalMutationWriter Create() => new GlobalMutationWriter(dispatcher);
}
