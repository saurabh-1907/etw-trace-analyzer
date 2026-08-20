using EtwTraceAnalyzer.Services;

namespace EtwTraceAnalyzer.Tests;

public sealed class FixtureTraceTests
{
    [Fact]
    public void RecordedFixturesAreParseableWhenPresent()
    {
        var roots = new[] { Path.Combine(AppContext.BaseDirectory, "fixtures"), Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", "fixtures")) };
        var fixtures = roots.Where(Directory.Exists).SelectMany(x => Directory.GetFiles(x, "*.etl")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (fixtures.Length == 0) return;
        var analyzer = new TraceAnalyzer();
        foreach (var fixture in fixtures)
        {
            var report = analyzer.Analyze(fixture);
            Assert.NotNull(report);
            Assert.False(string.IsNullOrWhiteSpace(report.TracePath));
        }
    }
}
