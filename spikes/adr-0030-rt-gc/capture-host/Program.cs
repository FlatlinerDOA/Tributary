using System.Diagnostics;
using System.Runtime.InteropServices;

var o = Options.Parse(args);
Console.WriteLine($"capture={o.Capture} runtime=.NET {Environment.Version} os=\"{RuntimeInformation.OSDescription}\" alloc-threads={o.AllocThreads} alloc-mbps={o.AllocMbps} alloc-retain={o.AllocRetain} device-periods={o.DevicePeriods} ring-ms={o.RingMs}");
var result = Capture.Run(o);
Console.WriteLine(result.Summary);

sealed record Options(string Capture, double Seconds, int AllocThreads, double AllocMbps, int AllocRetain, int RtPriority, int DevicePeriods, int RingMs, string ShimPath)
{
    public static Options Parse(string[] a)
    {
        string Get(string k, string d) { var i = Array.IndexOf(a, "--" + k); return i >= 0 ? a[i + 1] : d; }
        var defaultShim = Path.Combine(AppContext.BaseDirectory, "../../../../capture-shim/out",
            OperatingSystem.IsWindows() ? "capture_shim.dll" : OperatingSystem.IsMacOS() ? "capture_shim.dylib" : "capture_shim.so");
        return new(Get("capture", "shim"), double.Parse(Get("seconds", "60")), int.Parse(Get("alloc-threads", "2")),
            double.Parse(Get("alloc-mbps", "200")), int.Parse(Get("alloc-retain", "4096")), int.Parse(Get("rt-priority", "50")),
            int.Parse(Get("device-periods", "3")), int.Parse(Get("ring-ms", "2000")), Path.GetFullPath(Get("shim", defaultShim)));
    }
}

sealed record CaptureResult(string Summary);

static unsafe class Capture
{
    public static CaptureResult Run(Options o)
    {
        var capacity = (long)(48000L * o.RingMs / 1000 / CaptureLoop.Block * CaptureLoop.Block);
        var ring = (CaptureRing*)NativeMemory.AllocZeroed((nuint)sizeof(CaptureRing));
        var stats = (CaptureStats*)NativeMemory.AllocZeroed((nuint)sizeof(CaptureStats));
        var data = (int*)NativeMemory.AllocZeroed((nuint)(capacity * sizeof(int)));
        ring->Capacity = capacity;

        using var stop = new CancellationTokenSource();
        var allocated = new long[o.AllocThreads];
        var allocThreads = Enumerable.Range(0, o.AllocThreads)
            .Select(i => new Thread(() => allocated[i] = AllocationLoad.Run(i, o.AllocMbps * 1e6 / Math.Max(1, o.AllocThreads), o.AllocRetain, stop.Token)) { IsBackground = true })
            .ToArray();
        var consumer = new Consumer(ring, data);
        var consumerThread = new Thread(() => consumer.Drain(stop.Token)) { IsBackground = true };
        using var gcMonitor = new GcPauseMonitor();
        var gcBefore = GC.CollectionCount(0);
        var clock = Stopwatch.StartNew();
        foreach (var t in allocThreads) t.Start();
        consumerThread.Start();

        Func<int> shimGcCount = () => -1;
        if (o.Capture == "shim")
        {
            var lib = NativeLibrary.Load(o.ShimPath);
            var start = (delegate* unmanaged<CaptureRing*, int*, CaptureStats*, double, int, int, int>)NativeLibrary.GetExport(lib, "capture_start");
            var gcCount = (delegate* unmanaged<int>)NativeLibrary.GetExport(lib, "capture_gc_count");
            shimGcCount = () => gcCount();
            start(ring, data, stats, o.Seconds, o.DevicePeriods, o.RtPriority);
        }
        else
        {
            new Thread(() =>
            {
                Console.WriteLine($"managed rt_scheduling {Scheduling.TryPromoteCurrentThread(o.RtPriority)}");
                CaptureLoop.Run(ring, data, stats, o.Seconds, o.DevicePeriods);
            }) { Priority = ThreadPriority.Highest }.Start();
        }

        while (Volatile.Read(ref stats->Done) == 0) Thread.Sleep(10);
        Thread.Sleep(50);
        stop.Cancel();
        consumerThread.Join();
        foreach (var t in allocThreads) t.Join();

        var us = 1e6 / stats->TicksPerSecond;
        var summary =
            $"periods={stats->Periods} late_periods={stats->LatePeriods} max_late_us={stats->MaxLateTicks * us:F0} " +
            $"device_overruns={stats->DeviceOverruns} device_lost_samples={stats->DeviceLostSamples} ring_overflow_samples={stats->RingOverflowSamples} capture_thread_alloc_bytes={stats->AllocatedBytes}\n" +
            $"consumer_samples={consumer.Samples} consumer_gaps={consumer.Gaps} consumer_missing_samples={consumer.MissingSamples} " +
            $"host_gc0={GC.CollectionCount(0) - gcBefore} host_max_gc_pause_us={gcMonitor.MaxPause.TotalMicroseconds:F0} shim_gc0={shimGcCount()} " +
            $"alloc_mbps_actual={allocated.Sum() / 1e6 / clock.Elapsed.TotalSeconds:F0}";
        NativeMemory.Free(data);
        NativeMemory.Free(stats);
        NativeMemory.Free(ring);
        return new CaptureResult(summary);
    }
}

sealed unsafe class Consumer(CaptureRing* ring, int* data)
{
    long _expected;
    public long Samples { get; private set; }
    public long Gaps { get; private set; }
    public long MissingSamples { get; private set; }

    public void Drain(CancellationToken stop)
    {
        while (true)
        {
            var write = Volatile.Read(ref ring->WriteIndex);
            for (var read = ring->ReadIndex; read < write; read++) Check(data[read % ring->Capacity]);
            Volatile.Write(ref ring->ReadIndex, write);
            if (stop.IsCancellationRequested && Volatile.Read(ref ring->WriteIndex) == write) return;
            Thread.Sleep(1);
        }
    }

    void Check(int sequence)
    {
        if (sequence != (int)_expected)
        {
            Gaps++;
            MissingSamples += (uint)(sequence - (int)_expected);
        }
        _expected = sequence + 1L;
        Samples++;
    }
}
