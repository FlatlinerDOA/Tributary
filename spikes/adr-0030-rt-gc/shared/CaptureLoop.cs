using System.Diagnostics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
struct CaptureRing
{
    public long WriteIndex;
    public long ReadIndex;
    public long Capacity;
}

[StructLayout(LayoutKind.Sequential)]
struct CaptureStats
{
    public long Periods;
    public long LatePeriods;
    public long DeviceOverruns;
    public long DeviceLostSamples;
    public long RingOverflowSamples;
    public long MaxLateTicks;
    public long TicksPerSecond;
    public long AllocatedBytes;
    public int Done;
}

/// <summary>
/// Simulates a capture device: every period it delivers 64 samples carrying a running sequence number.
/// The device buffers <c>devicePeriods</c> periods; if the capture thread is later than that, the oldest
/// samples are lost, as an overrun would lose them on hardware.
/// </summary>
static unsafe class CaptureLoop
{
    public const int Block = 64;
    const double Rate = 48000;

    public static void Run(CaptureRing* ring, int* data, CaptureStats* stats, double seconds, int devicePeriods)
    {
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var periodTicks = Stopwatch.Frequency * Block / Rate;
        stats->TicksPerSecond = Stopwatch.Frequency;
        var start = Stopwatch.GetTimestamp();
        var end = start + (long)(seconds * Stopwatch.Frequency);
        long n = 0, sequence = 0;
        while (true)
        {
            var due = start + (long)((n + 1) * periodTicks);
            if (due >= end) break;
            while (Stopwatch.GetTimestamp() < due) Thread.SpinWait(8);
            var now = Stopwatch.GetTimestamp();
            var available = Math.Max(1, (long)((now - start) / periodTicks) - n);
            if (available > 1) stats->LatePeriods++;
            stats->MaxLateTicks = Math.Max(stats->MaxLateTicks, now - due);
            if (available > devicePeriods)
            {
                var lost = (available - devicePeriods) * Block;
                stats->DeviceOverruns++;
                stats->DeviceLostSamples += lost;
                sequence += lost;
                n += available - devicePeriods;
                available = devicePeriods;
            }
            for (var p = 0; p < available; p++) sequence = Write(ring, data, stats, sequence);
            n += available;
            stats->Periods = n;
        }
        stats->AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        Volatile.Write(ref stats->Done, 1);
    }

    static long Write(CaptureRing* ring, int* data, CaptureStats* stats, long sequence)
    {
        var write = ring->WriteIndex;
        if (write + Block - Volatile.Read(ref ring->ReadIndex) > ring->Capacity)
        {
            stats->RingOverflowSamples += Block;
            return sequence + Block;
        }
        for (var i = 0; i < Block; i++) data[(write + i) % ring->Capacity] = (int)(sequence + i);
        Volatile.Write(ref ring->WriteIndex, write + Block);
        return sequence + Block;
    }
}
