using Microsoft.Extensions.Logging.Abstractions;
using PowerPochi.Command;
using PowerPochi.Input;
using Xunit;

namespace PowerPochi.Tests.Command;

public class CommandDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ShouldExecuteCommand()
    {
        var controller = new RecordingKeyboardController();
        var dispatcher = new CommandDispatcher(controller, NullLogger<CommandDispatcher>.Instance);
        var request = new CommandRequest(CommandType.Blackout, "client1");

        var result = await dispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(CommandType.Blackout, controller.Executed);
    }

    [Fact]
    public async Task DispatchAsync_ShouldReturnFailureOnException()
    {
        var controller = new ThrowingKeyboardController();
        var dispatcher = new CommandDispatcher(controller, NullLogger<CommandDispatcher>.Instance);
        var request = new CommandRequest(CommandType.Whiteout, null);

        var result = await dispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("dispatch failed", result.Error);
    }

    private sealed class RecordingKeyboardController : IKeyboardController
    {
        public List<CommandType> Executed { get; } = new();

        public Task ExecuteAsync(CommandType command, CancellationToken cancellationToken)
        {
            Executed.Add(command);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingKeyboardController : IKeyboardController
    {
        public Task ExecuteAsync(CommandType command, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
