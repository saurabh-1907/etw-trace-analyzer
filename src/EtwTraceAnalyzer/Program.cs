using System.Text.Json;
using System.Text.Json.Serialization;
using EtwTraceAnalyzer.Services;

namespace EtwTraceAnalyzer;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            var analyzer = new TraceAnalyzer();
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

            if (options.Baseline is not null || options.Candidate is not null)
            {
                if (options.Baseline is null || options.Candidate is null)
                    throw new TraceAnalysisException("Baseline mode requires both --baseline and --candidate.");
                var comparison = new RegressionComparer().Compare(analyzer.Analyze(options.Baseline), analyzer.Analyze(options.Candidate), options.ThresholdPercent);
                WriteOutput(comparison, options.Output, jsonOptions);
                return comparison.Processes.Any(p => p.Regressions.Any(r => r.Regressed)) ? 2 : 0;
            }

            if (options.Input is null)
                throw new TraceAnalysisException("Provide an ETL path or use --baseline <etl> --candidate <etl>.");
            WriteOutput(analyzer.Analyze(options.Input), options.Output, jsonOptions);
            return 0;
        }
        catch (TraceAnalysisException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static void WriteOutput<T>(T value, string? output, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(value, options);
        if (string.IsNullOrWhiteSpace(output)) { Console.WriteLine(json); return; }
        var full = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, json);
        Console.WriteLine($"Wrote {full}");
    }
}

internal sealed class CliOptions
{
    public string? Input { get; private init; }
    public string? Baseline { get; private init; }
    public string? Candidate { get; private init; }
    public string? Output { get; private init; }
    public double ThresholdPercent { get; private init; } = 10;

    public static CliOptions Parse(string[] args)
    {
        if (args.Any(a => a is "--help" or "-h")) { PrintHelp(); Environment.Exit(0); }
        string? input = null, baseline = null, candidate = null, output = null;
        var threshold = 10.0;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--baseline": baseline = Next(args, ref i, args[i]); break;
                case "--candidate": candidate = Next(args, ref i, args[i]); break;
                case "--output": output = Next(args, ref i, args[i]); break;
                case "--threshold":
                    if (!double.TryParse(Next(args, ref i, args[i]), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out threshold) || threshold < 0)
                        throw new TraceAnalysisException("--threshold must be a non-negative number expressed as a percentage.");
                    break;
                default:
                    if (args[i].StartsWith('-')) throw new TraceAnalysisException($"Unknown option '{args[i]}'. Use --help for usage.");
                    if (input is not null) throw new TraceAnalysisException("Only one positional ETL input is supported.");
                    input = args[i];
                    break;
            }
        }
        return new CliOptions { Input = input, Baseline = baseline, Candidate = candidate, Output = output, ThresholdPercent = threshold };
    }

    private static string Next(string[] args, ref int index, string option)
    {
        if (++index >= args.Length) throw new TraceAnalysisException($"Missing value for {option}.");
        return args[index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("ETW Trace Analyzer");
        Console.WriteLine("Usage:");
        Console.WriteLine("  etw-trace-analyzer <trace.etl> [--output report.json]");
        Console.WriteLine("  etw-trace-analyzer --baseline baseline.etl --candidate candidate.etl [--threshold 10] [--output comparison.json]");
        Console.WriteLine("Exit codes: 0 = success/no regressions, 1 = input/runtime error, 2 = regressions detected.");
    }
}
