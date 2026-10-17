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
