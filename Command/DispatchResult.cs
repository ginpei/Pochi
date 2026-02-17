namespace PowerPochi.Command;

public sealed record DispatchResult(bool IsSuccess, string? Error)
{
    public static DispatchResult Success() => new(true, null);
    public static DispatchResult Failure(string error) => new(false, error);
}
