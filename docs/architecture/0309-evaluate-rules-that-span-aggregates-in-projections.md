# 0309. Evaluate rules that span aggregates in projections

- Layer: L2 Event model
- Status: Proposed
- Recorded: 2026-10-04
- Decision state in code: Planned
- Depends on:
  - [ADR-0300](0300-key-aggregates-by-hierarchical-paths.md): aggregates decide
    only on their own subtree; deleted references become inert.
  - [ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md): commands
    are decided into events.
  - [ADR-0305](0305-detect-conflicts-deterministically.md): conflicts are detected
    by path overlap and declared invariants.
  - [ADR-0307](0307-derive-reactions-and-subscriptions-from-projections.md):
    derived facts are projections.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Under [ADR-0300](0300-key-aggregates-by-hierarchical-paths.md), each aggregate
validates commands against its own subtree only. Some rules span aggregates: an
edge between two entities is valid only if both endpoints exist and are compatible,
but the endpoints live in other aggregates than the edge.

Concurrent edits make this worse. One peer changes an entity so that it no longer
fits an edge, while another peer adds that edge. Each event is valid against its
own aggregate, but together they are not.
[ADR-0305](0305-detect-conflicts-deterministically.md) detects this only through an
invariant declared at an ancestor of both paths, often the root, so every edit to
one would conflict with every concurrent edit to the other.

## Decision

1. **Aggregates never read outside their subtree.** Each aggregate's decider
   validates commands against its own state and declared invariants only.
2. **Rules that span aggregates are evaluated by projections** over the merged
   state. A reference that breaks such a rule is inert, like a reference to a
   deleted entity: consumers ignore it, and the projection publishes it as flagged.
   Breaking such a rule is never a conflict and never blocks editing.

## Alternatives considered

### Let deciders read other aggregates' state

Checks are immediate and exact. Conflict detection only covers the decider's own
subtree, so a concurrent change to the state it read goes undetected unless the
invariant is declared at a common ancestor, usually the root.

### Declare rules that span aggregates at the root

Every such rule is enforced as an invariant. Any two concurrent edits under the
root conflict, which makes live co-editing unusable.

### Dynamic consistency boundaries

Considered and rejected in
[ADR-0300](0300-key-aggregates-by-hierarchical-paths.md).

## Consequences

### Positive

- Conflict detection stays local to each aggregate's subtree.
- Merges never produce silent corruption: invalid references are visible and
  harmless.

### Negative and trade-offs

- States that break a rule spanning aggregates can exist after a merge, and users
  must repair them.

## Evidence

Required before acceptance:

- [ ] For a sample domain, list the rules that span aggregates and show that each
      can be evaluated by a projection.

## Fitness functions

- Architecture test: a decider depends only on its own aggregate's state type.
- Property test: for random concurrent edits, references that break a rule are
  inert and flagged, and state computation never fails.

## Review triggers

- Users regularly meet flagged states after merges.
- A rule spanning aggregates must be enforced before an event is accepted, not
  repaired afterwards.

## Notes

- 2026-10-04: Split from the former ADR-0040 when ADRs were grouped by layer.
  Application operations and the client API surface moved to the host layer.
