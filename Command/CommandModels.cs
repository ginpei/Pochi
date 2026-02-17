namespace PowerPochi.Command;

public enum CommandType
{
    Next,
    Prev,
    StartPresentation,
    EndPresentation,
    Blackout,
    Whiteout
}

public sealed record CommandEnvelope(string? Type, string? Command, string? ClientId);

public sealed record CommandRequest(CommandType Command, string? ClientId);
