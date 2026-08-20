namespace EtwTraceAnalyzer.Services;

public sealed class TraceAnalysisException : Exception
{
    public TraceAnalysisException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
