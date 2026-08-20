# ETW Trace Analyzer

A Windows-only .NET 8 CLI for analyzing ETW `.etl` traces with Microsoft's `Microsoft.Diagnostics.Tracing.TraceEvent` library.

## What it measures

For each process observed in a trace, the analyzer reports:

- CPU time reconstructed from kernel context-switch events.
- Context-switch count attributed to the process.
- Physical disk read and write bytes from kernel disk I/O events.
- Process lifetime from process start/stop events.

The parser is event-driven and does not require converting the ETL into an ETLX file.

## Requirements

- Windows 10/11 or Windows Server with a supported .NET 8 SDK/runtime.
- A trace captured with the kernel events needed by the metrics. In particular, CPU time requires context-switch events, and disk bytes require kernel disk I/O events.
- `Microsoft.Diagnostics.Tracing.TraceEvent` 3.2.5.

The project targets `net8.0-windows` because ETW is a Windows facility.

## CLI

Single trace:

```powershell
dotnet run --project .\src\EtwTraceAnalyzer -- .\trace.etl --output .\report.json
```

Baseline versus candidate:

```powershell
dotnet run --project .\src\EtwTraceAnalyzer -- `
  --baseline .\baseline.etl `
  --candidate .\candidate.etl `
  --threshold 10 `
  --output .\comparison.json
```

`--threshold` is a percentage. A candidate metric is flagged when it increases by more than that percentage. For zero baselines, any positive candidate value is considered a regression.

## Output

A single-trace report contains the trace path and an array of per-process records. A comparison report contains the baseline and candidate process metrics plus per-metric regression flags.

Baseline/candidate matching uses a normalized executable/process name rather than PID because PIDs commonly differ between independent captures. If the same executable appears multiple times in a trace, those instances are aggregated under that executable name.

## CPU accounting

Context switches identify which thread was running before each switch. The analyzer maintains the currently running thread per processor and accumulates the elapsed timestamp between switches to the process owning the old thread. Because the first running thread on a processor is not known until the trace establishes it, CPU time can be lower than wall-clock time for that initial interval.

This is intentional: the analyzer does not invent CPU time for intervals that the trace cannot establish.

## I/O accounting

The analyzer listens to kernel disk read/write events. It extracts byte counts from the decoded payload (`TransferSize`, `IoSize`, `IOSize`, `Size`, or `TransferLength`) so it remains tolerant of provider/event versions that expose the size under different names. Only events with a valid process ID and positive size are included. File I/O events are intentionally not combined with disk I/O because doing so can double-count the same operation.

## Error handling

Missing files, non-ETL inputs, empty files, inaccessible files, and parser exceptions are converted to a concise CLI error and a non-zero exit code. A malformed or truncated ETL is never reported as a successful analysis.

## Tests and fixtures

The test suite covers regression calculations, metric aggregation, malformed/truncated input handling, and ETL parsing. Windows ETL fixture tests expect recorded `.etl` files under `tests/fixtures/`.

Because ETL fixtures are binary and machine/environment dependent, this repository does not fabricate fixture bytes. On a Windows test machine, place real recorded fixtures there and run:

```powershell
dotnet test
```

The fixture test skips cleanly when no `.etl` fixture is present; the parser/error-path tests do not depend on fixtures.

## Trace capture example

The repository includes `scripts/capture-fixture.ps1`, which uses `xperf` to record a short kernel ETL containing process/thread, context-switch, and disk I/O activity. Run it on a Windows machine with the Windows Performance Toolkit installed, then copy the resulting `.etl` into `tests/fixtures/` if you want the recorded-fixture test to exercise it.

## Limitations

- Process identity across traces is executable name, not a globally unique process instance.
- CPU time depends on context-switch coverage in the input trace.
- Physical disk I/O is only visible when the trace contains the relevant kernel disk events.
- ETW event loss in the source trace cannot be reconstructed by the analyzer.
