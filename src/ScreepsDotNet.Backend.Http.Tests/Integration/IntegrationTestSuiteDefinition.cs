namespace ScreepsDotNet.Backend.Http.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestSuiteDefinition : ICollectionFixture<IntegrationTestHarness>
{
    public const string Name = "IntegrationTestSuite";
}
