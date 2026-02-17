using PowerPochi.Command;
using PowerPochi.Input;

namespace PowerPochi.Options;

public class KeyboardMappingOptions
{
    public Dictionary<CommandType, KeyboardAction> Mappings { get; } = new();

    public static KeyboardMappingOptions WithDefaults()
    {
        var options = new KeyboardMappingOptions();
        ApplyDefaults(options);
        return options;
    }

    public static void ApplyDefaults(KeyboardMappingOptions options)
    {
        options.Mappings[CommandType.Next] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.RightArrow));
        options.Mappings[CommandType.Prev] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.LeftArrow));
        options.Mappings[CommandType.StartPresentation] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.F5));
        options.Mappings[CommandType.EndPresentation] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.Escape));
        options.Mappings[CommandType.Blackout] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.B));
        options.Mappings[CommandType.Whiteout] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.W));
    }
}
