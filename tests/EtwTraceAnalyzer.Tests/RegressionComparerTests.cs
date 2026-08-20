using EtwTraceAnalyzer.Models;
using EtwTraceAnalyzer.Services;

namespace EtwTraceAnalyzer.Tests;

public sealed class RegressionComparerTests
{
    [Fact]
    public void FlagsOnlyIncreasesBeyondThreshold()
    {
        var b = Report("a.exe", 100, 100, 1000, 1000, 1000);
        var c = Report("a.exe", 111, 100, 1050, 1201, 900);
        var result = new RegressionComparer().Compare(b, c, 10);
        var m = result.Processes.Single().Regressions.ToDictionary(x => x.Metric);
        Assert.True(m["cpuTimeMilliseconds"].Regressed);
        Assert.False(m["contextSwitches"].Regressed);
        Assert.False(m["diskReadBytes"].Regressed);
        Assert.True(m["diskWriteBytes"].Regressed);
        Assert.False(m["lifetimeMilliseconds"].Regressed);
    }

    [Fact]
    public void PositiveValueAgainstZeroIsRegression()
    {
        var result = new RegressionComparer().Compare(Report("a.exe", 0, 0, 0, 0, 0), Report("a.exe", 1, 1, 1, 1, 1), 50);
        Assert.All(result.Processes.Single().Regressions, r => Assert.True(r.Regressed));
    }

    [Fact]
    public void ProcessNamesAreCaseInsensitiveAndDuplicateRowsMerge()
    {
        var b = new TraceReport { TracePath = "b.etl", GeneratedAtUtc = DateTimeOffset.UtcNow, Processes = new[]
        {
            Metrics("App.EXE", 1, 20), Metrics("app.exe", 2, 30)
        }};
        var c = Report("APP.exe", 55, 50, 0, 0, 0);
        var comparison = Assert.Single(new RegressionComparer().Compare(b, c, 10).Processes);
        Assert.Equal(50, comparison.Baseline!.CpuTimeMilliseconds);
    }

    private static TraceReport Report(string name, double cpu, long switches, long read, long write, double life) => new()
    {
        TracePath = name + ".etl", GeneratedAtUtc = DateTimeOffset.UtcNow,
        Processes = new[] { new ProcessMetrics { RepresentativeProcessId = 42, ProcessName = name, CpuTimeMilliseconds = cpu, ContextSwitches = switches, DiskReadBytes = read, DiskWriteBytes = write, LifetimeMilliseconds = life } }
    };

    private static ProcessMetrics Metrics(string name, int pid, double cpu) => new() { RepresentativeProcessId = pid, ProcessName = name, CpuTimeMilliseconds = cpu };
}
