namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Backend.Cli.Commands.Auth;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;

public sealed class AuthCommandTests
{
    [Fact]
    public async Task AuthIssueCommand_IssuesTokenForUser()
    {
        var service = new FakeTokenService();
        var command = new AuthIssueCommand(service, null, null, new TestFormatter());
        var settings = new AuthIssueCommand.Settings
        {
            UserId = "user-1",
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("user-1", service.LastIssuedUserId);
    }

    [Fact]
    public async Task AuthResolveCommand_ReturnsErrorWhenTokenMissing()
    {
        var service = new FakeTokenService { ResolveResult = null };
        var command = new AuthResolveCommand(service, null, null, new TestFormatter());
        var settings = new AuthResolveCommand.Settings
        {
            Token = "missing-token"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Equal("missing-token", service.LastResolvedToken);
    }

    [Fact]
    public async Task AuthTokenListCommand_PassesFilterToService()
    {
        var service = new FakeTokenService();
        var command = new AuthTokenListCommand(service, null, null, new TestFormatter());
        var settings = new AuthTokenListCommand.Settings
        {
            UserId = "user-2"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("user-2", service.LastListedUserId);
    }

    [Fact]
    public async Task AuthRevokeCommand_ReturnsFailureWhenTokenMissing()
    {
        var service = new FakeTokenService { RevokeResult = false };
        var command = new AuthRevokeCommand(service, null, null, new TestFormatter());
        var settings = new AuthRevokeCommand.Settings
        {
            Token = "token-xyz"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Equal("token-xyz", service.LastRevokedToken);
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string? LastIssuedUserId { get; private set; }
        public string? LastResolvedToken { get; private set; }
        public string? LastListedUserId { get; private set; }
        public string? LastRevokedToken { get; private set; }
        public string IssueResult { get; init; } = "token-123";
        public string? ResolveResult { get; set; } = "user-1";
        public IReadOnlyList<AuthTokenInfo> ListResult { get; init; } =
            new List<AuthTokenInfo> { new("token-123", "user-1", TimeSpan.FromMinutes(5)) };
        public bool RevokeResult { get; set; } = true;

        public Task<string> IssueTokenAsync(string userId, CancellationToken cancellationToken = default)
        {
            LastIssuedUserId = userId;
            return Task.FromResult(IssueResult);
        }

        public Task<string?> ResolveUserIdAsync(string token, CancellationToken cancellationToken = default)
        {
            LastResolvedToken = token;
            return Task.FromResult(ResolveResult);
        }

        public Task<IReadOnlyList<AuthTokenInfo>> ListTokensAsync(string? userId = null, CancellationToken cancellationToken = default)
        {
            LastListedUserId = userId;
            return Task.FromResult(ListResult);
        }

        public Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            LastRevokedToken = token;
            return Task.FromResult(RevokeResult);
        }
    }
    private sealed class TestFormatter : ICommandOutputFormatter
    {
        public void WriteJson<T>(T payload)
        {
        }

        public void WriteTable(Table table)
        {
        }

        public void WriteKeyValueTable(IEnumerable<(string Key, string Value)> rows, string? title = null)
        {
        }

        public void WriteMarkdownTable(string? title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
        }

        public void WriteLine(string message)
        {
        }

        public void WriteMarkupLine(string markup, params object[] args)
        {
        }
    }
}
