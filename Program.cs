using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using PowerPochi.Command;
using PowerPochi.Diagnostics;
using PowerPochi.Hosting;
using PowerPochi.Input;
using PowerPochi.Options;

var builder = WebApplication.CreateBuilder(args);

var urls = builder.Configuration["POWERPOCHI_URLS"]
    ?? builder.Configuration["POCHI_URLS"]
    ?? "http://0.0.0.0:5000";
var authToken = builder.Configuration["POWERPOCHI_TOKEN"]
    ?? builder.Configuration["POWERPOCHI_PIN"]
    ?? builder.Configuration["POCHI_TOKEN"]
    ?? builder.Configuration["POCHI_PIN"];

builder.Services.Configure<ServerOptions>(options =>
{
    options.Urls = urls;
    options.WebSocketPath = "/ws";
    options.Token = authToken;
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});

builder.Services.Configure<KeyboardMappingOptions>(KeyboardMappingOptions.ApplyDefaults);
builder.Services.AddSingleton<ICommandParser, JsonCommandParser>();
builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddSingleton<IKeyboardController, KeyboardController>();
builder.Services.AddSingleton<IKeyboardInjector>(sp => KeyboardInjectorFactory.Create(sp.GetRequiredService<ILoggerFactory>()));
builder.Services.AddSingleton<CommandMetrics>();
builder.Services.AddSingleton<WebSocketHandler>();

builder.WebHost.UseUrls(urls);
builder.WebHost.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var serverOptions = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var localIpAddress = GetLocalIPv4();
var publicUrl = BuildPublicUrl(serverOptions.Urls, localIpAddress);

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = serverOptions.KeepAliveInterval
});

app.Map(serverOptions.WebSocketPath, async context =>
{
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
    await handler.HandleAsync(context);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/metrics", (CommandMetrics metrics) => Results.Ok(metrics.Snapshot()));

app.MapGet("/server-info", () => Results.Ok(new
{
    url = publicUrl ?? serverOptions.Urls,
    ip = localIpAddress
}));

app.Run();

static string? GetLocalIPv4()
{
    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (ni.OperationalStatus != OperationalStatus.Up)
        {
            continue;
        }

        if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
        {
            continue;
        }

        foreach (var addr in ni.GetIPProperties().UnicastAddresses)
        {
            if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
            {
                return addr.Address.ToString();
            }
        }
    }

    try
    {
        foreach (var addr in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
            {
                return addr.ToString();
            }
        }
    }
    catch
    {
    }

    return null;
}

static string? BuildPublicUrl(string urls, string? ip)
{
    if (string.IsNullOrWhiteSpace(ip))
    {
        return null;
    }

    var parts = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var firstUrl = parts.Length > 0 ? parts[0] : urls;

    if (Uri.TryCreate(firstUrl, UriKind.Absolute, out var uri))
    {
        var builder = new UriBuilder(uri)
        {
            Host = ip
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    return null;
}
