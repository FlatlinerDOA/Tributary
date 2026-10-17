using System.Runtime.InteropServices;

static unsafe class Exports
{
    [UnmanagedCallersOnly(EntryPoint = "capture_start")]
    static int Start(CaptureRing* ring, int* data, CaptureStats* stats, double seconds, int devicePeriods, int rtPriority)
    {
        var thread = new Thread(() =>
        {
            Console.WriteLine($"shim rt_scheduling {Scheduling.TryPromoteCurrentThread(rtPriority)}");
            CaptureLoop.Run(ring, data, stats, seconds, devicePeriods);
        });
        thread.Start();
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "capture_gc_count")]
    static int GcCount() => GC.CollectionCount(0);
}
