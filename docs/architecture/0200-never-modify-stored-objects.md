# 0200. Never modify stored objects

- Layer: L1 Storage
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on: None
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Content addressing assumes that a hash always identifies the same bytes.
Deduplication, sync and verification all depend on it. Conventional DAWs overwrite
audio files in place when editing destructively, which would break every address
that refers to them.

Assumptions:

- Users will accept that storage grows with every import.

Unresolved questions:

- Garbage collection: history keeps every object alive. Is there a "prune history
  before X" operation?
- Recording: is a take immutable once the transport stops, or chunk by chunk while
  it is recording, for crash safety?

## Decision

Never modify an object after it is stored. The storage API offers no update or
overwrite operation. A changed object is a new object with a new address.

## Alternatives considered

### Mutable objects with versioned addresses

Edits in place are cheap. Addresses stop identifying content, so deduplication,
sync and verification need extra version bookkeeping.

## Consequences

### Positive

- Deduplication and sync stay valid because a hash never changes meaning.

### Negative and trade-offs

- Storage only shrinks if history is pruned.

## Evidence

Required before acceptance:

- [ ] Propose a design for recording crash safety that keeps stored objects
      immutable.
- [ ] Propose a history-pruning design or explicitly decide not to have one.

## Fitness functions

- The storage API has no update or overwrite operation (enforced by an interface
  test).

## Review triggers

- Storage growth becomes a common user complaint.

## Notes

- 2026-10-04: Split from the former ADR-0004 when ADRs were grouped by layer. This
  record keeps the storage rule. Representing audio edits as render-time transforms
  moved to the domain layer.
