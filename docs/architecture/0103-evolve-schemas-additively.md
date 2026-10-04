# 0103. Evolve schemas additively

- Layer: L0 Primitives
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0102](0102-encode-persisted-data-as-deterministic-cbor.md): the encoding
    whose integer keys are evolved.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Persisted data falls into two kinds:

- **The system of record:** events. They are signed, can never be rewritten, and
  a project from 2026 must still be readable from them in 2046.
- **Derived data:** projections, their checkpoints, indexes and caches. These can
  always be rebuilt from the events, so their shape is free to change.

Schemas will change many times over the life of a project. Only the system of
record has to stay readable forever.

## Decision

These rules apply to events, the system of record:

- Put a schema version in every event envelope.
- Upcast older events in memory when they are read, and never write them back.
- Change schemas only by adding new integer keys. Never reuse or repurpose a key.
- Keep unknown keys when forwarding or projecting events.
- Anything persisted inside an event, including structured identifiers such as
  paths, is part of the schema and evolves by the same rules.

Derived data is exempt. Projection schemas, checkpoints, indexes and caches may
change at any time without upcasters. Persisted derived data carries a version
stamp and is discarded and rebuilt from the events when the stamp no longer
matches.

The one constraint on derived data: **a change to a projection must not change
rendered output.** Rendering the same events before and after the change must give
identical audio. A change that would alter what a project sounds like is a change
to the meaning of history, so it must be expressed through events (for example a
new event type, or an explicit algorithm-version field the user opts into), never
by editing a projection.

## Alternatives considered

### Migrate stored data in place

Readers stay simple. Signatures and hashes break.

### Snapshot at version boundaries and discard older records

Reading is cheaper, but history and attribution before the snapshot are lost.

### Weak schemas (store the CBOR map and read it leniently)

There is no upcaster code. Every consumer then has to handle every historical
shape, so the burden moves rather than disappears.

### Apply the same rules to derived data

Every persisted structure would be stable. Projections could never be redesigned
without upcasters, even though they can always be rebuilt.

## Consequences

### Positive

- Old projects open without being converted.
- A peer running an older version can relay events containing fields it does not
  understand.
- Projections can be redesigned freely, with no migration code.

### Negative and trade-offs

- Upcasters for events build up forever and need permanent test coverage.
- A registry of key allocations is needed so that no key is ever reused.
- Changing a projection costs a rebuild when a project is next opened.
- Rendering behaviour is pinned to history. Fixing something that changes the
  sound of existing projects needs a deliberate, event-level change.

### Possible future mitigation

If upcaster chains become a burden, a user-selectable **permanent migration** could
rewrite a project's events into the current schema. It is not part of this
decision, and it has costs that would need their own ADR:

- Rewritten events have new hashes, so the original signatures and parent links
  no longer apply. The migrating actor would re-sign the new history and record a
  signed mapping from old event hashes to new ones, with the original events kept
  as an archive.
- Peers that have not migrated hold a different DAG, so a migration would act like
  a fork that every collaborator must adopt together.
- Upcasters would still be needed for projects that are never migrated.

## Evidence

Required before acceptance:

- [ ] Draft a key-registry format (one file per event type, checked by CI).
- [ ] Prototype an upcaster chain for one event type across three versions.
- [ ] Assemble a corpus of reference projects for golden render tests.

## Fitness functions

- Each event schema version has fixture events. CI reads the whole fixture corpus
  through the current upcasters.
- CI fails when a registered event key is removed or its type changes.
- Golden render test: every reference project renders to identical bytes before
  and after any change to projection code.
- Test: derived data with a stale version stamp is discarded and rebuilt, never
  read.

## Review triggers

- The upcaster chain makes reading too slow. Consider the permanent migration
  option above.
- Rebuilding projections after a change makes opening projects too slow.

## Notes

- 2026-10-04: Renumbered from the former ADR-0006 when ADRs were grouped by layer.
