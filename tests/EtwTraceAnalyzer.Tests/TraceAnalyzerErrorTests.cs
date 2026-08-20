using EtwTraceAnalyzer.Services;

namespace EtwTraceAnalyzer.Tests;

public sealed class TraceAnalyzerErrorTests
{
    [Fact]
    public void MissingFileIsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".etl");
        var ex = Assert.Throws<TraceAnalysisException>(() => new TraceAnalyzer().Analyze(path));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyEtlIsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".etl");
        File.WriteAllBytes(path, Array.Empty<byte>());
        try { var ex = Assert.Throws<TraceAnalysisException>(() => new TraceAnalyzer().Analyze(path)); Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TruncatedEtlIsReportedAsParseFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".etl");
        File.WriteAllBytes(path, Enumerable.Range(0, 256).Select(i => (byte)i).ToArray());
        try { var ex = Assert.Throws<TraceAnalysisException>(() => new TraceAnalyzer().Analyze(path)); Assert.Contains("Unable to parse ETL", ex.Message, StringComparison.OrdinalIgnoreCase); }
        finally { File.Delete(path); }
    }
}
