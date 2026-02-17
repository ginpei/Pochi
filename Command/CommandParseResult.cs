namespace PowerPochi.Command;

public sealed record CommandParseResult(bool IsSuccess, CommandRequest? Command, string? Error)
{
    public static CommandParseResult Success(CommandRequest command) => new(true, command, null);
    public static CommandParseResult Failure(string error) => new(false, null, error);
}
