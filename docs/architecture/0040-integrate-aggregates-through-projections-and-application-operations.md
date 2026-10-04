# 0040. Integrate aggregates through projections and application operations

- Status: Proposed
- Recorded: 2026-10-04
- Decision state in code: Planned
- Depends on:
  - [ADR-0001](0001-drive-the-engine-through-one-typed-api.md): every client uses
    one typed API.
  - [ADR-0007](0007-make-api-serialization-pluggable.md): client artefacts are
    generated from the C# contract.
  - [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md): aggregates decide
    only on their own subtree; deleted references become inert.
  - [ADR-0018](0018-select-paths-with-one-pattern-language.md): subscription
    selectors.
  - [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md): commands
    are decided into events.
  - [ADR-0022](0022-detect-conflicts-deterministically.md): conflicts are detected
    by path overlap and declared invariants.
  - [ADR-0024](0024-derive-reactions-and-subscriptions-from-projections.md):
    derived facts are projections; clients subscribe by path.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Under [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md), each aggregate
validates commands against its own subtree only. Many rules span aggregates:

- A routing edge is valid only if its endpoints exist and their signal types match,
  but the endpoints live under `/tracks` and `/buses`, not `/routing`.
- One user action can touch several aggregates, for example creating a filter and
  connecting it.

Concurrent edits make this worse. Peer A changes a track from audio to MIDI while
peer B connects that track to an audio sink. Each event is valid against its own
aggregate, but together they are not. ADR-0022 detects this only through an
invariant declared at an ancestor of both paths, which here is the root, so every
routing edit would conflict with every concurrent track edit.

Clients also need a usable surface. A React UI wants state to bind to and simple
value changes. Agents over MCP work best with a small set of intent-level tools.
Neither should need to know aggregate boundaries or event shapes.

## Decision

1. **Aggregates never read outside their subtree.** Each aggregate's decider
   validates commands against its own state and declared invariants only.
2. **Rules that span aggregates are evaluated by projections.** A projection
   evaluates them over the merged state. A reference that breaks such a rule (for
   example an edge whose endpoint types do not match) is inert, like a reference
   to a deleted entity: it is ignored when compiling the graph, and the projection
   publishes it as flagged. Breaking such a rule is never a conflict and never
   blocks editing.
3. **Actions that span aggregates are application operations.** An operation reads
   projections, then issues ordinary commands, one per aggregate, in an order in
   which every intermediate state is valid. Aggregates validate only their own part.
   If a later command is rejected, earlier ones are not undone, and the state they
   leave must be harmless (for example an unconnected filter).
4. **Clients use operations, never aggregate commands.** The typed API of
   [ADR-0001](0001-drive-the-engine-through-one-typed-api.md) exposes:
   - **Queries and subscriptions** to projections by path selector.
   - **Property sets:** `set(path, field, value)` for fields whose owner allows it.
     The application layer turns them into the owning aggregate's command.
   - **Intent operations** for structural changes, such as "connect track to
     output".
   - **Batches** of the above, applied together.
5. **Checks in clients are advisory.** Clients may read the same projections to
   prevent invalid edits in the interface. The projection's verdict on the merged
   state is authoritative.
6. **Typed client artefacts are generated** from the C# operation contract under
   [ADR-0007](0007-make-api-serialization-pluggable.md), including a TypeScript
   client and MCP tool schemas.

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
[ADR-0017](0017-key-aggregates-by-hierarchical-paths.md).

### Expose aggregate commands directly to clients

There is no extra layer. Clients must know aggregate boundaries, issue several
commands per action and handle partial failure, and agents face a large set of
fine-grained tools.

## Consequences

### Positive

- Conflict detection stays local to each aggregate's subtree.
- Merges never produce silent corruption: invalid references are visible and
  harmless.
- UI and agent clients get a small, intent-level, generated API.

### Negative and trade-offs

- States that break a rule spanning aggregates can exist after a merge, and users
  must repair them.
- Operations are not atomic. A partly applied operation leaves harmless leftovers
  that may need cleanup.
- The application layer is a new component with its own tests and versioning.

## Evidence

Required before acceptance:

- [ ] List the rules that span aggregates in the draft path tree, and show that each
      can be evaluated by a projection.
- [ ] Walk through "connect a track to an output", "change a track from audio to
      MIDI while it is connected" and "delete a bus with incoming sends".
- [ ] Prototype a generated TypeScript client and MCP tool schemas from a sample
      operation contract.

## Fitness functions

- Architecture test: a decider depends only on its own aggregate's state type.
- Property test: for random concurrent edits, the merged state never fails to
  compile; references that break a rule are inert and flagged.
- Test: an operation whose second command is rejected leaves a state that compiles.
- Architecture test: no client-facing API type exposes an aggregate command or
  event type.

## Review triggers

- Users regularly meet flagged states after merges.
- A rule spanning aggregates must be enforced before an event is accepted, not
  repaired afterwards.
