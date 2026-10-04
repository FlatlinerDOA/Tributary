# 0504. Model render and store as explicit graph filters

- Layer: L4 Domain model
- Status: Proposed
- Recorded: 2026-10-04
- Decision state in code: Planned
- Depends on:
  - [ADR-0107](0107-address-content-with-multihash-sha-256.md): output and input
    hashes.
  - [ADR-0203](0203-store-blobs-as-content-defined-chunks.md): stored outputs are
    chunked blobs.
  - [ADR-0302](0302-authorize-actors-with-path-scoped-capabilities.md): the ability
    each action requires.
  - [ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md): commands
    become events; tombstones for blob deletion.
  - [ADR-0304](0304-merge-non-conflicting-edits-implicitly.md): merge rules for
    filter settings.
  - [ADR-0307](0307-derive-reactions-and-subscriptions-from-projections.md):
    automatic reactions are never stored as events.
  - [ADR-0309](0309-evaluate-rules-that-span-aggregates-in-projections.md): rules
    spanning aggregates are evaluated by projections.
  - [ADR-0500](0500-lay-out-the-project-as-a-path-tree.md): filters are graph
    vertices; connections live in the routing aggregate.
  - [ADR-0501](0501-resolve-audio-edits-as-render-time-transforms.md): a render pass
    creates a new immutable source.
  - [ADR-0503](0503-compose-warp-into-one-time-map-per-clip.md): stretches on a new
    source are not composed with earlier ones.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Audio leaves the graph through sinks: audio devices, disk exports, network audio
streams and the project's own store. Each needs finished audio. Event sinks (MIDI,
OSC) need no audio and no render pass.

A graph section can also be expensive to run, hard to understand, or produced by
plugins and models that collaborators lack. Users need to turn it into concrete
audio that is kept with the project and synced.

These are two different contracts. Producing finished audio is needed at every
audio sink and is never persisted. Keeping that audio as part of the project is
persisted, synced and costs storage.

Facts:

- [ADR-0307](0307-derive-reactions-and-subscriptions-from-projections.md) forbids
  storing automatic reactions as events. Stored events come only from actors'
  commands.
- Some nodes, such as GPU inference, are not deterministic, so their stored output
  is the only way to reproduce a result.

Unresolved questions:

- The default for auto-update.
- The grace period and the tombstone event, which depend on the redaction design
  that [ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md) lists as
  unresolved.
- How a node declares that it is not deterministic.

## Decision

1. **Render is an explicit filter.** It runs the render pass over its input and
   produces finished audio with a declared channel layout, sample rate and dither.
   Its output is not persisted.
2. **Store is a separate explicit filter.** It takes rendered audio and keeps it as
   a new immutable, content-addressed source that syncs with the project. The
   upstream graph is kept, and the store filter can be bypassed to hear the live
   graph instead.
3. **Edges are typed as audio or event, and every audio sink needs a render
   filter.** Every audio input to an audio sink (device, export, network audio
   stream, store) must pass through a render filter. Event sinks have no such
   requirement. This rule spans aggregates, so a projection evaluates it under
   [ADR-0309](0309-evaluate-rules-that-span-aggregates-in-projections.md). An audio
   sink fed without a render filter is silent and flagged.
4. **Connecting audio to a sink adds a missing render filter.** The filter is
   created first, with a newly generated ID, and then the connection is made
   through it, as separate commands. When there is more than one valid way to
   satisfy a type rule, the connection is rejected instead. Deleting a render filter
   that a sink depends on is allowed; the sink becomes silent and flagged, and
   clients warn before the deletion.
5. **Every stored output records its recipe:** the input hashes, the graph section,
   and the algorithm identifiers and versions used.
6. **Staleness is detected from the recorded input hash.** A store filter whose
   current input hash differs from the recorded one is stale. It keeps playing its
   existing output and shows a stale indicator with a re-render button.
7. **Auto-update is a store filter property tied to the actor who enabled it.**
   When it is enabled, that actor's device re-renders and records the new output as
   that actor's command once edits upstream have settled. Other peers never
   re-render on their own, so auto-update produces no unattributed reactions.
8. **Preserve history is a store filter property, disabled by default.**
   - Disabled: a superseded output is tombstoned after it has been replaced on every
     live head and a grace period has passed. The most recent superseded output is
     kept for undo, and older history is rebuilt from the recipe on demand.
   - Enabled: superseded outputs are kept. Enabling it shows a warning with the
     filter's current history size and the cost of each further render.
   - Outputs whose recipe includes a node that is not deterministic are always
     kept, whatever this setting says.
9. **Settings merge as follows:** preserve history is true if any concurrent edit
   set it, and other settings follow the register rule of
   [ADR-0304](0304-merge-non-conflicting-edits-implicitly.md).
10. **Every action uses the `write` ability** on the filter's path: adding filters,
    changing their settings, committing a re-render and tombstoning superseded
    outputs. No new ability is introduced.
11. **A stretch applied after a store filter acts on the stored source.** This is
    the only way to stack independent stretches
    ([ADR-0503](0503-compose-warp-into-one-time-map-per-clip.md)).

## Alternatives considered

### Insert render stages when compiling the graph

Users never have to add them. The compiled graph would contain nodes that the user
cannot see or configure.

### One filter that both renders and stores

There is one concept. Every device output would either persist audio it does not
need or carry a setting that turns persistence off, which mixes two contracts.

### A manual freeze command

It is familiar. It adds a concept next to automatic caching, and its results are
neither part of the graph nor synced.

### Re-render automatically on every peer

Playback is never stale on any peer. Concurrent renders duplicate work and storage,
and the stored events would be reactions with no actor behind them.

### Keep every superseded output

History is exact. Storage grows with every render.

## Consequences

### Positive

- Every audio sink has a visible, configurable render filter.
- A node editor can show and edit the persisted graph directly.
- Storing is opt-in, attributed and synced.
- Storage growth is bounded unless the user opts into preserving history.

### Negative and trade-offs

- Users see render filters on every audio output, which adds to the graph.
- Rebuilt history may differ from the original output if the algorithms involved
  are not bit-exact.
- A peer who cannot run the graph cannot rebuild a tombstoned output.
- Auto-update stays stale while the enabling actor's device is offline.
- After a merge or a deletion, an audio sink can be left without a render filter.
  It is silent until the user repairs it.

## Evidence

Required before acceptance:

- [ ] Place render filters and store filters in the
      [ADR-0500](0500-lay-out-the-project-as-a-path-tree.md) path tree, and walk
      through "connect a track to an output" and "store a bus".
- [ ] Measure storage per render and growth per hour with auto-update and preserve
      history enabled, for realistic stems.
- [ ] Specify the tombstone event with the redaction design in
      [ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md).
- [ ] Decide the auto-update default.
- [ ] Define how a node declares that it is not deterministic.

## Fitness functions

- Test: connecting audio to an audio sink with no render filter issues a
  create-filter command and then a connect command.
- Property test: in any merged state, an audio sink fed without a render filter is
  silent and flagged.
- Test: a stale store filter keeps playing its existing output until it is
  re-rendered.
- Test: with preserve history disabled, a superseded output is tombstoned only after
  every live head has replaced it and the grace period has passed.
- Test: an output whose recipe includes a non-deterministic node is never
  tombstoned.
- Test: concurrent preserve-history edits merge to true if either is true.

## Review triggers

- Render filters on every output prove confusing for users.
- Render storage cost becomes a common user complaint.
- Rebuilt history is not acceptably close to the original.

## Notes

- 2026-10-04: Renumbered from the former ADR-0041 when ADRs were grouped by layer,
  and narrowed to the domain model. The rule that compilation adds no nodes moved
  to the graph snapshot record, the cache rule to the stretch and cache record, and
  the print rules to the inference and longevity records.
