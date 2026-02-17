using PowerPochi.Command;

namespace PowerPochi.Input;

public enum KeyCode
{
    RightArrow,
    LeftArrow,
    F5,
    Escape,
    B,
    W
}

public sealed record KeyStroke(KeyCode Key)
{
    public static KeyStroke WithKey(KeyCode key) => new(key);
    public override string ToString() => Key.ToString();
}

public sealed class KeyboardAction
{
    public IReadOnlyList<KeyStroke> Sequence { get; }

    private KeyboardAction(IReadOnlyList<KeyStroke> sequence)
    {
        Sequence = sequence;
    }

    public static KeyboardAction Single(KeyStroke stroke) => new(new List<KeyStroke> { stroke });

    public override string ToString() => string.Join(", ", Sequence);
}
