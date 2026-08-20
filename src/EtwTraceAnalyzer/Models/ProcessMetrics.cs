namespace EtwTraceAnalyzer.Models;

public sealed class ProcessMetrics
{
    public int RepresentativeProcessId { get; init; }
    public string ProcessName { get; init; } = "<unknown>";
    public double CpuTimeMilliseconds { get; init; }
    public long ContextSwitches { get; init; }
    public long DiskReadBytes { get; init; }
    public long DiskWriteBytes { get; init; }
    public double LifetimeMilliseconds { get; init; }
    public DateTimeOffset? StartTimeUtc { get; init; }
    public DateTimeOffset? EndTimeUtc { get; init; }
}
