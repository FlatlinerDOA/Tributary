# 0041. Render and store graph sections with explicit filters

- Status: Proposed
- Recorded: 2026-10-04
- Decision state in code: Planned
- Depends on:
  - [ADR-0004](0004-treat-audio-sources-as-immutable.md): a render pass creates a
    new immutable source; consolidate stays a render-time transform.
  - [ADR-0006](0006-evolve-schemas-additively.md): derived data must not change
    rendered output.
  - [ADR-0009](0009-address-content-with-multihash-sha-256.md): output and input
    hashes.
  - [ADR-0014](0014-store-blobs-as-content-defined-chunks.md): stored outputs are
    chunked blobs.
  - [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md): filters are graph
    vertices; connections live in the `/routing` aggregate.
  - [ADR-0019](0019-authorize-actors-with-path-scoped-capabilities.md): the ability
    each action requires.
  - [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md): deciders
    turn commands into events; tombstones for blob deletion.
  - [ADR-0021](0021-merge-non-conflicting-edits-implicitly.md): merge rules for
    filter settings.
  - [ADR-0024](0024-derive-reactions-and-subscriptions-from-projections.md):
    automatic reactions are never stored as events.
  - [ADR-0031](0031-swap-immutable-graph-snapshots-to-the-realtime-thread.md): graph
    compilation.
  - [ADR-0033](0033-compose-warp-into-one-time-map-per-clip.md): the engine's
    automatic cache; stretches on a new source are not composed with earlier ones.
  - [ADR-0035](0035-schedule-ml-inference-as-two-tier-graph-nodes.md): GPU inference
    output cannot be reproduced.
  - [ADR-0036](0036-guarantee-longevity-through-degradation-tiers.md): print
    insurance for non-stock plugins.
  - [ADR-0040](0040-integrate-aggregates-through-projections-and-application-operations.md):
    rules spanning aggregates are evaluated by projections; actions spanning
    aggregates are application operations.
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

- [ADR-0024](0024-derive-reactions-and-subscriptions-from-projections.md) forbids
  storing automatic reactions as events. Stored events come only from actors'
  commands.
- [ADR-0035](0035-schedule-ml-inference-as-two-tier-graph-nodes.md): GPU inference
  is often non-deterministic, so its stored output is the only way to reproduce a
  result.
- [ADR-0036](0036-guarantee-longevity-through-degradation-tiers.md) prints non-stock
  plugin output for longevity, and
  [ADR-0034](0034-host-clap-plugins-first-out-of-process.md) falls back to a cached
  print when a plugin crashes.
- The draft path tree in
  [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md) has no place for sinks.

Unresolved questions:

- Where sinks live in the path tree.
- The default for auto-update.
- The grace period and the tombstone event, which depend on the redaction design
  that [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md) lists as
  unresolved.

## Decision

1. **Render is an explicit filter.** It runs the render pass over its input and
   produces finished audio with a declared channel layout, sample rate and dither.
   Its output is not persisted. The engine may cache it like any other stage.
2. **Store is a separate explicit filter.** It takes rendered audio and keeps it as
   a new immutable, content-addressed source that syncs with the project. The
   upstream graph is kept, and the store filter can be bypassed to hear the live
   graph instead.
3. **Edges are typed as audio or event, and every audio sink needs a render
   filter.** Every audio input to an audio sink (device, export, network audio
   stream, store) must pass through a render filter. Event sinks have no such
   requirement. This rule spans aggregates, so a projection evaluates it under
   [ADR-0040](0040-integrate-aggregates-through-projections-and-application-operations.md).
   An audio sink fed without a render filter is silent and flagged.
4. **The connect operation adds missing render filters.** When an application
   operation connects audio to an audio sink with no render filter, it first
   creates one (with a newly generated ID) and then connects through it, as
   separate commands. When there is more than one valid way to satisfy a type
   rule, the operation is rejected instead. Deleting a render filter that a sink
   depends on is allowed; the sink becomes silent and flagged, and clients warn
   before the deletion.
5. **Graph compilation never adds or removes nodes.** It only rewrites arithmetic
   into forms that produce identical output, following
   [ADR-0006](0006-evolve-schemas-additively.md).
6. **Every stored output records its recipe:** the input hashes, the graph section,
   and the algorithm identifiers and versions used.
7. **Staleness is detected from the recorded input hash.** A store filter whose
   current input hash differs from the recorded one is stale. It keeps playing its
   existing output and shows a stale indicator with a re-render button.
8. **Auto-update is a store filter property tied to the actor who enabled it.**
   When it is enabled, that actor's device re-renders and records the new output as
   that actor's command once edits upstream have settled. Other peers never
   re-render on their own, so auto-update produces no unattributed reactions.
9. **Preserve history is a store filter property, disabled by default.**
   - Disabled: a superseded output is tombstoned after it has been replaced on every
     live head and a grace period has passed. The most recent superseded output is
     kept for undo, and older history is rebuilt from the recipe on demand.
   - Enabled: superseded outputs are kept. Enabling it shows a warning with the
     filter's current history size and the cost of each further render.
   - Outputs whose recipe includes a node that is not deterministic, such as tier 2
     inference, are always kept, whatever this setting says.
10. **Settings merge as follows:** preserve history is true if any concurrent edit
    set it, and other settings follow the register rule of
    [ADR-0021](0021-merge-non-conflicting-edits-implicitly.md).
11. **Every action uses the `write` ability** on the filter's path: adding filters,
    changing their settings, committing a re-render and tombstoning superseded
    outputs. No new ability is introduced.
12. **Prints share the stored form.** Prints required by
    [ADR-0035](0035-schedule-ml-inference-as-two-tier-graph-nodes.md) and
    [ADR-0036](0036-guarantee-longevity-through-degradation-tiers.md) are stored as
    store-filter outputs with a recipe.
13. **A stretch applied after a store filter acts on the stored source.** This is
    the only way to stack independent stretches
    ([ADR-0033](0033-compose-warp-into-one-time-map-per-clip.md)).

## Alternatives considered

### Insert render stages when compiling the graph

Users never have to add them. The compiled graph would contain nodes that the user
cannot see or configure, and compilation would do more than rewrite arithmetic.

### One filter that both renders and stores

There is one concept. Every device output would either persist audio it does not
need or carry a setting that turns persistence off, which mixes two contracts.

### A manual freeze command

It is familiar. It adds a concept next to the automatic cache, and its results are
neither part of the graph nor synced.

### Re-render automatically on every peer

Playback is never stale on any peer. Concurrent renders duplicate work and storage,
and the stored events would be reactions with no actor behind them.

### Keep every superseded output

History is exact. Storage grows with every render.

## Consequences

### Positive

- Every audio sink has a visible, configurable render filter.
- A node editor can show and edit the persisted graph directly, because the graph
  that plays contains exactly the same nodes.
- Storing is opt-in, attributed and synced. Caching stays automatic and invisible.
- One stored form serves user stores, inference prints and print insurance.
- Storage growth is bounded unless the user opts into preserving history.

### Negative and trade-offs

- Users see render filters on every audio output, which adds to the graph.
- Rebuilt history may differ from the original output if the algorithm is not
  bit-exact (an open item in
  [ADR-0033](0033-compose-warp-into-one-time-map-per-clip.md)).
- A peer who cannot run the graph cannot rebuild a tombstoned output.
- Auto-update stays stale while the enabling actor's device is offline.
- After a merge or a deletion, an audio sink can be left without a render filter.
  It is silent until the user repairs it.

## Evidence

Required before acceptance:

- [ ] Place sinks, render filters and store filters in the
      [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md) path tree, and walk
      through "connect a track to an output" and "store a bus".
- [ ] Measure storage per render and growth per hour with auto-update and preserve
      history enabled, for realistic stems.
- [ ] Specify the tombstone event with the redaction design in
      [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md).
- [ ] Decide the auto-update default.
- [ ] Define how a node declares that it is not deterministic.

## Fitness functions

- Test: graph compilation never changes the set of nodes.
- Test: the connect operation on an audio sink with no render filter issues a
  create-filter command and then a connect command.
- Property test: in any merged state, an audio sink fed without a render filter is
  silent and flagged, and the graph still compiles.
- Test: a stale store filter keeps playing its existing output until it is
  re-rendered.
- Test: with preserve history disabled, a superseded output is tombstoned only after
  every live head has replaced it and the grace period has passed.
- Test: an output whose recipe includes a non-deterministic node is never
  tombstoned.
- Test: concurrent preserve-history edits merge to true if either is true.
- Test: automatic cache entries are never included in sync.

## Review triggers

- Render filters on every output prove confusing for users.
- Render storage cost becomes a common user complaint.
- Rebuilt history is not acceptably close to the original.
