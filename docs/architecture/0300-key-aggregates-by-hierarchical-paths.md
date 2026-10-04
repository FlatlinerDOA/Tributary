# 0300. Key aggregates by hierarchical paths

- Layer: L2 Event model
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0103](0103-evolve-schemas-additively.md): paths evolve under the schema
    rules, with upcasters.
  - [ADR-0104](0104-identify-entities-with-canonical-string-ids.md): path segments.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Domain state needs aggregate boundaries for three purposes: the scope over which
invariants are checked, deciding when two concurrent changes collide, and the unit
of atomic change. Some operations naturally span several entities: "take all of
one collaborator's changes", or a change scoped to the drum tracks. Letting one
change span sibling aggregates breaks the rule that each change belongs to one
aggregate.

Relationships between entities, such as the edges of a graph, cut across any
ownership tree.

## Decision

Key every aggregate by a path: an array of identifier segments.

1. **One change, one path.** Every persisted change targets exactly one path.
2. **Subtree state.** The state of the aggregate at a path is the fold of the
   changes at that path and at every descendant path. A command handled at a node
   may read the state of that node's whole subtree.
3. **Lowest common ancestor.** An atomic change spanning several entities is
   recorded at the lowest common ancestor of their paths. Do this only when an
   intermediate state would break an invariant; otherwise use separate changes.
4. **Entities in the tree, relationships in their own aggregates.** Connections
   between entities live in dedicated aggregates that own the invariants governing
   them. A reference to an entity that has been deleted becomes an inert tombstone:
   it is ignored, not invalid.
5. **Stable identity.** Paths are built only from stable IDs and fixed names, never
   from how things are arranged in the UI. Folders and groups are properties, not
   path segments.
6. **Declared invariants.** Each aggregate declares the invariants that must hold
   over its subtree.
7. **Persisted form.** A path is persisted as a CBOR array of segment strings. The
   `/a/b` form is only for display, logs and resource URIs.

This record defines the path model only. Each application defines its own tree.

## Alternatives considered

### Flat aggregates per track, bus, section and plugin

There are few concepts. Operations spanning entities need either changes spanning
several aggregates or sagas, and both break the boundary rule or need someone to
coordinate them.

### One aggregate for the whole project

Invariant checks are trivial. Any two concurrent structural edits collide.

### One aggregate per clip or note

There is little contention, but invariants that span a track or the routing graph
have nowhere to live.

### No aggregates: dynamic consistency boundaries

In Dynamic Consistency Boundaries (DCB), each decision queries exactly the events
it depends on, and its events are appended only if nothing matching that query has
changed. Every event records its read selector, and concurrent events conflict when
one writes what the other read. Checks that span entities (such as an edge's
endpoint types) become ordinary decisions, and conflicts are more precise than with
path overlap.

It was rejected for three reasons:

- Every command handler becomes its own consistency rule with its own read set.
  Peers on different versions then disagree more easily about whether an event is
  valid, and the surface that must stay compatible for decades grows.
- Decision time has no bound. A decision folds whatever its query matches, and
  bounding it means deciding only from declared, checkpointed state models, which
  are aggregates under another name.
- Conflict detection grows with DAG width times the number of read selectors, and
  every read selector becomes permanent schema.

## Consequences

### Positive

- Changes in sibling subtrees are independent by construction.
- Atomic changes at any scope keep the one-change, one-aggregate rule.
- Selection, authorisation and replication can all be scoped by subtree.

### Negative and trade-offs

- Paths are part of the persisted schema. Restructuring the tree needs path
  upcasters, so the top levels must be right early.
- Validating a command at a high node needs the state of a large subtree.
- Moving an entity to another parent changes its path. It must be modelled as a
  domain move at the common ancestor that records where the entity came from.

## Evidence

Required before acceptance:

- [ ] Prototype a path upcaster that moves one subtree.

## Fitness functions

- Schema lint: every change type declares the path pattern it may be recorded at.
- Architecture test: path segments are built only from typed IDs or fixed names,
  never from display names.
- Property test: an inert tombstoned reference never makes state computation fail.

## Review triggers

- A new feature needs an entity with two owners.

## Notes

- 2026-10-04: Split from the former ADR-0017 when ADRs were grouped by layer. The
  DAW path tree moved to the domain layer, so this record stays free of domain
  types as the data layer library requires.
