namespace EtwTraceAnalyzer.Models;

public sealed class TraceReport
{
    public string TracePath { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public IReadOnlyList<ProcessMetrics> Processes { get; init; } = Array.Empty<ProcessMetrics>();
}
