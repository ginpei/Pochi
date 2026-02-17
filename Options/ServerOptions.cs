namespace PowerPochi.Options;

public class ServerOptions
{
    public string Urls { get; set; } = "http://0.0.0.0:5000";
    public string WebSocketPath { get; set; } = "/ws";
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
    public string? Token { get; set; }
}
