namespace PowerPochi.Command;

public interface ICommandDispatcher
{
    Task<DispatchResult> DispatchAsync(CommandRequest request, CancellationToken cancellationToken);
}
