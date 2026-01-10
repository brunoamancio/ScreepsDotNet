namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using ScreepsDotNet.Backend.Cli.Commands.System;
using ScreepsDotNet.Backend.Core.Services;

public sealed class SystemCommandTests
{
    [Fact]
    public async Task SystemMessageCommand_PublishesMessage()
    {
        var service = new FakeSystemControlService();
        var command = new SystemMessageCommand(service);
        var settings = new SystemMessageCommand.Settings
        {
            Message = "Server restart in 5 minutes"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("Server restart in 5 minutes", service.LastMessage);
    }

    private sealed class FakeSystemControlService : ISystemControlService
    {
        public string? LastMessage { get; private set; }

        public Task<bool> IsSimulationPausedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task PauseSimulationAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ResumeSimulationAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishServerMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }

        public Task<int?> GetTickDurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task SetTickDurationAsync(int durationMilliseconds, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
