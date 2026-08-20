namespace EtwTraceAnalyzer.Models;

public sealed class MetricRegression
{
    public string Metric { get; init; } = string.Empty;
    public double Baseline { get; init; }
    public double Candidate { get; init; }
    public double ChangePercent { get; init; }
    public bool Regressed { get; init; }
}

public sealed class ProcessComparison
{
    public string ProcessName { get; init; } = string.Empty;
    public ProcessMetrics? Baseline { get; init; }
    public ProcessMetrics? Candidate { get; init; }
    public IReadOnlyList<MetricRegression> Regressions { get; init; } = Array.Empty<MetricRegression>();
}

public sealed class ComparisonReport
{
    public string BaselinePath { get; init; } = string.Empty;
    public string CandidatePath { get; init; } = string.Empty;
    public double ThresholdPercent { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public IReadOnlyList<ProcessComparison> Processes { get; init; } = Array.Empty<ProcessComparison>();
}
