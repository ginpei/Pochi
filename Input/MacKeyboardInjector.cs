using Microsoft.Extensions.Logging;

namespace PowerPochi.Input;

public sealed class MacKeyboardInjector : IKeyboardInjector
{
    private readonly ILogger<MacKeyboardInjector> _logger;

    public MacKeyboardInjector(ILogger<MacKeyboardInjector> logger)
    {
        _logger = logger;
    }

    public Task InjectAsync(IReadOnlyList<KeyStroke> sequence, CancellationToken cancellationToken)
    {
        _logger.LogWarning("macOS keyboard injection not implemented. Ignoring {Count} key strokes.", sequence.Count);
        return Task.CompletedTask;
    }
}
