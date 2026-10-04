# 0702. Expose application operations to clients

- Layer: L6 Host and API
- Status: Proposed
- Recorded: 2026-10-04
- Decision state in code: Planned
- Depends on:
  - [ADR-0300](0300-key-aggregates-by-hierarchical-paths.md): one change targets
    one aggregate.
  - [ADR-0301](0301-select-paths-with-one-pattern-language.md): subscription
    selectors.
  - [ADR-0307](0307-derive-reactions-and-subscriptions-from-projections.md):
    clients subscribe to projections by path.
  - [ADR-0309](0309-evaluate-rules-that-span-aggregates-in-projections.md):
    rules that span aggregates are evaluated by projections.
  - [ADR-0310](0310-hold-uncommitted-edits-in-local-draft-overlays.md): draft
    overlays.
  - [ADR-0700](0700-drive-the-engine-through-one-typed-api.md): every client uses
    one typed API.
  - [ADR-0701](0701-make-api-serialization-pluggable.md): client artefacts are
    generated from the C# contract.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

One user action can touch several aggregates, for example creating a filter and
connecting it. Aggregates only validate their own subtree, so something has to
coordinate the commands.

Clients also need a usable surface. A React UI wants state to bind to and simple
value changes. Agents over MCP work best with a small set of intent-level tools.
Neither should need to know aggregate boundaries or event shapes.

## Decision

1. **Actions that span aggregates are application operations.** An operation reads
   projections, then issues ordinary commands, one per aggregate, in an order in
   which every intermediate state is valid. Aggregates validate only their own part.
   If a later command is rejected, earlier ones are not undone, and the state they
   leave must be harmless (for example an unconnected filter).
2. **Clients use operations, never aggregate commands.** The typed API of
   [ADR-0700](0700-drive-the-engine-through-one-typed-api.md) exposes:
   - **Queries and subscriptions** to projections by path selector.
   - **Property sets:** `set(path, field, value)` for fields whose owner allows it.
     The application layer turns them into the owning aggregate's command.
   - **Intent operations** for structural changes, such as "connect track to
     output".
   - **Batches** of the above.
3. **Batches are all-or-nothing.** A batch is applied to a draft overlay
   ([ADR-0310](0310-hold-uncommitted-edits-in-local-draft-overlays.md)) and
   committed only if every operation in it succeeds.
4. **Checks in clients are advisory.** Clients may read the same projections to
   prevent invalid edits in the interface. The projection's verdict on the merged
   state is authoritative.
5. **Typed client artefacts are generated** from the C# operation contract under
   [ADR-0701](0701-make-api-serialization-pluggable.md), including a TypeScript
   client and MCP tool schemas.

## Alternatives considered

### Expose aggregate commands directly to clients

There is no extra layer. Clients must know aggregate boundaries, issue several
commands per action and handle partial failure, and agents face a large set of
fine-grained tools.

## Consequences

### Positive

- UI and agent clients get a small, intent-level, generated API.
- Clients that need atomicity use batches.

### Negative and trade-offs

- A single operation is not atomic. A partly applied operation leaves harmless
  leftovers that may need cleanup.
- The application layer is a component with its own tests and versioning.

## Evidence

Required before acceptance:

- [ ] Walk through "connect a track to an output", "change a track from audio to
      MIDI while it is connected" and "delete a bus with incoming sends".
- [ ] Prototype a generated TypeScript client and MCP tool schemas from a sample
      operation contract.

## Fitness functions

- Test: an operation whose second command is rejected leaves a state that
  compiles.
- Test: a batch with one failing operation commits nothing.
- Architecture test: no client-facing API type exposes an aggregate command or
  event type.

## Review triggers

- Agents or UIs regularly need atomic operations outside batches.

## Notes

- 2026-10-04: Split from the former ADR-0040 when ADRs were grouped by layer.
  Batches moved here from the former draft overlay record.
