namespace PowerPochi.Input;

public interface IKeyboardInjector
{
    Task InjectAsync(IReadOnlyList<KeyStroke> sequence, CancellationToken cancellationToken);
}
