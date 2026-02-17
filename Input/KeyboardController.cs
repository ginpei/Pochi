using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPochi.Command;
using PowerPochi.Options;

namespace PowerPochi.Input;

public sealed class KeyboardController : IKeyboardController
{
    private readonly IKeyboardInjector _injector;
    private readonly ILogger<KeyboardController> _logger;
    private readonly IReadOnlyDictionary<CommandType, KeyboardAction> _actions;

    public KeyboardController(IKeyboardInjector injector, ILogger<KeyboardController> logger, IOptions<KeyboardMappingOptions> options)
    {
        _injector = injector;
        _logger = logger;
        var mapping = options.Value;
        if (mapping.Mappings.Count == 0)
        {
            KeyboardMappingOptions.ApplyDefaults(mapping);
        }

        _actions = mapping.Mappings;
    }

    public Task ExecuteAsync(CommandType command, CancellationToken cancellationToken)
    {
        if (!_actions.TryGetValue(command, out var action))
        {
            throw new InvalidOperationException($"Command mapping not found for {command}");
        }

        _logger.LogInformation("Executing command {Command} -> {Action}", command, action);
        return _injector.InjectAsync(action.Sequence, cancellationToken);
    }
}
