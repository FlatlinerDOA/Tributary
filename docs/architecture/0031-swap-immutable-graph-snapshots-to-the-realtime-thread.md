# 0031. Swap immutable graph snapshots to the real-time thread

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md): vertices and the
    routing aggregate the graph is compiled from.
  - [ADR-0024](0024-derive-reactions-and-subscriptions-from-projections.md): the
    graph is compiled from projections and recompiled on their changes.
  - [ADR-0030](0030-implement-the-realtime-engine-in-csharp.md): the RT discipline
    this design serves.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

The audio graph changes during playback as projections change. The RT thread must
never wait for a lock, and it must never free memory that another thread is using.
The engine is multi-core and schedules nodes by their dependencies. It must also
render offline deterministically.

Points to consider:

- Snapshots that are replaced must be freed off the RT thread. In C# the GC does
  this, but native plugin handles need explicit lifetime management.
- Changing the topology during playback can click unless the old and new graphs are
  crossfaded.
- Deterministic offline rendering needs a fixed order for floating-point sums,
  whatever the thread count.

## Decision

Compile the audio graph from the project's projections (track and bus vertices
plus the routing aggregate) on a non-RT thread into an immutable snapshot that
includes the schedule, buffers and delay compensation. Publish it to the RT thread
with a single atomic reference swap at a buffer boundary. Plugin and stateful node
instances live outside the snapshot so that their state survives a swap. Parameter
changes reach the RT thread through a lock-free queue.

## Alternatives considered

### Mutable graph protected by a lock

Updates are simple, but priority inversion and blocking on the RT thread are
unacceptable.

### Incremental edit messages to the RT thread

There is no full recompile. The RT thread does mutation work, and its timing
becomes harder to bound.

## Consequences

### Positive

- The RT thread never waits for a lock. Graph changes are atomic.

### Negative and trade-offs

- Recompiling large graphs costs CPU, so incremental compilation may be needed.
- Topology changes need crossfades.

## Evidence

Required before acceptance:

- [ ] Benchmark compile time for a 200-track graph.
- [ ] Glitch test: 1,000 random topology changes during playback with zero xruns
      and no detectable discontinuities.
- [ ] Offline render with 1, 4 and 16 threads produces identical output bytes.

## Fitness functions

- Determinism test in CI comparing offline render bytes across thread counts.

## Review triggers

- Graph compile latency is noticeable when making edits.
