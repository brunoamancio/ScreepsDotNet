namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Backend.Cli.Commands.Invader;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;

public sealed class InvaderCommandTests
{
    [Fact]
    public async Task InvaderRemoveCommand_ResolvesUserAndCallsService()
    {
        var repo = new FakeUserRepository("user-abc");
        var service = new FakeInvaderService();
        var command = new InvaderRemoveCommand(service, repo);
        var settings = new InvaderRemoveCommand.Settings
        {
            Username = "Summoner",
            InvaderId = "507f1f77bcf86cd799439011"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal("user-abc", service.LastUserId);
        Assert.Equal("507f1f77bcf86cd799439011", service.LastRemoveRequest?.Id);
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

    private sealed class FakeInvaderService : IInvaderService
    {
        public string? LastUserId { get; private set; }
        public RemoveInvaderRequest? LastRemoveRequest { get; private set; }

        public Task<CreateInvaderResult> CreateInvaderAsync(string userId, CreateInvaderRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new CreateInvaderResult(CreateInvaderResultStatus.Success, Id: "507f1f77bcf86cd799439011", ErrorMessage: null));

        public Task<RemoveInvaderResult> RemoveInvaderAsync(string userId, RemoveInvaderRequest request, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastRemoveRequest = request;
            return Task.FromResult(new RemoveInvaderResult(RemoveInvaderResultStatus.Success));
        }
    }
}
