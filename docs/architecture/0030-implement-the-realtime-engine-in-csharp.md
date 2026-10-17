# 0030. Implement the real-time engine in C#

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0001](0001-drive-the-engine-through-one-typed-api.md): the engine runs
    behind the typed API, which allows a separate process.
  - [ADR-0002](0002-keep-the-core-managed-with-optional-native-providers.md): the
    managed-first principle this decision applies, with its native exception.
  - [ADR-0003](0003-target-the-latest-dotnet-release.md): the runtime whose GC and
    NativeAOT behaviour the spike measures. A runtime upgrade repeats the soak test.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review. The decision is conditional on the spike described under Evidence.

## Context

The real-time audio thread must never miss a deadline. At 64 samples and 48 kHz,
each buffer has about 1.33 ms, shared by every node.

**Main risk:** even if the RT thread never allocates, the .NET GC can suspend every
thread that runs managed code. Ephemeral (gen0/gen1) collections are blocking. Any
allocation anywhere in the process can trigger one, including UI work, agents and
deserialization. Pauses are usually short, but the longest pauses decide whether
audio drops out.

Possible mitigations to measure:

- Run the engine in its own process, behind the typed API, so that almost nothing
  allocates there and GCs are rare.
- `GC.TryStartNoGCRegion` around playback, `GCSettings.LatencyMode`, and tuned
  GC configuration.
- Run the RT callback on a native thread that calls into managed code. It is still
  suspended if a GC happens while it runs managed code.

## Decision

Implement the engine in C# under a strict real-time discipline: no allocation, no
locks and no blocking calls on the RT path, enforced by analyzers. Run it in a
dedicated engine process. If the spike below fails its thresholds, fall back to a
Rust real-time kernel behind a C ABI, and keep graph compilation in C#.

## Alternatives considered

### Rust core behind a C ABI

It has no GC, its memory safety comes with RT-friendly idioms, and it has a growing
audio ecosystem. It adds a second language, FFI boundaries and native packaging.

### C++ core (JUCE or a custom engine)

This is the industry norm with the most mature tooling and plugin SDK experience.
It is memory-unsafe, carries the same native costs as Rust, and has JUCE licensing
terms.

### Hybrid: C# graph compiler and a native per-buffer kernel

GC is kept off the hot path. The domain stays managed. The FFI surface is
narrower than in a full Rust core.

## Consequences

### Positive

- One language across domain, storage and engine.

### Negative and trade-offs

- An RT discipline must be followed and enforced in a language that does not help
  with it.
- The risk is in the tail behaviour of the GC, which can only be shown with long
  measurements.

## Evidence

Available:

- Spike harness: [`spikes/adr-0030-rt-gc/`](../../spikes/adr-0030-rt-gc/). The same
  synthetic workload in C# and Rust: deadline-paced 64-sample blocks at 48 kHz,
  driver-style resync after an xrun, RT thread promoted (`SCHED_FIFO` on Linux,
  MMCSS "Pro Audio" on Windows), and throttled same-process allocation load.
- BenchmarkDotNet with `MemoryDiagnoser` shows `Engine.Process()` allocates zero
  bytes per block. The soak harness reports zero bytes allocated on the RT thread.
  This proves only that the synthetic DSP path is allocation-free, not that GC
  pauses are tolerable.
- NativeAOT isolation test (same spike, `capture-shim/` and `capture-host/`). A C#
  library published with NativeAOT carries its own runtime and GC heap. A simulated
  capture thread running inside it, allocation-free and writing to a native ring,
  kept its overruns at the no-load noise floor (29–33 in 30 s, in a VM) under up to
  about 1,900 host GCs with pauses up to 188 ms. The same loop on a host thread rose
  to 207, 293 and 1,457 overruns as allocation pressure increased. A large ring
  absorbed every pause of the managed consumer.
  - What it shows: recording capture can be kept out of the engine's GC without
    leaving C#. The engine and monitoring path stay exposed to the GC.
  - What it doesn't cover: driver-created callback threads, desktop hardware,
    Android, and iOS, where the whole app is a single NativeAOT image, so there is
    no second runtime.

A clean run of N blocks bounds the xrun rate below about 3/N at 95% confidence
(about 2.7 million blocks per hour). Run length therefore sets the claim: 2 hours
supports "fewer than one xrun per 40 minutes", and 8 hours "fewer than one per
2.7 hours".

Required for the go/no-go decision (C# engine or Rust kernel fallback):

- [ ] Agree pass thresholds in advance. Proposed: zero xruns over 2 hours at 70%
      DSP load, isolated and with 200 MB/s same-process allocation.
- [ ] Engine with 200 tracks of synthetic DSP, 64-sample buffer at 48 kHz, for
      2 hours while other threads in the same process allocate at a fixed rate.
      Record xruns, dropped buffers and the maximum GC pause. Sweep allocation rate
      and live-set size to show which one drives the pause length.
- [ ] Repeat with the engine isolated in its own process (no allocating load).
- [ ] The same workload in Rust as a baseline on the same host.

Required before acceptance:

- [ ] One 8-hour soak of the chosen configuration on desktop hardware, to catch
      effects that only appear over hours: OS background work, thermal throttling,
      heap fragmentation and timer drift.
- [ ] NativeAOT on iOS and Android: check startup and that it works with the audio
      callback.

## Fitness functions

- A Roslyn analyzer flags allocations or locks in code marked `[RealTime]`.
- A soak test in CI counts xruns.

## Review triggers

- The .NET GC gains a mode with bounded pauses, or a runtime regression changes
  pause behaviour.
