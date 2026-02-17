using Microsoft.Extensions.Logging;
using PowerPochi.Input;

namespace PowerPochi.Command;

public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IKeyboardController _keyboardController;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IKeyboardController keyboardController, ILogger<CommandDispatcher> logger)
    {
        _keyboardController = keyboardController;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(CommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _keyboardController.ExecuteAsync(request.Command, cancellationToken);
            return DispatchResult.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch command {Command} from {ClientId}", request.Command, request.ClientId);
            return DispatchResult.Failure("dispatch failed");
        }
    }
}
