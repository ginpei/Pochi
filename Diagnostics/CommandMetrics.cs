using System.Collections.Concurrent;
using PowerPochi.Command;

namespace PowerPochi.Diagnostics;

public sealed record CommandMetricsSnapshot(
    IReadOnlyDictionary<CommandType, long> SuccessCounts,
    IReadOnlyDictionary<CommandType, long> FailureCounts,
    long ParseFailures);

public sealed class CommandMetrics
{
    private readonly ConcurrentDictionary<CommandType, long> _successCounts = new();
    private readonly ConcurrentDictionary<CommandType, long> _failureCounts = new();
    private long _parseFailures;

    public void RecordSuccess(CommandType command)
    {
        _successCounts.AddOrUpdate(command, 1, static (_, current) => current + 1);
    }

    public void RecordFailure(CommandType command)
    {
        _failureCounts.AddOrUpdate(command, 1, static (_, current) => current + 1);
    }

    public void RecordParseFailure()
    {
        Interlocked.Increment(ref _parseFailures);
    }

    public CommandMetricsSnapshot Snapshot()
    {
        return new CommandMetricsSnapshot(
            _successCounts.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value),
            _failureCounts.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value),
            Interlocked.Read(ref _parseFailures));
    }
}
