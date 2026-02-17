using Microsoft.Extensions.Logging;

namespace PowerPochi.Input;

public static class KeyboardInjectorFactory
{
    public static IKeyboardInjector Create(ILoggerFactory loggerFactory)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsKeyboardInjector(loggerFactory.CreateLogger<WindowsKeyboardInjector>());
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacKeyboardInjector(loggerFactory.CreateLogger<MacKeyboardInjector>());
        }

        var logger = loggerFactory.CreateLogger("KeyboardInjectorFactory");
        logger.LogWarning("Unsupported platform for keyboard injection. Commands will be no-op.");
        return new NoopKeyboardInjector(loggerFactory.CreateLogger<NoopKeyboardInjector>());
    }
}
