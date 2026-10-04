# 0800. Guarantee longevity through degradation tiers

- Layer: L7 Product features
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0203](0203-store-blobs-as-content-defined-chunks.md): prints and plugin
    state are stored as chunks.
  - [ADR-0402](0402-share-projects-as-self-describing-trib-bundles.md): the archive
    format tiers 1 and 2 live in.
  - [ADR-0502](0502-decompose-imported-audio-into-samples-metadata-and-recipe.md):
    export can rebuild original source files.
  - [ADR-0504](0504-model-render-and-store-as-explicit-graph-filters.md): the stored
    form of prints.
  - [ADR-0603](0603-host-clap-plugins-first-out-of-process.md): the plugin host whose
    output is printed.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Projects should stay playable and editable for 20 years. Plugins, models and
operating systems disappear over that span. A project must always open, and losing
a plugin should cost only the ability to edit that effect, never the song.

Cost of print insurance: one stereo stream at 48 kHz in 32-bit float is
384 KB/s, about 1.4 GB per hour, for each printed plugin. Printing 50 plugins
continuously is far too much. Policy questions:

- Print at what bit depth and codec?
- Print every plugin, only the end of each chain, or the last non-stock (third-party)
  node of each chain?
- Print after each edit settles, or only while idle?

Archiving plugin binaries has licensing problems. Most EULAs forbid redistributing
them, and storing them may be restricted too. Model weights have the same issue.

## Decision

Support three tiers:

1. **Fully editable:** plugins and models are pinned by hash, and binaries are kept
   where the licence allows.
2. **Editable with frozen effects:** every non-stock plugin's output is printed and
   stored as a store-filter output with a recipe
   ([ADR-0504](0504-model-render-and-store-as-explicit-graph-filters.md)), and its
   state is stored as an opaque blob plus a snapshot of parameter
   values and parameter metadata.
3. **Opens in any DAW:** a canonical export with BWF/FLAC stems, MIDI, the tempo
   map and DAWproject/AAF.

When opening a project, fall back to the highest tier still available for each
plugin.

## Alternatives considered

### Freeze only when the user asks

It costs nothing by default, but users discover the missing plugin only when it is
too late.

### Export only (tier 3)

It is simple. Editability is lost entirely.

## Consequences

### Positive

- A song is never lost to a dead plugin.

### Negative and trade-offs

- Printing uses a lot of storage and background CPU.
- Legal uncertainty about archiving binaries.

## Evidence

Available:

- [DAWproject](https://github.com/bitwig/dawproject).

Required before acceptance:

- [ ] Model storage per project-hour under each print policy.
- [ ] Legal review of storing plugin binaries and model weights.
- [ ] Spike: delete a plugin binary, then open and play the project.

## Fitness functions

- Test with a test plugin: delete its binary; the project still opens and plays.
- Export round trip: tier-3 export imports into at least one other DAW.

## Review triggers

- The cost of printing makes it unusable on target devices.

## Notes

- 2026-10-04: Renumbered from the former ADR-0036 when ADRs were grouped by layer.
