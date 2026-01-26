namespace ScreepsDotNet.Engine.Tests.Unit;

using ScreepsDotNet.Common.Constants;
using Xunit;

public sealed class SystemUserIdsTests
{
    [Theory]
    [InlineData(SystemUserIds.LegacyInvader, true)]  // Legacy invader ("2")
    [InlineData(SystemUserIds.NamedInvader, true)]  // Named invader ("Invader")
    [InlineData("invader", true)]  // Case-insensitive
    [InlineData("INVADER", true)]  // Case-insensitive
    [InlineData(SystemUserIds.LegacySourceKeeper, false)]  // Source keeper (not invader)
    [InlineData(SystemUserIds.NamedSourceKeeper, false)]  // Source keeper (not invader)
    [InlineData("user1", false)]  // Normal user
    [InlineData("", false)]  // Empty
    [InlineData(null, false)]  // Null
    public void IsInvader_VariousUserIds_ReturnsExpectedResult(string? userId, bool expected)
    {
        var result = SystemUserIds.IsInvader(userId);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(SystemUserIds.LegacySourceKeeper, true)]  // Legacy source keeper ("3")
    [InlineData(SystemUserIds.NamedSourceKeeper, true)]  // Named source keeper ("SourceKeeper")
    [InlineData("sourcekeeper", true)]  // Case-insensitive
    [InlineData("SOURCEKEEPER", true)]  // Case-insensitive
    [InlineData(SystemUserIds.LegacyInvader, false)]  // Invader (not source keeper)
    [InlineData(SystemUserIds.NamedInvader, false)]  // Invader (not source keeper)
    [InlineData("user1", false)]  // Normal user
    [InlineData("", false)]  // Empty
    [InlineData(null, false)]  // Null
    public void IsSourceKeeper_VariousUserIds_ReturnsExpectedResult(string? userId, bool expected)
    {
        var result = SystemUserIds.IsSourceKeeper(userId);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(SystemUserIds.LegacyInvader, true)]  // Legacy invader ("2")
    [InlineData(SystemUserIds.LegacySourceKeeper, true)]  // Legacy source keeper ("3")
    [InlineData(SystemUserIds.NamedInvader, true)]  // Named invader ("Invader")
    [InlineData(SystemUserIds.NamedSourceKeeper, true)]  // Named source keeper ("SourceKeeper")
    [InlineData("invader", true)]  // Case-insensitive
    [InlineData("sourcekeeper", true)]  // Case-insensitive
    [InlineData("user1", false)]  // Normal user
    [InlineData("", false)]  // Empty
    [InlineData(null, false)]  // Null
    public void IsNpcUser_VariousUserIds_ReturnsExpectedResult(string? userId, bool expected)
    {
        var result = SystemUserIds.IsNpcUser(userId);
        Assert.Equal(expected, result);
    }
}
