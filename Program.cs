using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

var urls = builder.Configuration["POCHI_URLS"] ?? "http://0.0.0.0:5000";
builder.WebHost.UseUrls(urls);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Map("/ws", async context =>
{
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
        await SendTextAsync(webSocket, $"echo:{message}", context.RequestAborted);
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static Task SendTextAsync(WebSocket webSocket, string text, CancellationToken cancellationToken)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    return webSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
}
