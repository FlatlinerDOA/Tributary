# 0021. Merge non-conflicting edits implicitly

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md): the DAG
    whose heads are merged.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Concurrent edits produce several heads in the event DAG. Most concurrent edits do
not interfere: two people editing different tracks, or adding notes to the same
clip. Asking anyone to merge these would make live collaboration impossible.

If every peer writes its own merge node, those nodes are themselves concurrent and
need merging again (merge storms).

Prior art: Automerge defines document state by a set of heads and merges
implicitly. git requires an explicit merge commit for every fork.

## Decision

Define the state at a set of heads as the deterministic union of their events. Do
not store merge nodes. The next event an actor writes simply lists every head it
saw as a parent.

Fine-grained content (notes, automation points, clip placement) is expressed as
CRDT-typed command parameters. Its events commute, and each content type has a
merge rule written as plain data rules (for example set union, or last writer wins
with ties broken by event hash) in the format specification. These rules are
versioned with the schema.

## Alternatives considered

### Explicit merge node for every fork

History records every merge. Every peer writes its own merge node, which leads to
merge storms, and live co-editing becomes mostly merge nodes.

### Deterministic merge nodes with no author

Peers compute identical nodes, which deduplicate. They still add a node per fork
and carry no information that the union rule does not already define.

## Consequences

### Positive

- Most concurrent editing merges with no user involvement and no extra events.
- No merge storms.

### Negative and trade-offs

- The merge rule for each content type is part of the format forever. It is the
  only merge logic that runs again on replay, so it must stay simple, purely
  data-driven rules.
- Live co-editing produces wide DAGs.

## Evidence

Available:

- [Automerge](https://automerge.org) change-graph design.

Required before acceptance:

- [ ] Specify the merge rule for each CRDT content type (sequence, register, map,
      set) as plain text in the format spec.
- [ ] Measure the CRDT metadata overhead per note.
- [ ] Measure DAG width during live co-editing.

## Fitness functions

- Convergence property test: any delivery order of the same set of events gives
  identical state.
- Golden fixtures: recorded concurrent DAGs replay to identical bytes in every
  future version.

## Review triggers

- A content type needs a merge rule that cannot be written as plain data rules.
