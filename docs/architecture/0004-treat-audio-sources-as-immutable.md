# 0004. Treat audio sources as immutable

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on: None
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Destructive edits and "consolidate" operations in conventional DAWs create new
audio files, lose the link to the original recording, and add artefacts each time
a stretch is applied on top of another. Content addressing assumes that a hash
always identifies the same bytes.

Assumptions:

- Resolving transforms at render time, together with a render cache, costs an
  acceptable amount of CPU at playback.
- Users will accept that storage grows with every import.

Unresolved questions:

- Garbage collection: history keeps every source alive. Is there a "prune history
  before X" operation?
- Recording: is a take immutable once the transport stops, or chunk by chunk while
  it is recording, for crash safety?

## Decision

Never modify imported or recorded audio after it is stored. Represent every edit
(trim, warp, stretch, gain, consolidate, reverse) as a transform that the system
resolves from the original source at render time. Every transform names the
algorithm and version that resolves it.

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
- Deduplication and sync stay valid because a hash never changes meaning.

### Negative and trade-offs

- Playback has to resolve transforms, so it relies on caching.
- Storage only shrinks if history is pruned.

## Evidence

Required before acceptance:

- [ ] Measure CPU for resolving transforms on 100 warped clips, with a cold cache
      and with a warm cache.
- [ ] Propose a design for recording crash safety that keeps sources immutable.
- [ ] Propose a history-pruning design or explicitly decide not to have one.

## Fitness functions

- The source storage API has no update or overwrite operation (enforced by an
  interface test).

## Review triggers

- Storage growth becomes a common user complaint.
- A feature needs in-place sample editing that cannot be expressed as a transform.

## Notes

- 2026-10-04: The Decision gained a paragraph saying that a render pass over a graph section creates a new immutable source. Consolidate, which only mixes inputs, stays a render-time transform.
