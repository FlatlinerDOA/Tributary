# 0022. Detect conflicts deterministically

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md): path overlap and
    declared invariants.
  - [ADR-0021](0021-merge-non-conflicting-edits-implicitly.md): the union in which
    conflicts are detected.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Some concurrent edits cannot be merged by a union: two different tempo changes, or
two new sends that together create a routing cycle. These must be surfaced, not
silently merged. Every peer holding the same events must agree on what conflicts,
or a resolution that is valid on one peer will be invalid on another.

Prior art: Pijul records conflicts as first-class state instead of blocking work.

## Decision

Two concurrent events conflict if either:

- their paths are equal, or one is an ancestor of the other, and their operations
  do not commute (per a specified commutativity table for each event type); or
- their union breaks an invariant declared on an aggregate whose subtree contains
  both of them.

Concurrent creation of an entity at the same path is always a conflict. An event
conflicts across its path's whole subtree, and is kept or rejected as a whole,
never in part.

Conflicts are state, not errors. An unresolved conflict does not block editing or
playback. The UI shows the competing versions as alternatives. Rendering and
exporting a conflicted state follow a deterministic rule.

## Alternatives considered

### Last writer wins for everything

There is never a conflict. Structural changes are lost silently, and invariants
can break.

### Block further editing until conflicts are resolved

State is always clean. Offline collaborators would be stopped by conflicts they
did not cause.

## Consequences

### Positive

- Every peer sees the same conflicts.
- Work continues while a conflict is open.

### Negative and trade-offs

- The detection rule must stay identical across peers and versions.
- Events at high paths conflict with anything concurrent beneath them.

## Evidence

Available:

- [Pijul](https://pijul.org) conflict model.

Required before acceptance:

- [ ] Specify the commutativity table for each event type and how invariants are
      evaluated over unions.
- [ ] Simulate two to four peers plus agents, including one peer offline for a
      long period making structural edits. Check that peers expose identical
      conflict sets.
- [ ] Measure the conflict rate per path-tree layout with N agents and one human.
- [ ] Choose the rendering and export rule for conflicted states (for example
      "lowest event hash wins", or refuse to export until resolved).

## Fitness functions

- Property test: any delivery order of the same events gives an identical conflict
  set on every peer.
- Property test: no non-conflicted state breaks a declared invariant.

## Review triggers

- Users hit conflicts often enough to disrupt live collaboration.
