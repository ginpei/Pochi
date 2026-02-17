using Microsoft.Extensions.Logging;

namespace PowerPochi.Input;

public sealed class NoopKeyboardInjector : IKeyboardInjector
{
    private readonly ILogger<NoopKeyboardInjector> _logger;

    public NoopKeyboardInjector(ILogger<NoopKeyboardInjector> logger)
    {
        _logger = logger;
    }

    public Task InjectAsync(IReadOnlyList<KeyStroke> sequence, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Keyboard injection unavailable on this platform. Ignoring {Count} key strokes.", sequence.Count);
        return Task.CompletedTask;
    }
}
