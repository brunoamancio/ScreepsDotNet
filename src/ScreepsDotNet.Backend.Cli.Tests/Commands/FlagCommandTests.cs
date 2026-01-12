namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Backend.Cli.Commands.Flag;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;

public sealed class FlagCommandTests
{
    [Fact]
    public async Task FlagChangeColorCommand_ResolvesUserAndCallsService()
    {
        var repo = new FakeUserRepository("user-123");
        var service = new FakeFlagService();
        var command = new FlagChangeColorCommand(service, repo);
        var settings = new FlagChangeColorCommand.Settings
        {
            Username = "test-user",
            RoomName = "W1N1",
            Name = "Flag1",
            Color = Core.Constants.Color.Red,
            SecondaryColor = Core.Constants.Color.Blue
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("user-123", service.LastUserId);
        Assert.Equal("Flag1", service.LastFlagName);
    }

    [Fact]
    public async Task FlagRemoveCommand_RemovesFlagForResolvedUser()
    {
        var repo = new FakeUserRepository("user-999");
        var service = new FakeFlagService();
        var command = new FlagRemoveCommand(service, repo);
        var settings = new FlagRemoveCommand.Settings
        {
            Username = "test-user",
            RoomName = "W2N2",
            Name = "FlagGone"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("user-999", service.LastUserId);
        Assert.Equal("FlagGone", service.LastFlagName);
    }

    private sealed class FakeUserRepository(string userId) : IUserRepository
    {
        public Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<UserPublicProfile?> FindPublicProfileAsync(string? username, string? shard, CancellationToken cancellationToken = default)
            => Task.FromResult<UserPublicProfile?>(new UserPublicProfile(userId, username ?? "test-user", null, null, 0, null));

        public Task UpdateNotifyPreferencesAsync(string userId, IDictionary<string, object?> notifyPreferences, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> UpdateBadgeAsync(string userId, UserBadgeUpdate badge, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<EmailUpdateResult> UpdateEmailAsync(string userId, string email, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SetSteamVisibilityAsync(string userId, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<SetUsernameResult> SetUsernameAsync(string userId, string username, string? email, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeFlagService : IFlagService
    {
        public string? LastUserId { get; private set; }
        public string? LastFlagName { get; private set; }

        public Task<FlagResult> ChangeFlagColorAsync(string userId, string room, string? shard, string name, Core.Constants.Color color, Core.Constants.Color secondaryColor, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastFlagName = name;
            return Task.FromResult(new FlagResult(FlagResultStatus.Success, null));
        }

        public Task<FlagResult> CreateFlagAsync(string userId, CreateFlagRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new FlagResult(FlagResultStatus.Success, null));

        public Task<FlagResult> RemoveFlagAsync(string userId, string room, string? shard, string name, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastFlagName = name;
            return Task.FromResult(new FlagResult(FlagResultStatus.Success, null));
        }

        public Task<string> GenerateUniqueFlagNameAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult("FLAG_1");

        public Task<bool> IsFlagNameUniqueAsync(string userId, string name, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
