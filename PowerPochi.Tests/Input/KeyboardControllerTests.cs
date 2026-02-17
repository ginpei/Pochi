using Microsoft.Extensions.Logging.Abstractions;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;
using PowerPochi.Command;
using PowerPochi.Input;
using PowerPochi.Options;
using Xunit;

namespace PowerPochi.Tests.Input;

public class KeyboardControllerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldInvokeInjectorWithMappedKey()
    {
        var injector = new RecordingKeyboardInjector();
        var options = MicrosoftOptions.Create(KeyboardMappingOptions.WithDefaults());
        var controller = new KeyboardController(injector, NullLogger<KeyboardController>.Instance, options);

        await controller.ExecuteAsync(CommandType.Next, CancellationToken.None);

        Assert.Single(injector.Calls);
        Assert.Equal(KeyCode.RightArrow, injector.Calls[0].First().Key);
    }

    private sealed class RecordingKeyboardInjector : IKeyboardInjector
    {
        public List<IReadOnlyList<KeyStroke>> Calls { get; } = new();

        public Task InjectAsync(IReadOnlyList<KeyStroke> sequence, CancellationToken cancellationToken)
        {
            Calls.Add(sequence);
            return Task.CompletedTask;
        }
    }
}
