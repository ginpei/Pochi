using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using PowerPochi.Command;
using PowerPochi.Diagnostics;
using PowerPochi.Options;

namespace PowerPochi.Hosting;

public sealed class WebSocketHandler
{
    private readonly ICommandParser _parser;
    private readonly ICommandDispatcher _dispatcher;
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly CommandMetrics _metrics;
    private readonly ServerOptions _options;

    public WebSocketHandler(ICommandParser parser, ICommandDispatcher dispatcher, ILogger<WebSocketHandler> logger, CommandMetrics metrics, IOptions<ServerOptions> options)
    {
        _parser = parser;
        _dispatcher = dispatcher;
        _logger = logger;
        _metrics = metrics;
        _options = options.Value;
    }

    public async Task HandleAsync(HttpContext context)
    {
        var providedToken = GetProvidedToken(context);
        if (!IsAuthorized(_options.Token, providedToken))
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
        _logger.LogInformation("WebSocket connected from {RemoteIp}", context.Connection.RemoteIpAddress);
        await SendTextAsync(webSocket, "connected", context.RequestAborted);

        var buffer = new byte[4096];
        try
        {
            while (webSocket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(buffer, context.RequestAborted);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (webSocket.State == WebSocketState.CloseReceived)
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", context.RequestAborted);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count));
                var parseResult = _parser.Parse(message);
                if (!parseResult.IsSuccess || parseResult.Command is null)
                {
                    var error = parseResult.Error ?? "invalid message";
                    _metrics.RecordParseFailure();
                    _logger.LogWarning("Parse failed: {Error}; Client {ClientIp}", error, context.Connection.RemoteIpAddress);
                    await SendTextAsync(webSocket, $"error:{error}", context.RequestAborted);
                    continue;
                }

                _logger.LogInformation("Dispatching command {Command} from {ClientId}", parseResult.Command.Command, parseResult.Command.ClientId);
                var dispatchResult = await _dispatcher.DispatchAsync(parseResult.Command, context.RequestAborted);
                if (!dispatchResult.IsSuccess)
                {
                    _metrics.RecordFailure(parseResult.Command.Command);
                    _logger.LogWarning("Dispatch failed for {Command} from {ClientId}: {Error}", parseResult.Command.Command, parseResult.Command.ClientId, dispatchResult.Error);
                    await SendTextAsync(webSocket, $"error:{dispatchResult.Error}", context.RequestAborted);
                    continue;
                }

                _metrics.RecordSuccess(parseResult.Command.Command);
                _logger.LogInformation("Command {Command} from {ClientId} executed", parseResult.Command.Command, parseResult.Command.ClientId);
                await SendTextAsync(webSocket, $"ok:{parseResult.Command.Command}", context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("WebSocket aborted by client {RemoteIp}", context.Connection.RemoteIpAddress);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket reset by client {RemoteIp}", context.Connection.RemoteIpAddress);
        }
    }

    private static string? GetProvidedToken(HttpContext context)
    {
        if (context.Request.Query.TryGetValue("token", out var tokenValues) && !StringValues.IsNullOrEmpty(tokenValues))
        {
            var token = tokenValues.ToString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        if (context.Request.Headers.TryGetValue("PowerPochi-Token", out var headerValues) && !StringValues.IsNullOrEmpty(headerValues))
        {
            var token = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private static bool IsAuthorized(string? configuredToken, string? providedToken)
    {
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(providedToken) && string.Equals(configuredToken, providedToken, StringComparison.Ordinal);
    }

    private static Task SendTextAsync(WebSocket webSocket, string text, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return webSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }
}
