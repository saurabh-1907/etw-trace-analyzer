using EtwTraceAnalyzer.Models;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace EtwTraceAnalyzer.Services;

public sealed class TraceAnalyzer
{
    private sealed class MutableProcess
    {
        public int RepresentativePid;
        public string Name = "<unknown>";
        public double CpuMs;
        public long ContextSwitches;
        public long ReadBytes;
        public long WriteBytes;
        public DateTimeOffset? Start;
        public DateTimeOffset? End;
    }

    private sealed class ThreadState
    {
        public int ProcessId;
        public string ProcessName = "<unknown>";
    }

    private sealed class CpuState
    {
        public int? RunningThreadId;
        public double LastTimestampMsec;
    }

    public TraceReport Analyze(string path)
    {
        ValidatePath(path);
        var processes = new Dictionary<int, MutableProcess>();
        var threads = new Dictionary<int, ThreadState>();
        var cpuByProcessor = new Dictionary<int, CpuState>();
        var names = new Dictionary<int, string>();
        var eventCount = 0L;

        try
        {
            using var source = new ETWTraceEventSource(path);
            var kernel = new KernelTraceEventParser(source);

            kernel.ProcessStart += data =>
            {
                eventCount++;
                var p = GetProcess(processes, data.ProcessID, data.ProcessName);
                p.Start ??= ToUtc(data.TimeStamp);
                names[data.ProcessID] = p.Name;
            };
            kernel.ProcessDCStart += data =>
            {
                eventCount++;
                var p = GetProcess(processes, data.ProcessID, data.ProcessName);
                p.Start ??= ToUtc(data.TimeStamp);
                names[data.ProcessID] = p.Name;
            };
            kernel.ProcessStop += data =>
            {
                eventCount++;
                var p = GetProcess(processes, data.ProcessID, data.ProcessName);
                p.End = ToUtc(data.TimeStamp);
            };
            kernel.ProcessDCEnd += data =>
            {
                eventCount++;
                var p = GetProcess(processes, data.ProcessID, data.ProcessName);
                p.End ??= ToUtc(data.TimeStamp);
            };

            kernel.ThreadStart += data =>
            {
                eventCount++;
                threads[data.ThreadID] = new ThreadState { ProcessId = data.ProcessID, ProcessName = ResolveName(data.ProcessID, data.ProcessName, names) };
            };
            kernel.ThreadDCStart += data =>
            {
                eventCount++;
                threads[data.ThreadID] = new ThreadState { ProcessId = data.ProcessID, ProcessName = ResolveName(data.ProcessID, data.ProcessName, names) };
            };
            kernel.ThreadStop += data =>
            {
                eventCount++;
                threads.Remove(data.ThreadID);
            };
            kernel.ThreadDCEnd += data =>
            {
                eventCount++;
                threads.Remove(data.ThreadID);
            };

            kernel.ThreadCSwitch += data =>
            {
                eventCount++;
                var cpu = cpuByProcessor.GetValueOrDefault(data.ProcessorNumber) ?? new CpuState();
                cpuByProcessor[data.ProcessorNumber] = cpu;

                if (cpu.RunningThreadId is int oldRunning && data.TimeStampRelativeMSec >= cpu.LastTimestampMsec && threads.TryGetValue(oldRunning, out var owner))
                {
                    var elapsed = data.TimeStampRelativeMSec - cpu.LastTimestampMsec;
                    GetProcess(processes, owner.ProcessId, owner.ProcessName).CpuMs += elapsed;
                }

                var oldId = data.OldThreadID;
                if (oldId > 0 && threads.TryGetValue(oldId, out var oldThread))
                    GetProcess(processes, oldThread.ProcessId, oldThread.ProcessName).ContextSwitches++;

                cpu.RunningThreadId = data.NewThreadID;
                cpu.LastTimestampMsec = data.TimeStampRelativeMSec;
            };

            kernel.DiskIORead += data =>
            {
                eventCount++;
                AddIo(data, processes, names, true);
            };
            kernel.DiskIOWrite += data =>
            {
                eventCount++;
                AddIo(data, processes, names, false);
            };

            source.Process();
        }
        catch (Exception ex)
        {
            throw new TraceAnalysisException($"Unable to parse ETL trace '{path}'. The file may be malformed, truncated, inaccessible, or incompatible with the installed TraceEvent parser.", ex);
        }

        if (eventCount == 0)
            throw new TraceAnalysisException($"ETL trace '{path}' contained no decodable events.");

        return new TraceReport
        {
            TracePath = Path.GetFullPath(path),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Processes = processes.Values
                .Where(p => p.RepresentativePid > 0 || p.Name != "<unknown>")
                .OrderByDescending(p => p.CpuMs)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToImmutable)
                .ToArray()
        };
    }

    private static void AddIo(TraceEvent data, Dictionary<int, MutableProcess> processes, Dictionary<int, string> names, bool read)
    {
        var pid = data.ProcessID;
        if (pid <= 0) return;
        var size = ReadLong(data, "TransferSize", "IoSize", "IOSize", "Size", "TransferLength");
        if (size <= 0) return;
        var p = GetProcess(processes, pid, ResolveName(pid, data.ProcessName, names));
        if (read) p.ReadBytes = checked(p.ReadBytes + size);
        else p.WriteBytes = checked(p.WriteBytes + size);
    }

    private static MutableProcess GetProcess(Dictionary<int, MutableProcess> processes, int pid, string? name)
    {
        if (!processes.TryGetValue(pid, out var p))
        {
            p = new MutableProcess { RepresentativePid = pid, Name = NormalizeName(name) };
            processes.Add(pid, p);
        }
        else if (p.Name == "<unknown>" && !string.IsNullOrWhiteSpace(name))
        {
            p.Name = NormalizeName(name);
        }
        return p;
    }

    private static string ResolveName(int pid, string? eventName, Dictionary<int, string> names)
    {
        if (!string.IsNullOrWhiteSpace(eventName)) return NormalizeName(eventName);
        return names.TryGetValue(pid, out var known) ? known : "<unknown>";
    }

    private static string NormalizeName(string? value) => string.IsNullOrWhiteSpace(value) ? "<unknown>" : Path.GetFileName(value.Trim()).ToLowerInvariant();

    private static long ReadLong(TraceEvent data, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var value = data.PayloadByName(name);
                if (value is not null) return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { }
        }
        return 0;
    }

    private static DateTimeOffset ToUtc(DateTime timestamp) => new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

    private static ProcessMetrics ToImmutable(MutableProcess p)
    {
        var lifetime = p.Start.HasValue && p.End.HasValue ? Math.Max(0, (p.End.Value - p.Start.Value).TotalMilliseconds) : 0;
        return new ProcessMetrics
        {
            RepresentativeProcessId = p.RepresentativePid,
            ProcessName = p.Name,
            CpuTimeMilliseconds = Math.Max(0, p.CpuMs),
            ContextSwitches = p.ContextSwitches,
            DiskReadBytes = p.ReadBytes,
            DiskWriteBytes = p.WriteBytes,
            LifetimeMilliseconds = lifetime,
            StartTimeUtc = p.Start,
            EndTimeUtc = p.End
        };
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new TraceAnalysisException("An ETL path is required.");
        if (!File.Exists(path)) throw new TraceAnalysisException($"ETL file not found: '{path}'.");
        var info = new FileInfo(path);
        if (info.Length == 0) throw new TraceAnalysisException($"ETL file is empty: '{path}'.");
        if (!string.Equals(info.Extension, ".etl", StringComparison.OrdinalIgnoreCase)) throw new TraceAnalysisException($"Expected an .etl input, received '{info.Extension}'.");
    }
}
