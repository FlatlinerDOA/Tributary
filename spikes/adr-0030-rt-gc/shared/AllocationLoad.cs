using System.Diagnostics;

static class AllocationLoad
{
    public static long Run(int seed, double bytesPerSecond, int retain, CancellationToken stop)
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
}
