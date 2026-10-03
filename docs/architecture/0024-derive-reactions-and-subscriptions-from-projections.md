# 0024. Derive reactions and subscriptions from projections

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0018](0018-select-paths-with-one-pattern-language.md): subscription
    selectors.
  - [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md): projections
    are derived from the DAG.
  - [ADR-0023](0023-resolve-conflicts-with-semantic-events.md): resolutions are the
    actor decisions that replace sagas for effects spanning aggregates.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Some state follows automatically from other state: section lengths from clip
positions, delay compensation from the routing graph. In classic CQRS these are
sagas that emit new events. Without a central server, either every peer emits the
same reaction (duplicates), or one peer must be elected to do it.

Clients (UI and agents) need to know when state they care about changes. Raw
event subscriptions miss changes recorded at ancestor paths, and clients need
changes in state rather than raw events anyway.

## Decision

- **Reactions are projections.** Anything that follows automatically from other
  state is computed by a projection and never stored as an event. Stored events
  come only from actors' commands, including resolutions.
- **Projections publish changes by path.** After applying an event, a projection
  publishes the set of paths whose state actually changed.
- **Clients subscribe to those changes** using path-pattern selectors.
- **Checkpoints are kept per path node and keyed by event hash,** so a change
  invalidates only the checkpoints on its path to the root, and the state at any
  set of heads (including common ancestors) can be rebuilt.

## Alternatives considered

### Sagas emitting events

This is familiar from CQRS. It causes duplicate events or needs an elected runner,
and the reactions become permanent history.

### Clients subscribe to raw events

It is simple. Clients must re-implement projection logic, and they miss changes
recorded at ancestor paths.

## Consequences

### Positive

- No duplicate reactions, and no coordination.
- Clients receive state changes that are already computed.

### Negative and trade-offs

- Projections must compute changed paths precisely, or clients over-refresh.
- Reaction logic can change between versions without affecting history, but
  derived state may then differ between versions.

## Evidence

Required before acceptance:

- [ ] Prototype change publication for a root-level resolution touching three
      tracks.
- [ ] Measure checkpoint storage per path node for a 200-track project.

## Fitness functions

- Architecture test: projection code cannot append events.
- Test: a subscriber to `/tracks/3/**` is notified of a root-level resolution that
  changes track 3, and not of one that changes only track 4.

## Review triggers

- A reaction needs to be attributed or reviewed, which would make it an actor
  decision rather than a projection.
