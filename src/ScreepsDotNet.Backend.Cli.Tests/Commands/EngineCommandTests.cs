using ScreepsDotNet.Backend.Cli.Commands.Engine;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Data.Rooms;
using ScreepsDotNet.Engine.Validation;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

public sealed class EngineCommandTests
{
    private const string TestRoomName = "W1N1";
    private const int TestGameTime = 1000;

    #region EngineStatusCommand Tests

    [Fact]
    public async Task EngineStatusCommand_WithData_OutputsTable()
    {
        // Arrange
        var diagnosticsService = new FakeEngineDiagnosticsService
        {
            Statistics = new EngineStatisticsSnapshot(
                TotalRoomsProcessed: 100,
                AverageProcessingTimeMs: 15.5,
                TotalIntentsValidated: 500,
                RejectionRate: 0.05,
                TopErrorCode: "NotOwned",
                TopIntentType: "harvest"
            )
        };

        var command = new EngineStatusCommand(diagnosticsService);
        var settings = new EngineStatusCommand.Settings { OutputJson = false };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task EngineStatusCommand_JsonFlag_OutputsJson()
    {
        // Arrange
        var diagnosticsService = new FakeEngineDiagnosticsService
        {
            Statistics = new EngineStatisticsSnapshot(
                TotalRoomsProcessed: 100,
                AverageProcessingTimeMs: 15.5,
                TotalIntentsValidated: 500,
                RejectionRate: 0.05,
                TopErrorCode: "NotOwned",
                TopIntentType: "harvest"
            )
        };

        var command = new EngineStatusCommand(diagnosticsService);
        var settings = new EngineStatusCommand.Settings { OutputJson = true };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task EngineStatusCommand_NoData_HandlesGracefully()
    {
        // Arrange
        var diagnosticsService = new FakeEngineDiagnosticsService
        {
            Statistics = new EngineStatisticsSnapshot(
                TotalRoomsProcessed: 0,
                AverageProcessingTimeMs: 0,
                TotalIntentsValidated: 0,
                RejectionRate: 0,
                TopErrorCode: null,
                TopIntentType: null
            )
        };

        var command = new EngineStatusCommand(diagnosticsService);
        var settings = new EngineStatusCommand.Settings { OutputJson = false };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
    }

    #endregion

    #region RoomStateCommand Tests

    [Fact]
    public async Task RoomStateCommand_WithAuth_ReturnsState()
    {
        // Arrange
        var stateProvider = new FakeRoomStateProvider();
        stateProvider.SetRoomState(TestRoomName, new RoomState(
            RoomName: TestRoomName,
            GameTime: TestGameTime,
            Info: null,
            Objects: new Dictionary<string, Driver.Contracts.RoomObjectSnapshot>(),
            Users: new Dictionary<string, Driver.Contracts.UserState>(),
            Intents: null,
            Terrain: new Dictionary<string, Driver.Contracts.RoomTerrainSnapshot>(),
            Flags: []
        ));

        var command = new RoomStateCommand(stateProvider);
        var settings = new RoomStateCommand.Settings
        {
            RoomName = TestRoomName,
            OutputJson = false
        };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RoomStateCommand_JsonFlag_OutputsJson()
    {
        // Arrange
        var stateProvider = new FakeRoomStateProvider();
        stateProvider.SetRoomState(TestRoomName, new RoomState(
            RoomName: TestRoomName,
            GameTime: TestGameTime,
            Info: null,
            Objects: new Dictionary<string, Driver.Contracts.RoomObjectSnapshot>(),
            Users: new Dictionary<string, Driver.Contracts.UserState>(),
            Intents: null,
            Terrain: new Dictionary<string, Driver.Contracts.RoomTerrainSnapshot>(),
            Flags: []
        ));

        var command = new RoomStateCommand(stateProvider);
        var settings = new RoomStateCommand.Settings
        {
            RoomName = TestRoomName,
            OutputJson = true
        };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RoomStateCommand_NonExistentRoom_HandlesGracefully()
    {
        // Arrange
        var stateProvider = new FakeRoomStateProvider();
        // Don't set any room state

        var command = new RoomStateCommand(stateProvider);
        var settings = new RoomStateCommand.Settings
        {
            RoomName = "NonExistent",
            OutputJson = false
        };

        // Act & Assert - should throw or handle gracefully
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);
        Assert.Equal(0, exitCode);
    }

    #endregion

    #region ValidationStatsCommand Tests

    [Fact]
    public async Task ValidationStatsCommand_WithData_OutputsTable()
    {
        // Arrange
        var statisticsSink = new FakeValidationStatisticsSink
        {
            Statistics = new ValidationStatistics
            {
                TotalIntentsValidated = 100,
                ValidIntentsCount = 95,
                RejectedIntentsCount = 5,
                RejectionsByErrorCode = new Dictionary<ValidationErrorCode, int>
                {
                    [ValidationErrorCode.NotOwned] = 3,
                    [ValidationErrorCode.InvalidTargetType] = 2
                },
                RejectionsByIntentType = new Dictionary<string, int>
                {
                    ["harvest"] = 2,
                    ["transfer"] = 3
                }
            }
        };

        var command = new ValidationStatsCommand(statisticsSink);
        var settings = new ValidationStatsCommand.Settings { OutputJson = false, Reset = false };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.False(statisticsSink.WasReset);
    }

    [Fact]
    public async Task ValidationStatsCommand_JsonFlag_OutputsJson()
    {
        // Arrange
        var statisticsSink = new FakeValidationStatisticsSink
        {
            Statistics = new ValidationStatistics
            {
                TotalIntentsValidated = 100,
                ValidIntentsCount = 95,
                RejectedIntentsCount = 5
            }
        };

        var command = new ValidationStatsCommand(statisticsSink);
        var settings = new ValidationStatsCommand.Settings { OutputJson = true, Reset = false };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ValidationStatsCommand_ResetFlag_ClearsStatistics()
    {
        // Arrange
        var statisticsSink = new FakeValidationStatisticsSink
        {
            Statistics = new ValidationStatistics
            {
                TotalIntentsValidated = 100,
                ValidIntentsCount = 95,
                RejectedIntentsCount = 5
            }
        };

        var command = new ValidationStatsCommand(statisticsSink);
        var settings = new ValidationStatsCommand.Settings { OutputJson = false, Reset = true };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(statisticsSink.WasReset);
    }

    [Fact]
    public async Task ValidationStatsCommand_NoData_HandlesGracefully()
    {
        // Arrange
        var statisticsSink = new FakeValidationStatisticsSink
        {
            Statistics = new ValidationStatistics
            {
                TotalIntentsValidated = 0,
                ValidIntentsCount = 0,
                RejectedIntentsCount = 0
            }
        };

        var command = new ValidationStatsCommand(statisticsSink);
        var settings = new ValidationStatsCommand.Settings { OutputJson = false, Reset = false };

        // Act
        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
    }

    #endregion

    #region Fakes

    private sealed class FakeEngineDiagnosticsService : IEngineDiagnosticsService
    {
        public EngineStatisticsSnapshot Statistics { get; init; } = new(0, 0, 0, 0, null, null);

        public Task<EngineStatisticsSnapshot> GetEngineStatisticsAsync(CancellationToken token = default)
            => Task.FromResult(Statistics);
    }

    private sealed class FakeRoomStateProvider : IRoomStateProvider
    {
        private readonly Dictionary<string, RoomState> _states = [];

        public void SetRoomState(string roomName, RoomState state)
            => _states[roomName] = state;

        public Task<RoomState> GetRoomStateAsync(string roomName, int gameTime, CancellationToken token = default)
        {
            if (!_states.TryGetValue(roomName, out var state))
            {
                state = new RoomState(
                    roomName,
                    0,
                    null,
                    new Dictionary<string, Driver.Contracts.RoomObjectSnapshot>(),
                    new Dictionary<string, Driver.Contracts.UserState>(),
                    null,
                    new Dictionary<string, Driver.Contracts.RoomTerrainSnapshot>(),
                    []);
            }
            return Task.FromResult(state)!;
        }

        public void Invalidate(string roomName)
            => _states.Remove(roomName);
    }

    private sealed class FakeValidationStatisticsSink : IValidationStatisticsSink
    {
        public ValidationStatistics Statistics { get; init; } = new();
        public bool WasReset { get; private set; }

        public void RecordValidation(Driver.Contracts.IntentRecord intent, ValidationResult result)
        {
            // Not needed for these tests
        }

        public ValidationStatistics GetStatistics()
            => Statistics;

        public void Reset()
            => WasReset = true;
    }

    #endregion
}
