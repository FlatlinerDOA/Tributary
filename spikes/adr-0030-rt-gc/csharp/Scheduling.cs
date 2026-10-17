using System.Runtime.InteropServices;

static class Scheduling
{
    [StructLayout(LayoutKind.Sequential)]
    struct SchedParam { public int Priority; }

    [DllImport("libc")] static extern nint pthread_self();
    [DllImport("libc")] static extern int pthread_setschedparam(nint thread, int policy, ref SchedParam param);

    [DllImport("avrt", CharSet = CharSet.Unicode, SetLastError = true)] static extern nint AvSetMmThreadCharacteristicsW(string taskName, ref uint taskIndex);
    [DllImport("avrt", SetLastError = true)] static extern bool AvSetMmThreadPriority(nint avrtHandle, int priority);
    [DllImport("winmm")] static extern uint timeBeginPeriod(uint periodMs);

    const int SchedFifo = 1;
    const int AvrtPriorityCritical = 2;

    public static string TryPromoteCurrentThread(int fifoPriority)
    {
        if (fifoPriority <= 0) return "disabled";
        if (OperatingSystem.IsLinux())
        {
            var param = new SchedParam { Priority = fifoPriority };
            var rc = pthread_setschedparam(pthread_self(), SchedFifo, ref param);
            return rc == 0 ? $"sched_fifo({fifoPriority})=ok" : $"sched_fifo({fifoPriority})=failed(errno={rc})";
        }
        if (OperatingSystem.IsWindows())
        {
            var timer = timeBeginPeriod(1) == 0 ? "ok" : "failed";
            uint taskIndex = 0;
            var handle = AvSetMmThreadCharacteristicsW("Pro Audio", ref taskIndex);
            if (handle == 0) return $"timer_1ms={timer} mmcss_pro_audio=failed(err={Marshal.GetLastWin32Error()})";
            var prio = AvSetMmThreadPriority(handle, AvrtPriorityCritical) ? "ok" : $"failed(err={Marshal.GetLastWin32Error()})";
            return $"timer_1ms={timer} mmcss_pro_audio=ok avrt_priority_critical={prio}";
        }
        return "unsupported-os";
    }
}
