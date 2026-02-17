using PowerPochi.Command;

namespace PowerPochi.Input;

public interface IKeyboardController
{
    Task ExecuteAsync(CommandType command, CancellationToken cancellationToken);
}
