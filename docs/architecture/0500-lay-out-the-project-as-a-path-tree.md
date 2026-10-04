# 0500. Lay out the project as a path tree

- Layer: L4 Domain model
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0300](0300-key-aggregates-by-hierarchical-paths.md): the path model,
    aggregates and declared invariants.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

The path model in [ADR-0300](0300-key-aggregates-by-hierarchical-paths.md) leaves
each application to define its tree. A DAW project has tracks, buses, clips,
plugin chains, a tempo map, a key map and an arrangement. Routing and modulation
are graphs: sends and sidechains cut across any ownership tree.

Paths are persisted, so the top levels must be right early.

Unresolved questions:

- Where audio sinks (devices, exports, network streams) live in the tree.

## Decision

Use this initial tree (first draft, to be validated by evidence):

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

### Sends stored under their source track

This matches how mixer UIs show sends. Cycle checking then needs an invariant over
every track at the root, and deleting a track has to reach into other tracks.

## Consequences

### Positive

- Routing and modulation invariants each have one owning aggregate.

### Negative and trade-offs

- Moving a clip to another track changes its path and must be modelled as a move at
  the common ancestor.

## Evidence

Required before acceptance:

- [ ] Walk through "create track with send", "delete bus with incoming sends" and
      "move clip between tracks" under the draft tree.
- [ ] Decide the modulation feedback rules with input from DSP and engine design.
- [ ] Place audio sinks in the tree.

## Fitness functions

- Schema lint: every domain change type declares a path pattern within this tree.

## Review triggers

- A new feature needs an entity with two owners.

## Notes

- 2026-10-04: Split from the former ADR-0017 when ADRs were grouped by layer.
