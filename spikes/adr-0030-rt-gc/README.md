# ADR-0030 spike: GC suspension vs. real-time deadlines

Throwaway harness for the evidence required by
[ADR-0030](../../docs/architecture/0030-implement-the-realtime-engine-in-csharp.md).
The soak harness is not production code and sits outside the IoC/test structure of the main codebase,
because BenchmarkDotNet cannot express a deadline-paced loop under cross-thread GC interference.
The per-block DSP cost is measured with BenchmarkDotNet in `benchmarks/`.

Workload (identical in `csharp/` and `rust/`): N tracks, each a sine oscillator through
S biquad stages, summed to a stereo bus, one 64-sample block per 48 kHz period (1.333 ms).
S is calibrated at start-up so the mean callback time hits the target DSP load.
Blocks are paced by deadline and the RT thread busy-spins between blocks. A block is an xrun if it
completes after its deadline (start of the next period); the schedule then resyncs to the next period
boundary, as a driver would, and the skipped periods are counted as `dropped_buffers`.
No audio device is involved, so device and driver latency are not measured.

The RT thread is promoted at start-up and the result is printed as `rt_scheduling`:

| OS      | Mechanism                                                        |
|---------|------------------------------------------------------------------|
| Linux   | `SCHED_FIFO` at `--rt-priority` (default 50); needs `CAP_SYS_NICE` or root |
| Windows | `timeBeginPeriod(1)` + MMCSS "Pro Audio" task at `AVRT_PRIORITY_CRITICAL`; no admin needed |
| macOS   | not implemented (`unsupported-os`)                               |

## Running

Prerequisites: .NET 10 SDK, Rust toolchain, bash (Git Bash or WSL on Windows; WSL measures the Linux VM, not Windows).

```
git fetch origin ccr-5fbb3a14-r2ucog && git checkout ccr-5fbb3a14-r2ucog
dotnet build -c Release csharp
cargo build --release --manifest-path rust/Cargo.toml
./run-matrix.sh 300                                    # full matrix, 18 runs, ~90 min
LOADS=0.7 ALLOC_MBPS=200 ALLOC_RETAIN=256 ./run-matrix.sh 28800   # one 8 h soak per row
dotnet run -c Release --project benchmarks -- --filter '*'        # BenchmarkDotNet, Engine.Process()
```

Results go to `results/<utc-timestamp>-matrix.txt`; the first line records the host OS, CPU and (Windows) power plan.
Close other applications and use a high-performance power plan.

C# options: `--seconds`, `--load` (0–1), `--tracks` (200), `--alloc-threads` (0 = isolated),
`--alloc-mbps` (total across threads, default 200, 0 = unthrottled), `--alloc-retain` (objects each allocating
thread keeps live; 256 ≈ 8 MB, 4096 ≈ 128 MB), `--gc default|lowlatency|nogc`, `--rt-priority` (0 disables promotion).
Rust takes `--seconds`, `--load`, `--tracks`, `--rt-priority`.

## Reading the output

- C# isolated vs. Rust at the same `mean_load`: cost of the runtime without GC.
- Isolated vs. alloc rows: cost of GC suspension. The alloc-mbps × alloc-retain sweep shows whether pause length
  follows allocation rate or live-set size.
- `rt_thread_alloc_bytes` must be 0; anything else means the RT path allocates.
- `max_gc_pause_us` is the longest single collection pause, polled from `GC.GetGCMemoryInfo` every 1 ms.
  `gc_time_pct` is total pause time over the run length.
- `max_late_us` is the worst single overrun; `xruns` counts late blocks, `dropped_buffers` the periods lost to them.
- `steal_ticks_*` (Linux only) is hypervisor steal; any increase means the host is virtualised and results are noisy.
- Benchmarks: `Allocated` must be `-` (zero) for every row; `Mean` per block divided by 1333 µs is the DSP load.

## Status for ADR-0030 Evidence

- Container (4 shared vCPU): unusable as evidence. Hypervisor steal causes xruns even in Rust under `SCHED_FIFO`.
- `results/20261004T024408Z-matrix.txt` (Windows desktop, before resync, throttling and MMCSS): isolated C# within
  a few xruns of Rust over 300 s; alloc rows invalid (unthrottled allocator, cascading schedule).
- Still needed: rerun this matrix on the desktop, agree pass thresholds, 8 h soak, then update the ADR's Evidence
  checklist. iOS/Android NativeAOT needs devices and is not covered by this harness.
