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

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var serverOptions = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;

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

app.Run();
