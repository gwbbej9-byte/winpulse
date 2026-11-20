using System.Diagnostics;

namespace WinPulse;

public readonly record struct HostStats(
    string MachineName,
    double CpuPercent,
    ulong RamUsedBytes,
    ulong RamTotalBytes);

public sealed class ProcessSnapshot
{
    public required string Name { get; init; }
    public int Pid { get; init; }
    public long WorkingSetBytes { get; init; }
    public long PeakWorkingSetBytes { get; init; }
    public double CpuPercent { get; init; }
    public int ThreadCount { get; init; }
}

public sealed class ProcessSampler
{
    private readonly Dictionary<int, (TimeSpan cpu, long ticks)> _prevCpu = new();
    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private bool _haveHostSample;

    public HostStats Host { get; private set; } = new(Environment.MachineName, double.NaN, 0, 0);

    public IReadOnlyList<ProcessSnapshot> Processes { get; private set; } = Array.Empty<ProcessSnapshot>();

    public void Tick()
    {
        Host = SampleHost();
        Processes = SampleProcesses();
    }

    private HostStats SampleHost()
    {
        ulong used = 0, total = 0;
        var mem = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        if (Native.GlobalMemoryStatusEx(ref mem) && mem.ullTotalPhys > 0)
        {
            total = mem.ullTotalPhys;
            used = mem.ullTotalPhys - mem.ullAvailPhys;
        }

        double cpu = double.NaN;
        if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong i = idle.ToUInt64();
            ulong k = kernel.ToUInt64();
            ulong u = user.ToUInt64();
            if (_haveHostSample)
            {
                ulong idleDelta = i - _prevIdle;
                ulong totalDelta = (k - _prevKernel) + (u - _prevUser);
                if (totalDelta > 0)
                {
                    double busy = (double)(totalDelta - idleDelta) / totalDelta * 100.0;
                    if (busy < 0) busy = 0;
                    if (busy > 100) busy = 100;
                    cpu = busy;
                }
            }
            _prevIdle = i;
            _prevKernel = k;
            _prevUser = u;
            _haveHostSample = true;
        }

        return new HostStats(Environment.MachineName, cpu, used, total);
    }

    private List<ProcessSnapshot> SampleProcesses()
    {
        int cores = Math.Max(1, Environment.ProcessorCount);
        long now = Stopwatch.GetTimestamp();
        double tickHz = Stopwatch.Frequency;
        var seen = new HashSet<int>();
        var list = new List<ProcessSnapshot>(256);

        Process[] procs;
        try
        {
            procs = Process.GetProcesses();
        }
        catch
        {
            return list;
        }

        foreach (var p in procs)
        {
            try
            {
                int pid;
                try { pid = p.Id; }
                catch { continue; }

                string name;
                try { name = p.ProcessName; }
                catch { name = "pid-" + pid; }

                long ws = 0;
                try { ws = p.WorkingSet64; } catch { /* access denied → 0 */ }

                long peak = 0;
                try { peak = p.PeakWorkingSet64; } catch { /* access denied → 0 */ }

                int threads = 0;
                try { threads = p.Threads.Count; } catch { /* access denied → 0 */ }

                TimeSpan cpuTime = TimeSpan.Zero;
                bool haveCpu = false;
                try
                {
                    cpuTime = p.TotalProcessorTime;
                    haveCpu = true;
                }
                catch { /* access denied → NaN CPU, row stays */ }

                seen.Add(pid);
                double cpuPct = double.NaN;
                if (haveCpu)
                {
                    if (_prevCpu.TryGetValue(pid, out var prev))
                    {
                        double wallSec = (now - prev.ticks) / tickHz;
                        if (wallSec > 0.05)
                        {
                            double cpuSec = (cpuTime - prev.cpu).TotalSeconds;
                            cpuPct = cpuSec / wallSec / cores * 100.0;
                            if (cpuPct < 0) cpuPct = 0;
                        }
                    }
                    _prevCpu[pid] = (cpuTime, now);
                }

                list.Add(new ProcessSnapshot
                {
                    Name = name,
                    Pid = pid,
                    WorkingSetBytes = ws,
                    PeakWorkingSetBytes = peak,
                    CpuPercent = cpuPct,
                    ThreadCount = threads
                });
            }
            finally
            {
                p.Dispose();
            }
        }

        if (_prevCpu.Count > seen.Count + 32)
        {
            var dead = _prevCpu.Keys.Where(id => !seen.Contains(id)).ToList();
            foreach (int id in dead)
                _prevCpu.Remove(id);
        }

        return list;
    }
}
