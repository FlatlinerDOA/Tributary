# ADR-0030 spike: GC suspension vs. real-time deadlines

Throwaway harness for the evidence required by
[ADR-0030](../../docs/architecture/0030-implement-the-realtime-engine-in-csharp.md).
Not production code, and deliberately outside the IoC/test structure the main codebase uses.

Workload (identical in `csharp/` and `rust/`): N tracks, each a sine oscillator through
S biquad stages, summed to a stereo bus, one 64-sample block per 48 kHz period (1.333 ms).
S is calibrated at start-up so the mean callback time hits the target DSP load.
Blocks are paced by deadline; a block is an xrun if it completes after its deadline
(start of the next period). No audio device is involved, so device and driver latency are not measured.

```
dotnet run -c Release --project csharp -- --seconds 300 --load 0.7 --alloc-threads 2   # same-process allocation load
dotnet run -c Release --project csharp -- --seconds 300 --load 0.7                     # isolated
cargo run --release --manifest-path rust/Cargo.toml -- --seconds 300 --load 0.7        # baseline
```

## Running on a desktop

Prerequisites: .NET 10 SDK, Rust toolchain, bash (WSL or Git Bash on Windows).

```
git fetch origin ccr-5fbb3a14-r2ucog && git checkout ccr-5fbb3a14-r2ucog
dotnet build -c Release csharp
cargo build --release --manifest-path rust/Cargo.toml
./run-matrix.sh 300        # writes results/<utc-timestamp>-matrix.txt
```

For a meaningful run, close other applications, disable power saving, and where possible raise the
engine's scheduling priority (Linux: `chrt -f 50`, Windows: High priority class). Add `--gc nogc` to try
`GC.TryStartNoGCRegion`. Note the host hardware in the results file; the container run is not comparable.

Reading the output: a C#-vs-Rust gap in `xruns` / `max_late_us` at the same `mean_load` is attributable to the
runtime; a gap between `isolated` and `alloc-load` is attributable to GC suspension. `max_suspension_us` comes
from runtime event dispatch and is approximate; trust `max_late_us` and `total_pause_ms`.

## Status for ADR-0030 Evidence

- Done in container (4 shared vCPU, no RT scheduling): harness, smoke runs, 120 s matrix (see `results/`).
- Still needed on real hardware: long soak (target 8 h), pass thresholds agreed in advance, then update the
  ADR's Evidence checklist. iOS/Android NativeAOT needs devices and is not covered by this harness.
