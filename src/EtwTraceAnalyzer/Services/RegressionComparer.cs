using EtwTraceAnalyzer.Models;

namespace EtwTraceAnalyzer.Services;

public sealed class RegressionComparer
{
    public ComparisonReport Compare(TraceReport baseline, TraceReport candidate, double thresholdPercent)
    {
        if (double.IsNaN(thresholdPercent) || double.IsInfinity(thresholdPercent) || thresholdPercent < 0)
            throw new ArgumentOutOfRangeException(nameof(thresholdPercent), "Threshold must be a finite non-negative percentage.");

        var baselineByName = baseline.Processes
            .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Merge(g), StringComparer.OrdinalIgnoreCase);
        var candidateByName = candidate.Processes
            .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Merge(g), StringComparer.OrdinalIgnoreCase);

        var names = baselineByName.Keys.Union(candidateByName.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        var comparisons = new List<ProcessComparison>();

        foreach (var name in names)
        {
            baselineByName.TryGetValue(name, out var b);
            candidateByName.TryGetValue(name, out var c);
            var regressions = new List<MetricRegression>();
            AddMetric(regressions, "cpuTimeMilliseconds", b?.CpuTimeMilliseconds ?? 0, c?.CpuTimeMilliseconds ?? 0, thresholdPercent);
            AddMetric(regressions, "contextSwitches", b?.ContextSwitches ?? 0, c?.ContextSwitches ?? 0, thresholdPercent);
            AddMetric(regressions, "diskReadBytes", b?.DiskReadBytes ?? 0, c?.DiskReadBytes ?? 0, thresholdPercent);
            AddMetric(regressions, "diskWriteBytes", b?.DiskWriteBytes ?? 0, c?.DiskWriteBytes ?? 0, thresholdPercent);
            AddMetric(regressions, "lifetimeMilliseconds", b?.LifetimeMilliseconds ?? 0, c?.LifetimeMilliseconds ?? 0, thresholdPercent);
            comparisons.Add(new ProcessComparison { ProcessName = name, Baseline = b, Candidate = c, Regressions = regressions });
        }

        return new ComparisonReport
        {
            BaselinePath = baseline.TracePath,
            CandidatePath = candidate.TracePath,
            ThresholdPercent = thresholdPercent,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Processes = comparisons
        };
    }

    private static void AddMetric(List<MetricRegression> output, string name, double baseline, double candidate, double threshold)
    {
        var change = baseline == 0 ? (candidate > 0 ? double.PositiveInfinity : 0) : ((candidate - baseline) / Math.Abs(baseline)) * 100.0;
        output.Add(new MetricRegression { Metric = name, Baseline = baseline, Candidate = candidate, ChangePercent = change, Regressed = candidate > baseline && change > threshold });
    }

    private static ProcessMetrics Merge(IEnumerable<ProcessMetrics> values)
    {
        var list = values.ToArray();
        return new ProcessMetrics
        {
            RepresentativeProcessId = list.FirstOrDefault()?.RepresentativeProcessId ?? 0,
            ProcessName = list.FirstOrDefault()?.ProcessName ?? "<unknown>",
            CpuTimeMilliseconds = list.Sum(x => x.CpuTimeMilliseconds),
            ContextSwitches = list.Sum(x => x.ContextSwitches),
            DiskReadBytes = list.Sum(x => x.DiskReadBytes),
            DiskWriteBytes = list.Sum(x => x.DiskWriteBytes),
            LifetimeMilliseconds = list.Sum(x => x.LifetimeMilliseconds),
            StartTimeUtc = list.Where(x => x.StartTimeUtc.HasValue).Select(x => x.StartTimeUtc).Min(),
            EndTimeUtc = list.Where(x => x.EndTimeUtc.HasValue).Select(x => x.EndTimeUtc).Max()
        };
    }
}
