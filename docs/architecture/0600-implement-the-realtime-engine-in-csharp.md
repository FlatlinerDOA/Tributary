# 0600. Implement the real-time engine in C#

- Layer: L5 Engine
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0100](0100-keep-the-core-managed-with-optional-native-providers.md): the
    managed-first principle this decision applies, with its native exception.
  - [ADR-0101](0101-target-the-latest-dotnet-release.md): the runtime whose GC and
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

- Run the engine in its own process, behind an API, so that almost nothing
  allocates there and GCs are rare.
- `GC.TryStartNoGCRegion` around playback, `GCSettings.LatencyMode`, and tuned
  GC configuration.
- Run the RT callback on a native thread that calls into managed code. It is still
  suspended if a GC happens while it runs managed code.

## Decision

Implement the engine in C# under a strict real-time discipline: no allocation, no
locks and no blocking calls on the RT path, enforced by analyzers. The engine
depends on nothing outside its own layer and lower layers, never starts processes
or threads it does not own, and talks to the outside only through channels, so it
can run in a dedicated process apart from code that allocates heavily. If the spike below fails its thresholds, fall back to a
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

Required before acceptance (spike):

- [ ] Engine process with 200 tracks of synthetic DSP, 64-sample buffer at 48 kHz,
      running for 8 hours while a separate thread in the same process allocates
      heavily to force GCs. Record xruns and the maximum GC suspension time.
- [ ] Repeat with the engine isolated in its own process (no allocating load).
- [ ] The same workload in Rust as a baseline.
- [ ] NativeAOT on iOS and Android: check startup and that it works with the audio
      callback.
- [ ] On an iOS and an Android device, run the engine in the same process as an
      allocating host and UI workload, because mobile apps cannot run the engine in
      a separate process. Record xruns and the maximum GC suspension time, and
      compare with a native real-time kernel.
- [ ] Agree pass thresholds in advance (for example zero xruns over 8 hours at
      70% DSP load).

## Fitness functions

- A Roslyn analyzer flags allocations or locks in code marked `[RealTime]`.
- A soak test in CI counts xruns.

## Review triggers

- The .NET GC gains a mode with bounded pauses, or a runtime regression changes
  pause behaviour.

## Notes

- 2026-10-04: Renumbered from the former ADR-0030 when ADRs were grouped by layer.
