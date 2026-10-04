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
