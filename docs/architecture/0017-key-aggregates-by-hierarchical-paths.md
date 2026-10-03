# 0017. Key aggregates by hierarchical paths

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0006](0006-evolve-schemas-additively.md): paths evolve under the schema
    rules, with upcasters.
  - [ADR-0016](0016-identify-entities-with-canonical-string-ids.md): path segments.
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

Routing and modulation are graphs. Sends and sidechains cut across any ownership
tree.

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
   between entities live in dedicated aggregates (`/routing`, `/modulation`) that
   own the invariants governing them. A reference to an entity that has been
   deleted becomes an inert tombstone: it is ignored, not invalid.
5. **Stable identity.** Paths are built only from stable IDs and fixed names, never
   from how things are arranged in the UI. Folders and groups are properties, not
   path segments.
6. **Declared invariants.** Each aggregate declares the invariants that must hold
   over its subtree.
7. **Persisted form.** A path is persisted as a CBOR array of segment strings. The
   `/a/b` form is only for display, logs and resource URIs.

Initial tree (first draft, to be validated by evidence):

```
/                                   project root
├── tempo                           monotonic tempo map
├── key                             key map
├── arrangement/sections/{id}
├── tracks/{trackId}                vertex; owns its serial chain
│   ├── clips/{clipId}              notes and automation
│   └── chain/{pluginId}
├── buses/{busId}                   vertex
├── routing                         invariant: acyclic audio/MIDI graph
│   └── edges/{edgeId}
│       └── params                  send level, pre/post
└── modulation                      invariant: feedback only through declared one-block delays
    └── edges/{edgeId}
```

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

### Sends stored under their source track

This matches how mixer UIs show sends. Cycle checking then needs an invariant over
every track at the root, and deleting a track has to reach into other tracks.

## Consequences

### Positive

- Changes in sibling subtrees are independent by construction.
- Atomic changes at any scope keep the one-change, one-aggregate rule.
- Selection, authorisation and replication can all be scoped by subtree.

### Negative and trade-offs

- Paths are part of the persisted schema. Restructuring the tree needs path
  upcasters, so the top levels must be right early.
- Validating a command at a high node needs the state of a large subtree.
- Moving an entity to another parent (for example a clip to another track) changes
  its path. It must be modelled as a domain move at the common ancestor that
  records where the entity came from.

## Evidence

Required before acceptance:

- [ ] Walk through "create track with send", "delete bus with incoming sends" and
      "move clip between tracks" under the draft tree.
- [ ] Decide the modulation feedback rules with input from DSP and engine design.
- [ ] Prototype a path upcaster that moves one subtree.

## Fitness functions

- Schema lint: every change type declares the path pattern it may be recorded at.
- Architecture test: path segments are built only from typed IDs or fixed names,
  never from display names.
- Property test: an inert tombstoned reference never makes state computation fail.

## Review triggers

- A new feature needs an entity with two owners.
