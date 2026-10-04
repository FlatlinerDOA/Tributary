# 0703. Route real-time gestures through a fast path

- Layer: L6 Host and API
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0302](0302-authorize-actors-with-path-scoped-capabilities.md): the fast path
    is authorized like any other action.
  - [ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md): where
    coalesced gestures are recorded.
  - [ADR-0601](0601-swap-immutable-graph-snapshots-to-the-realtime-thread.md): the
    engine's lock-free parameter queue.
  - [ADR-0700](0700-drive-the-engine-through-one-typed-api.md): coalesced gestures
    become ordinary commands.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Turning a knob or playing MPE input produces hundreds of value changes per second.
Sending each change through command validation, signing and the event DAG would
add latency and bloat history. These values still need to be recorded, as
automation or as the final parameter state.

Unresolved questions:

- How faithful is coalescing? Is it lossless for automation recording and lossy
  (end value only) for parameter tweaks?
- How much of a gesture can be lost in a crash, and how often are coalesced events
  flushed during a long gesture?
- Do remote peers hear gestures live (as a transient stream) or only after
  coalescing?

## Decision

Send interactive gesture values to the engine's parameter queue without going
through the command layer: no deciding, signing or event appending on the way. Record the gesture afterwards as coalesced
commands at gesture end and at a bounded interval during long gestures. These carry
engine sample timestamps so that recorded automation is sample-accurate. The fast
path requires a capability covering the target parameter's path.

## Alternatives considered

### Every value change becomes an event

This is fully faithful. It adds latency to the audio path and inflates history by
orders of magnitude.

### Gestures are never recorded

The live path is simplest, but users lose their parameter changes and cannot
record automation.

## Consequences

### Positive

- Controller-to-audio latency is limited by the engine, not by the event DAG.
- History holds intent-level events.

### Negative and trade-offs

- The engine and the DAG disagree briefly during a gesture.
- A crash can lose up to one flush interval of a gesture.

## Evidence

Required before acceptance:

- [ ] Measure controller-to-audio latency on the fast path and through the full
      command path.
- [ ] Measure the coalescing ratio and reconstruction error (for example maximum
      deviation after curve simplification) for recorded automation.
- [ ] Specify the flush interval and the crash-loss bound.

## Fitness functions

- Test: replaying the coalesced events reproduces the final parameter state
  exactly, and reproduces automation within the specified error bound.

## Review triggers

- Users notice that recorded automation does not match what they played.

## Notes

- 2026-10-04: Renumbered from the former ADR-0032 when ADRs were grouped by layer.
