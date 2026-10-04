# 0603. Host CLAP plugins first, out of process

- Layer: L5 Engine
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0100](0100-keep-the-core-managed-with-optional-native-providers.md): plugin
    ABIs are a permitted native boundary.
  - [ADR-0601](0601-swap-immutable-graph-snapshots-to-the-realtime-thread.md):
    plugin instances live outside graph snapshots.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Plugins are third-party native code. When one crashes in-process, the whole DAW
goes down. CLAP has polyphonic modulation, lets the host share its thread pool, and
has an MIT licence. Most commercial plugins are still VST3 or AU. Reports say
Steinberg relicensed the VST3 SDK under MIT in 2025. Verify this, because it
changes the case for treating VST3 as secondary.

Points to consider:

- Out-of-process hosting adds IPC each buffer, for each plugin or group of plugins.
  At 64-sample buffers this overhead may dominate.
- Embedding plugin GUIs across processes is hard, particularly on macOS. Separate
  windows may be the only option.
- AUv3 on iOS and macOS already runs out of process, under Apple's control.

## Decision

Implement CLAP hosting first, then VST3, AU and LV2 as additional formats. Run
plugins outside the engine process in sandbox host processes that can be grouped
(per plugin, per vendor, or all together) and that share audio buffers through
shared memory. A plugin crash must leave the engine running and replace the
plugin's output with silence, or with a previously cached print where one exists.
The engine never starts or supervises plugin host processes. It only exchanges
audio with them through channels it is given, and treats a missed deadline like a
crash for that buffer.

## Alternatives considered

### In-process by default, with sandboxing as an option

This has the lowest latency and is the industry norm. One crash takes down the
engine.

### VST3 first

It reaches the most commercial plugins. It lacks CLAP's polyphonic modulation and
thread-pool model.

## Consequences

### Positive

- The engine survives plugin crashes and hangs.
- Plugins can later be offloaded to another machine on the LAN.

### Negative and trade-offs

- IPC costs latency and CPU. Cross-process GUI embedding is complex.
- Few plugins are available as CLAP at launch.

## Evidence

Required before acceptance:

- [ ] Measure the IPC round trip per buffer at 32, 64 and 128 samples, with one
      process per plugin and with grouped processes.
- [ ] Count CLAP availability among the 50 most-used plugins.
- [ ] Verify the VST3 SDK licence status.
- [ ] Spike plugin GUI embedding on Windows, macOS and Linux.

## Fitness functions

- Test: a plugin that segfaults does not stop engine playback. Its output is
  replaced with the fallback within one buffer.

## Review triggers

- IPC overhead exceeds the budget at the target buffer size.

## Notes

- 2026-10-04: Renumbered from the former ADR-0034 when ADRs were grouped by layer.
