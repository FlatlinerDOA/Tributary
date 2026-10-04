# 0501. Resolve audio edits as render-time transforms

- Layer: L4 Domain model
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0200](0200-never-modify-stored-objects.md): stored audio is never modified.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Destructive edits and "consolidate" operations in conventional DAWs create new
audio files, lose the link to the original recording, and add artefacts each time
a stretch is applied on top of another.

Assumptions:

- Resolving transforms at render time, together with a render cache, costs an
  acceptable amount of CPU at playback.

## Decision

Represent every edit to imported or recorded audio (trim, warp, stretch, gain,
consolidate, reverse) as a transform that the system resolves from the original
source at render time. Every transform names the algorithm and version that
resolves it.

Running a render pass over a graph section never modifies a stored source. It
creates a new immutable source from the section's output, recording the inputs,
algorithms and versions it was made from.

## Alternatives considered

### Destructive editing

This is simple and uses little storage. It loses the original, cannot be undone
across sessions, and breaks content addressing.

### Copy-on-edit rendered files (conventional consolidate)

Playback stays cheap. Stretches stack and add artefacts, and storage grows anyway
because originals are usually kept.

## Consequences

### Positive

- Edits can always be undone, and the original quality is always available.

### Negative and trade-offs

- Playback has to resolve transforms, so it relies on caching.

## Evidence

Required before acceptance:

- [ ] Measure CPU for resolving transforms on 100 warped clips, with a cold cache
      and with a warm cache.

## Fitness functions

- Test: every edit operation on a clip leaves its source's address unchanged.

## Review triggers

- A feature needs in-place sample editing that cannot be expressed as a transform.

## Notes

- 2026-10-04: Split from the former ADR-0004 when ADRs were grouped by layer.
  Consolidate only mixes inputs and stays a render-time transform.
