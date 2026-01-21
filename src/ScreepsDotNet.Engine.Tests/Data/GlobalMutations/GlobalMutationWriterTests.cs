#pragma warning disable xUnit1051 // CancellationToken usage not required for simple unit tests

namespace ScreepsDotNet.Engine.Tests.Data.GlobalMutations;

using ScreepsDotNet.Driver.Abstractions.GlobalProcessing;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;

public sealed class GlobalMutationWriterTests
{
    #region GCL Tests

    [Fact]
    public async Task IncrementUserGcl_BatchesMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("user1", 100);
        writer.IncrementUserGcl("user1", 200);

        await writer.FlushAsync();

        var batch = Assert.Single(dispatcher.Batches);
        Assert.Equal(2, batch.UserGclMutations.Count);
        Assert.Equal("user1", batch.UserGclMutations[0].UserId);
        Assert.Equal(100, batch.UserGclMutations[0].GclIncrement);
        Assert.Equal("user1", batch.UserGclMutations[1].UserId);
        Assert.Equal(200, batch.UserGclMutations[1].GclIncrement);
    }

    [Fact]
    public async Task IncrementUserGcl_NullUserId_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl(null!, 100);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task IncrementUserGcl_EmptyUserId_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("", 100);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task IncrementUserGcl_ZeroAmount_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("user1", 0);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task IncrementUserGcl_NegativeAmount_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("user1", -100);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task IncrementUserGcl_MultipleUsers_BatchesAll()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("user1", 100);
        writer.IncrementUserGcl("user2", 200);
        writer.IncrementUserGcl("user1", 50);

        await writer.FlushAsync();

        var batch = Assert.Single(dispatcher.Batches);
        Assert.Equal(3, batch.UserGclMutations.Count);
    }

    [Fact]
    public async Task IncrementUserGcl_FlushClearsList()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("user1", 100);
        await writer.FlushAsync();

        writer.IncrementUserGcl("user2", 200);
        await writer.FlushAsync();

        Assert.Equal(2, dispatcher.Batches.Count);
        Assert.Single(dispatcher.Batches[0].UserGclMutations);
        Assert.Single(dispatcher.Batches[1].UserGclMutations);
    }

    [Fact]
    public async Task IncrementUserGcl_Reset_ClearsList()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("user1", 100);
        writer.Reset();

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    #endregion

    #region Power Tests

    [Fact]
    public async Task IncrementUserPower_BatchesMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserPower("user1", 10.5);
        writer.IncrementUserPower("user1", 20.7);

        await writer.FlushAsync();

        var batch = Assert.Single(dispatcher.Batches);
        Assert.Equal(2, batch.UserPowerMutations.Count);
        Assert.Equal("user1", batch.UserPowerMutations[0].UserId);
        Assert.Equal(10.5, batch.UserPowerMutations[0].PowerChange);
        Assert.Equal("user1", batch.UserPowerMutations[1].UserId);
        Assert.Equal(20.7, batch.UserPowerMutations[1].PowerChange);
    }

    [Fact]
    public async Task DecrementUserPower_BatchesMutationWithNegativeValue()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.DecrementUserPower("user1", 10.5);

        await writer.FlushAsync();

        var batch = Assert.Single(dispatcher.Batches);
        var mutation = Assert.Single(batch.UserPowerMutations);
        Assert.Equal("user1", mutation.UserId);
        Assert.Equal(-10.5, mutation.PowerChange);
    }

    [Fact]
    public async Task IncrementUserPower_NullUserId_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserPower(null!, 10.5);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task IncrementUserPower_ZeroAmount_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserPower("user1", 0);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task DecrementUserPower_ZeroAmount_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.DecrementUserPower("user1", 0);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task IncrementUserPower_NegativeAmount_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserPower("user1", -10.5);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task DecrementUserPower_NegativeAmount_IgnoresMutation()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.DecrementUserPower("user1", -10.5);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    #endregion

    #region Mixed Tests

    [Fact]
    public async Task MixedMutations_GclAndPower_BatchesBoth()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("user1", 100);
        writer.IncrementUserPower("user1", 10.5);
        writer.DecrementUserPower("user1", 5.0);

        await writer.FlushAsync();

        var batch = Assert.Single(dispatcher.Batches);
        Assert.Single(batch.UserGclMutations);
        Assert.Equal(2, batch.UserPowerMutations.Count);
        Assert.Equal(10.5, batch.UserPowerMutations[0].PowerChange);
        Assert.Equal(-5.0, batch.UserPowerMutations[1].PowerChange);
    }

    [Fact]
    public async Task FlushAsync_NoMutations_DoesNotDispatch()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        await writer.FlushAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task FlushAsync_OnlyGclMutations_DispatchesWithEmptyPowerList()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserGcl("user1", 100);

        await writer.FlushAsync();

        var batch = Assert.Single(dispatcher.Batches);
        Assert.Single(batch.UserGclMutations);
        Assert.Empty(batch.UserPowerMutations);
    }

    [Fact]
    public async Task FlushAsync_OnlyPowerMutations_DispatchesWithEmptyGclList()
    {
        var dispatcher = new FakeGlobalMutationDispatcher();
        var writer = new GlobalMutationWriter(dispatcher);

        writer.IncrementUserPower("user1", 10.5);

        await writer.FlushAsync();

        var batch = Assert.Single(dispatcher.Batches);
        Assert.Empty(batch.UserGclMutations);
        Assert.Single(batch.UserPowerMutations);
    }

    #endregion

    #region Fake Implementation

    private sealed class FakeGlobalMutationDispatcher : IGlobalMutationDispatcher
    {
        public List<GlobalMutationBatch> Batches { get; } = [];

        public Task ApplyAsync(GlobalMutationBatch batch, CancellationToken token = default)
        {
            Batches.Add(batch);
            return Task.CompletedTask;
        }
    }

    #endregion
}
