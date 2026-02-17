using System.Collections.Generic;
using System.Net.WebSockets;
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

var keyboardController = new KeyboardController(app.Logger);
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

sealed class KeyboardController : IKeyboardController
{
    private readonly ILogger _logger;
    private readonly IReadOnlyDictionary<CommandType, KeyboardAction> _actions;

    public KeyboardController(ILogger logger)
    {
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
        return Task.CompletedTask;
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
