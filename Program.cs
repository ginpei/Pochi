using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

var builder = WebApplication.CreateBuilder(args);

var urls = builder.Configuration["POCHI_URLS"] ?? "http://0.0.0.0:5000";
builder.WebHost.UseUrls(urls);

var authToken = builder.Configuration["POCHI_TOKEN"] ?? builder.Configuration["POCHI_PIN"];
var serializerOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

var app = builder.Build();

var keyboardInjector = CreateKeyboardInjector(app.Logger);
var keyboardController = new KeyboardController(keyboardInjector, app.Logger);
var dispatcher = new CommandDispatcher(keyboardController, app.Logger);

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Map("/ws", async context =>
{
    var providedToken = GetProvidedToken(context);
    if (!IsAuthorized(authToken, providedToken))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("unauthorized", context.RequestAborted);
        return;
    }

    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

    await SendTextAsync(webSocket, "connected", context.RequestAborted);

    var buffer = new byte[4096];
    while (webSocket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
    {
        var result = await webSocket.ReceiveAsync(buffer, context.RequestAborted);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", context.RequestAborted);
            break;
        }

        var message = Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count));
        if (!TryParseCommand(message, serializerOptions, out var command, out var error))
        {
            await SendTextAsync(webSocket, $"error:{error}", context.RequestAborted);
            continue;
        }

        var dispatchResult = await dispatcher.DispatchAsync(command, context.RequestAborted);
        if (!dispatchResult.IsSuccess)
        {
            await SendTextAsync(webSocket, $"error:{dispatchResult.Error}", context.RequestAborted);
            continue;
        }

        await SendTextAsync(webSocket, $"ok:{command.Command}", context.RequestAborted);
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static string? GetProvidedToken(HttpContext context)
{
    if (context.Request.Query.TryGetValue("token", out var tokenValues) && !StringValues.IsNullOrEmpty(tokenValues))
    {
        var token = tokenValues.ToString();
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }
    }

    if (context.Request.Headers.TryGetValue("Pochi-Token", out var headerValues) && !StringValues.IsNullOrEmpty(headerValues))
    {
        var token = headerValues.ToString();
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }
    }

    return null;
}

static bool IsAuthorized(string? configuredToken, string? providedToken)
{
    if (string.IsNullOrWhiteSpace(configuredToken))
    {
        return true;
    }

    return !string.IsNullOrWhiteSpace(providedToken) && string.Equals(configuredToken, providedToken, StringComparison.Ordinal);
}

static bool TryParseCommand(string message, JsonSerializerOptions serializerOptions, out CommandRequest command, out string error)
{
    command = default;
    error = string.Empty;

    try
    {
        var envelope = JsonSerializer.Deserialize<CommandEnvelope>(message, serializerOptions);
        if (envelope is null)
        {
            error = "invalid message";
            return false;
        }

        if (!string.Equals(envelope.Type, "command", StringComparison.OrdinalIgnoreCase))
        {
            error = "unsupported type";
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.Command) || !Enum.TryParse<CommandType>(envelope.Command, ignoreCase: true, out var parsedCommand))
        {
            error = "unknown command";
            return false;
        }

        command = new CommandRequest(parsedCommand, envelope.ClientId);
        return true;
    }
    catch (JsonException ex)
    {
        error = $"invalid json: {ex.Message}";
        return false;
    }
}

static Task SendTextAsync(WebSocket webSocket, string text, CancellationToken cancellationToken)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    return webSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
}

static IKeyboardInjector CreateKeyboardInjector(ILogger logger)
{
    if (OperatingSystem.IsWindows())
    {
        return new WindowsKeyboardInjector(logger);
    }

    if (OperatingSystem.IsMacOS())
    {
        return new MacKeyboardInjector(logger);
    }

    logger.LogWarning("Unsupported platform for keyboard injection. Commands will be no-op.");
    return new NoopKeyboardInjector(logger);
}

sealed class CommandDispatcher
{
    private readonly IKeyboardController _keyboardController;
    private readonly ILogger _logger;

    public CommandDispatcher(IKeyboardController keyboardController, ILogger logger)
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
            _logger.LogError(ex, "Failed to dispatch command {Command}", request.Command);
            return DispatchResult.Failure("dispatch failed");
        }
    }
}

interface IKeyboardController
{
    Task ExecuteAsync(CommandType command, CancellationToken cancellationToken);
}

interface IKeyboardInjector
{
    Task InjectAsync(IReadOnlyList<KeyStroke> sequence, CancellationToken cancellationToken);
}

sealed class KeyboardController : IKeyboardController
{
    private readonly IKeyboardInjector _injector;
    private readonly ILogger _logger;
    private readonly IReadOnlyDictionary<CommandType, KeyboardAction> _actions;

    public KeyboardController(IKeyboardInjector injector, ILogger logger)
    {
        _injector = injector;
        _logger = logger;
        _actions = new Dictionary<CommandType, KeyboardAction>
        {
            [CommandType.Next] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.RightArrow)),
            [CommandType.Prev] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.LeftArrow)),
            [CommandType.StartPresentation] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.F5)),
            [CommandType.EndPresentation] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.Escape)),
            [CommandType.Blackout] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.B)),
            [CommandType.Whiteout] = KeyboardAction.Single(KeyStroke.WithKey(KeyCode.W))
        };
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

readonly record struct DispatchResult(bool IsSuccess, string? Error)
{
    public static DispatchResult Success() => new(true, null);
    public static DispatchResult Failure(string error) => new(false, error);
}

sealed class KeyboardAction
{
    public IReadOnlyList<KeyStroke> Sequence { get; }

    private KeyboardAction(IReadOnlyList<KeyStroke> sequence)
    {
        Sequence = sequence;
    }

    public static KeyboardAction Single(KeyStroke stroke) => new KeyboardAction(new List<KeyStroke> { stroke });

    public override string ToString() => string.Join(", ", Sequence);
}

readonly record struct KeyStroke(KeyCode Key)
{
    public override string ToString() => Key.ToString();

    public static KeyStroke WithKey(KeyCode key) => new(key);
}

enum KeyCode
{
    RightArrow,
    LeftArrow,
    F5,
    Escape,
    B,
    W
}

record CommandEnvelope(string? Type, string? Command, string? ClientId);

record CommandRequest(CommandType Command, string? ClientId);

enum CommandType
{
    Next,
    Prev,
    StartPresentation,
    EndPresentation,
    Blackout,
    Whiteout
}

sealed class WindowsKeyboardInjector : IKeyboardInjector
{
    private readonly ILogger _logger;

    public WindowsKeyboardInjector(ILogger logger)
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

sealed class MacKeyboardInjector : IKeyboardInjector
{
    private readonly ILogger _logger;

    public MacKeyboardInjector(ILogger logger)
    {
        _logger = logger;
    }

    public Task InjectAsync(IReadOnlyList<KeyStroke> sequence, CancellationToken cancellationToken)
    {
        _logger.LogWarning("macOS keyboard injection not implemented. Ignoring {Count} key strokes.", sequence.Count);
        return Task.CompletedTask;
    }
}

sealed class NoopKeyboardInjector : IKeyboardInjector
{
    private readonly ILogger _logger;

    public NoopKeyboardInjector(ILogger logger)
    {
        _logger = logger;
    }

    public Task InjectAsync(IReadOnlyList<KeyStroke> sequence, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Keyboard injection unavailable on this platform. Ignoring {Count} key strokes.", sequence.Count);
        return Task.CompletedTask;
    }
}
