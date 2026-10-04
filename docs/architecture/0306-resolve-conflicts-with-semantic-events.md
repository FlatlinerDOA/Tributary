# 0306. Resolve conflicts with semantic events

- Layer: L2 Event model
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0302](0302-authorize-actors-with-path-scoped-capabilities.md): the
    `resolve` ability and coverage rule.
  - [ADR-0305](0305-detect-conflicts-deterministically.md): the conflicts being
    resolved.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Conflicts must be resolved by a person or an agent. A generic merge node
("merge a1f3…") says nothing about what was decided. A resolution may need to span
many aggregates atomically ("take all of Alice's changes"). Replaying a resolution
must not depend on detection logic that may change over 20 years.

## Decision

Resolve conflicts with typed domain events:

- **Parents** are the conflicting heads.
- **Path** is the lowest common ancestor of the conflicting paths, or higher for a
  broader scope.
- **Payload** has two parts:
  - `intent`: the scope or policy, for history and display (for example
    `FavourAll { parent }`, `FavourByScope { drums: alice, vocals: bob }`).
  - `decisions`: the explicit choice for each conflict (`ChoseVersion`,
    `KeptAsAlternatives`, `Excluded { events }`, or an aggregate-specific merge such
    as `TransposedOnMerge`). Only `decisions` is used on replay.
- **Ability:** the author needs the `resolve` ability covering the resolution's
  path subtree. It is granted separately from `write`.

The resulting state must satisfy every invariant it touches. Concurrent
resolutions of the same conflict are themselves a conflict, resolved the same way.

## Alternatives considered

### Generic merge nodes choosing a whole branch

They are uniform. Choosing a whole branch discards the other side's unrelated work,
and history does not show what was decided.

### Record only the intent

It is compact. Replay would need to run conflict detection again, which may change
over time.

## Consequences

### Positive

- History reads as decisions that are attributable, reviewable and can be undone by
  later resolutions.
- Agents can be given narrow merge roles through `resolve`.
- Effects that span aggregates (such as re-transposing after a key change) become
  explicit decisions by whoever merges.

### Negative and trade-offs

- The catalogue of resolution events grows with each aggregate type.
- Root-level resolutions conflict with anything concurrent.

## Evidence

Required before acceptance:

- [ ] Draft the minimum catalogue (`ChoseVersion`, `Excluded`,
      `KeptAsAlternatives`) in CDDL.
- [ ] Prototype "take all of Alice's changes" over a three-aggregate conflict.

## Fitness functions

- Replay test: rebuilding state from resolutions never invokes conflict detection.
- Test: a resolution whose result breaks an invariant is rejected.
- Test: a resolution at `/` is rejected for an actor whose `resolve` covers only
  `/tracks/**`.

## Review triggers

- A common resolution cannot be expressed with the catalogue.

## Notes

- 2026-10-04: Renumbered from the former ADR-0023 when ADRs were grouped by layer.
