using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

const int Block = 64;
const double Rate = 48000;

var o = Options.Parse(args);
var periodTicks = Stopwatch.Frequency * Block / Rate;
var periodUs = 1e6 * Block / Rate;

var engine = new Engine(o.Tracks);
var stages = engine.Calibrate(o.Load * periodUs);
Console.WriteLine($"runtime=.NET {Environment.Version} os=\"{RuntimeInformation.OSDescription}\" cpus={Environment.ProcessorCount} gc={(GCSettings.IsServerGC ? "server" : "workstation")} tracks={o.Tracks} stages={stages} alloc-threads={o.AllocThreads} alloc-mbps={(o.AllocMbps > 0 ? o.AllocMbps.ToString() : "unthrottled")} alloc-retain={o.AllocRetain} gcmode={o.GcMode}");

using var stop = new CancellationTokenSource();
var allocated = new long[o.AllocThreads];
var allocThreads = Enumerable.Range(0, o.AllocThreads)
    .Select(i => new Thread(() => allocated[i] = AllocLoad(i, o.AllocMbps * 1e6 / Math.Max(1, o.AllocThreads), o.AllocRetain, stop.Token)) { IsBackground = true })
    .ToArray();
var allocClock = Stopwatch.StartNew();
foreach (var t in allocThreads) t.Start();

if (o.GcMode == "lowlatency") GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
if (o.GcMode == "nogc") GC.TryStartNoGCRegion(240_000_000);
Console.WriteLine($"latency_mode={GCSettings.LatencyMode}");

using var gcMonitor = new GcPauseMonitor();
RunResult? result = null;
var rt = new Thread(() => result = RunRealTime(o, engine, periodTicks)) { Priority = ThreadPriority.Highest };
rt.Start();
rt.Join();
stop.Cancel();
foreach (var t in allocThreads) t.Join();
var allocSeconds = allocClock.Elapsed.TotalSeconds;

var r = result!;
var us = 1e6 / Stopwatch.Frequency;
Console.WriteLine($"rt_scheduling {r.Scheduling}");
Console.WriteLine($"blocks={r.Blocks} xruns={r.Xruns} dropped_buffers={r.DroppedBuffers} max_late_us={r.MaxLateTicks * us:F0} max_callback_us={r.MaxCallbackTicks * us:F0} p50_us={Percentile(r.Histogram, .5)} p99_us={Percentile(r.Histogram, .99)} p99.99_us={Percentile(r.Histogram, .9999)} mean_load={Mean(r.Histogram) / periodUs:P1} rt_thread_alloc_bytes={r.RtThreadAllocatedBytes}");
Console.WriteLine($"gc0={r.Gc0} gc1={r.Gc1} gc2={r.Gc2} total_pause_ms={r.TotalPause.TotalMilliseconds:F1} gc_time_pct={r.TotalPause.TotalSeconds / o.Seconds:P1} max_gc_pause_us={gcMonitor.MaxPause.TotalMicroseconds:F0} gc_pauses_observed={gcMonitor.Collections} alloc_mbps_actual={allocated.Sum() / 1e6 / allocSeconds:F0}");

static long AllocLoad(int seed, double bytesPerSecond, int retain, CancellationToken stop)
{
    var rng = new Random(seed);
    var retained = new object[retain];
    var clock = Stopwatch.StartNew();
    long allocated = 0, i = 0;
    while (!stop.IsCancellationRequested)
    {
        if (bytesPerSecond > 0 && allocated > bytesPerSecond * clock.Elapsed.TotalSeconds)
        {
            Thread.Sleep(1);
            continue;
        }
        var size = rng.Next(256, 64 * 1024);
        retained[i++ % retained.Length] = new byte[size];
        allocated += size;
        if ((i & 63) == 0)
        {
            var length = rng.Next(10, 5000);
            retained[rng.Next(retained.Length)] = new string('x', length).Split('x');
            allocated += length * 2L + length * 8L;
        }
    }
    return allocated;
}

static RunResult RunRealTime(Options o, Engine engine, double periodTicks)
{
    var scheduling = Scheduling.TryPromoteCurrentThread(o.RtPriority);
    var hist = new long[20_000];
    long xruns = 0, dropped = 0, blocks = 0, maxLate = 0, maxDur = 0;
    var gc0 = GC.CollectionCount(0); var gc1 = GC.CollectionCount(1); var gc2 = GC.CollectionCount(2);
    var pauseBefore = GC.GetTotalPauseDuration();
    var allocBefore = GC.GetAllocatedBytesForCurrentThread();
    var start = Stopwatch.GetTimestamp();
    var end = start + (long)(o.Seconds * Stopwatch.Frequency);
    var n = 0L;
    while (true)
    {
        var s = start + (long)(n * periodTicks);
        if (s >= end) break;
        while (Stopwatch.GetTimestamp() < s) Thread.SpinWait(8);
        var t0 = Stopwatch.GetTimestamp();
        engine.Process();
        var t1 = Stopwatch.GetTimestamp();
        var late = t1 - (start + (long)((n + 1) * periodTicks));
        var dur = t1 - t0;
        maxDur = Math.Max(maxDur, dur);
        hist[Math.Min(hist.Length - 1, (int)(dur * 1e6 / Stopwatch.Frequency))]++;
        blocks++;
        if (late > 0)
        {
            xruns++;
            maxLate = Math.Max(maxLate, late);
            var next = (long)Math.Ceiling((t1 - start) / periodTicks);
            dropped += next - (n + 1);
            n = next;
        }
        else n++;
    }
    var rtAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
    return new RunResult(scheduling, blocks, xruns, dropped, maxLate, maxDur, hist, rtAlloc,
        GC.CollectionCount(0) - gc0, GC.CollectionCount(1) - gc1, GC.CollectionCount(2) - gc2,
        GC.GetTotalPauseDuration() - pauseBefore);
}

static long Percentile(long[] h, double p)
{
    var target = (long)Math.Ceiling(h.Sum() * p);
    return h.Select((c, i) => (c, i))
        .Scan(0L, (acc, x) => acc + x.c)
        .Select((acc, i) => (acc, i))
        .FirstOrDefault(x => x.acc >= target, (0, h.Length - 1)).i;
}
static double Mean(long[] h) => h.Select((c, i) => (double)c * i).Sum() / Math.Max(1, h.Sum());

sealed record RunResult(string Scheduling, long Blocks, long Xruns, long DroppedBuffers, long MaxLateTicks, long MaxCallbackTicks,
    long[] Histogram, long RtThreadAllocatedBytes, int Gc0, int Gc1, int Gc2, TimeSpan TotalPause);

sealed record Options(double Seconds, double Load, int Tracks, int AllocThreads, double AllocMbps, int AllocRetain, string GcMode, int RtPriority)
{
    public static Options Parse(string[] a)
    {
        string Get(string k, string d) { var i = Array.IndexOf(a, "--" + k); return i >= 0 ? a[i + 1] : d; }
        return new(double.Parse(Get("seconds", "60")), double.Parse(Get("load", "0.7")), int.Parse(Get("tracks", "200")),
            int.Parse(Get("alloc-threads", "0")), double.Parse(Get("alloc-mbps", "200")), int.Parse(Get("alloc-retain", "4096")), Get("gc", "default"), int.Parse(Get("rt-priority", "50")));
    }
}

static class EnumerableExtensions
{
    public static IEnumerable<TAcc> Scan<T, TAcc>(this IEnumerable<T> source, TAcc seed, Func<TAcc, T, TAcc> step)
    {
        var acc = seed;
        foreach (var x in source) yield return acc = step(acc, x);
    }
}

sealed class GcPauseMonitor : IDisposable
{
    readonly CancellationTokenSource _stop = new();
    readonly Thread _thread;
    long _lastIndex;
    long _seen;
    TimeSpan _max;

    public GcPauseMonitor()
    {
        _lastIndex = GC.GetGCMemoryInfo(GCKind.Any).Index;
        _thread = new Thread(Poll) { IsBackground = true };
        _thread.Start();
    }

    public long Collections => Interlocked.Read(ref _seen);
    public TimeSpan MaxPause => _max;

    void Poll()
    {
        while (!_stop.IsCancellationRequested)
        {
            var info = GC.GetGCMemoryInfo(GCKind.Any);
            if (info.Index != _lastIndex)
            {
                _seen += info.Index - _lastIndex;
                _lastIndex = info.Index;
                var pause = info.PauseDurations.ToArray().Aggregate(TimeSpan.Zero, (a, b) => a + b);
                if (pause > _max) _max = pause;
            }
            Thread.Sleep(1);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _thread.Join();
        _stop.Dispose();
    }
}
