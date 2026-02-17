using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace PowerPochi.Input;

public sealed class WindowsKeyboardInjector : IKeyboardInjector
{
    private readonly ILogger<WindowsKeyboardInjector> _logger;

    public WindowsKeyboardInjector(ILogger<WindowsKeyboardInjector> logger)
    {
        _logger = logger;
    }

    public Task InjectAsync(IReadOnlyList<KeyStroke> sequence, CancellationToken cancellationToken)
    {
        if (sequence.Count == 0)
        {
            return Task.CompletedTask;
        }

        var inputs = new INPUT[sequence.Count * 2];
        var index = 0;

        foreach (var stroke in sequence)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = MapVirtualKey(stroke.Key);

            inputs[index++] = CreateKeyInput(key, isKeyUp: false);
            inputs[index++] = CreateKeyInput(key, isKeyUp: true);
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogError("SendInput sent {Sent}/{Total} inputs. Error: {Error}", sent, inputs.Length, error);
        }

        return Task.CompletedTask;
    }

    private static INPUT CreateKeyInput(ushort virtualKey, bool isKeyUp)
    {
        return new INPUT
        {
            type = InputType.INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = isKeyUp ? KEYEVENTF.KEYUP : KEYEVENTF.KEYDOWN,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    private static ushort MapVirtualKey(KeyCode key) => key switch
    {
        KeyCode.RightArrow => (ushort)VirtualKeyShort.VK_RIGHT,
        KeyCode.LeftArrow => (ushort)VirtualKeyShort.VK_LEFT,
        KeyCode.F5 => (ushort)VirtualKeyShort.VK_F5,
        KeyCode.Escape => (ushort)VirtualKeyShort.VK_ESCAPE,
        KeyCode.B => (ushort)VirtualKeyShort.VK_B,
        KeyCode.W => (ushort)VirtualKeyShort.VK_W,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private enum VirtualKeyShort : ushort
    {
        VK_LEFT = 0x25,
        VK_RIGHT = 0x27,
        VK_ESCAPE = 0x1B,
        VK_F5 = 0x74,
        VK_B = 0x42,
        VK_W = 0x57
    }

    private enum InputType : uint
    {
        INPUT_MOUSE = 0,
        INPUT_KEYBOARD = 1,
        INPUT_HARDWARE = 2
    }

    [Flags]
    private enum KEYEVENTF : uint
    {
        KEYDOWN = 0x0000,
        EXTENDEDKEY = 0x0001,
        KEYUP = 0x0002
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public InputType type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public KEYEVENTF dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
