using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime;
using System.Runtime.InteropServices;

const int Block = 64;
const double Rate = 48000;

var o = Options.Parse(args);
var periodTicks = Stopwatch.Frequency * Block / Rate;
var periodUs = 1e6 * Block / Rate;

var gcListener = new GcPauseListener();
var engine = new Engine(o.Tracks);
var stages = engine.Calibrate(o.Load * periodUs);
Console.WriteLine($"runtime=.NET {Environment.Version} gc={(GCSettings.IsServerGC ? "server" : "workstation")} concurrent={GCSettings.LatencyMode} tracks={o.Tracks} stages={stages} alloc-threads={o.AllocThreads} gcmode={o.GcMode}");

var stop = false;
var allocThreads = Enumerable.Range(0, o.AllocThreads)
    .Select(i => new Thread(() => AllocLoad(i, () => stop)) { IsBackground = true })
    .ToArray();
foreach (var t in allocThreads) t.Start();

if (o.GcMode == "lowlatency") GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
if (o.GcMode == "nogc") GC.TryStartNoGCRegion(240_000_000);

var rt = new Thread(() => RunRealTime(o, engine, periodTicks, periodUs, gcListener)) { Priority = ThreadPriority.Highest };
rt.Start();
rt.Join();
stop = true;

static void AllocLoad(int seed, Func<bool> stop)
{
    var rng = new Random(seed);
    var retained = new object[4096];
    long i = 0;
    while (!stop())
    {
        var size = rng.Next(256, 64 * 1024);
        retained[i++ % retained.Length] = new byte[size];
        if ((i & 63) == 0) retained[rng.Next(retained.Length)] = new string('x', rng.Next(10, 5000)).Split('x');
    }
}

static void RunRealTime(Options o, Engine engine, double periodTicks, double periodUs, GcPauseListener gc)
{
    Console.WriteLine($"sched_fifo_priority={o.RtPriority} result={Scheduling.TrySetFifo(o.RtPriority)}");
    var hist = new long[20_000];
    long xruns = 0, blocks = 0, maxLateTicks = 0, maxDurTicks = 0;
    var gcBefore = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    var pauseBefore = GC.GetTotalPauseDuration();
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
        var deadline = start + (long)((n + 1) * periodTicks);
        var late = t1 - deadline;
        if (late > 0) { xruns++; maxLateTicks = Math.Max(maxLateTicks, late); }
        var dur = t1 - t0;
        maxDurTicks = Math.Max(maxDurTicks, dur);
        hist[Math.Min(hist.Length - 1, (int)(dur * 1e6 / Stopwatch.Frequency))]++;
        blocks++;
        n++;
    }
    var us = 1e6 / Stopwatch.Frequency;
    var gcAfter = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    Console.WriteLine($"blocks={blocks} xruns={xruns} max_late_us={maxLateTicks * us:F0} max_callback_us={maxDurTicks * us:F0} p50_us={Percentile(hist, .5)} p99_us={Percentile(hist, .99)} p99.99_us={Percentile(hist, .9999)} mean_load={Mean(hist) / periodUs:P1}");
    Console.WriteLine($"gc0={gcAfter.Item1 - gcBefore.Item1} gc1={gcAfter.Item2 - gcBefore.Item2} gc2={gcAfter.Item3 - gcBefore.Item3} total_pause_ms={(GC.GetTotalPauseDuration() - pauseBefore).TotalMilliseconds:F1} suspensions={gc.Count} max_suspension_us={gc.MaxSuspensionUs:F0}");
}

static long Percentile(long[] h, double p)
{
    var total = h.Sum(); var target = (long)Math.Ceiling(total * p); long acc = 0;
    for (var i = 0; i < h.Length; i++) { acc += h[i]; if (acc >= target) return i; }
    return h.Length - 1;
}
static double Mean(long[] h) => h.Select((c, i) => (double)c * i).Sum() / Math.Max(1, h.Sum());

sealed record Options(double Seconds, double Load, int Tracks, int AllocThreads, string GcMode, int RtPriority)
{
    public static Options Parse(string[] a)
    {
        string Get(string k, string d) { var i = Array.IndexOf(a, "--" + k); return i >= 0 ? a[i + 1] : d; }
        return new(double.Parse(Get("seconds", "60")), double.Parse(Get("load", "0.7")), int.Parse(Get("tracks", "200")), int.Parse(Get("alloc-threads", "0")), Get("gc", "default"), int.Parse(Get("rt-priority", "50")));
    }
}

sealed class Engine
{
    const int Block = 64;
    readonly int _tracks;
    int _stages = 1;
    float[] _state = [];
    readonly float[] _phase, _inc;
    readonly float[] _l = new float[Block], _r = new float[Block];

    public Engine(int tracks)
    {
        _tracks = tracks;
        _phase = new float[tracks];
        _inc = Enumerable.Range(0, tracks).Select(i => 2 * MathF.PI * (110 + 7 * i) / 48000f).ToArray();
        SetStages(1);
    }

    void SetStages(int stages) { _stages = stages; _state = new float[_tracks * stages * 2]; }

    public int Calibrate(double targetUs)
    {
        for (var s = 1; s < 4096; s++)
        {
            SetStages(s);
            for (var i = 0; i < 2000; i++) Process();
            var t0 = Stopwatch.GetTimestamp();
            for (var i = 0; i < 2000; i++) Process();
            var us = (Stopwatch.GetTimestamp() - t0) * 1e6 / Stopwatch.Frequency / 2000;
            if (us >= targetUs) return s;
        }
        return _stages;
    }

    public void Process()
    {
        var l = _l.AsSpan(); var r = _r.AsSpan();
        l.Clear(); r.Clear();
        const float b0 = 0.2f, b1 = 0.4f, b2 = 0.2f, a1 = -0.3f, a2 = 0.1f;
        for (var t = 0; t < _tracks; t++)
        {
            var ph = _phase[t]; var inc = _inc[t];
            var pan = (t & 1) == 0 ? 0.6f : 0.4f;
            for (var i = 0; i < Block; i++)
            {
                var x = Sin(ph); ph += inc; if (ph > MathF.PI) ph -= 2 * MathF.PI;
                for (var s = 0; s < _stages; s++)
                {
                    var k = (t * _stages + s) * 2;
                    var y = b0 * x + _state[k];
                    _state[k] = b1 * x - a1 * y + _state[k + 1];
                    _state[k + 1] = b2 * x - a2 * y;
                    x = y;
                }
                l[i] += x * pan; r[i] += x * (1 - pan);
            }
            _phase[t] = ph;
        }
    }

    static float Sin(float x) { var x2 = x * x; return x * (1f - x2 * (0.16605f - x2 * 0.00761f)); }
}

sealed class GcPauseListener : EventListener
{
    long _begin;
    long _count;
    long _maxTicks;
    public long Count => Interlocked.Read(ref _count);
    public double MaxSuspensionUs => Interlocked.Read(ref _maxTicks) * 1e6 / Stopwatch.Frequency;

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, EventLevel.Informational, (EventKeywords)0x1);
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (e.EventId == 9) _begin = Stopwatch.GetTimestamp();
        else if (e.EventId == 3 && _begin != 0)
        {
            var d = Stopwatch.GetTimestamp() - _begin;
            Interlocked.Increment(ref _count);
            long cur;
            while (d > (cur = Interlocked.Read(ref _maxTicks)) && Interlocked.CompareExchange(ref _maxTicks, d, cur) != cur) { }
        }
    }
}

static class Scheduling
{
    [StructLayout(LayoutKind.Sequential)]
    struct SchedParam { public int Priority; }

    [DllImport("libc", SetLastError = true)] static extern nint pthread_self();
    [DllImport("libc", SetLastError = true)] static extern int pthread_setschedparam(nint thread, int policy, ref SchedParam param);

    const int SchedFifo = 1;

    public static string TrySetFifo(int priority)
    {
        if (priority <= 0) return "disabled";
        if (!OperatingSystem.IsLinux()) return "unsupported-os";
        var param = new SchedParam { Priority = priority };
        var rc = pthread_setschedparam(pthread_self(), SchedFifo, ref param);
        return rc == 0 ? "ok" : $"failed(errno={rc})";
    }
}
