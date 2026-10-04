# 0602. Stretch in real time and cache renders by hash

- Layer: L5 Engine
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0100](0100-keep-the-core-managed-with-optional-native-providers.md): limits
    on native stretch libraries.
  - [ADR-0107](0107-address-content-with-multihash-sha-256.md): cache keys.
  - [ADR-0503](0503-compose-warp-into-one-time-map-per-clip.md): the composed time
    map each clip is rendered through.
  - [ADR-0601](0601-swap-immutable-graph-snapshots-to-the-realtime-thread.md): the
    engine that plays the real-time tier.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

The composed time map of
[ADR-0503](0503-compose-warp-into-one-time-map-per-clip.md) has to be rendered
during playback, and rendering it well is expensive.

Points to consider:

- Real-time and high-quality stretch algorithms sound different. Swapping one for
  the other in the middle of playback can be audible.
- A cache keyed on the algorithm version only reproduces renders if the algorithm is
  bit-exact across CPUs. SIMD, FMA and the math library can change float results.
- Algorithm licensing (Rubber Band is GPL or commercial) and the managed-core
  principle limit the choice of algorithm.
- Transients, onsets, pitch and spectral frames can be analysed once at import,
  independent of tempo.

## Decision

Use a real-time stretch for playback and swap in high-quality background renders
when they are ready. Key cached renders by `hash(source, composed warp parameters,
tempo-map segment, algorithm id and version)` and invalidate only the affected time
ranges. The engine may cache any stage this way. Cache entries are local, never
synced and never managed by users.

## Alternatives considered

### High-quality render only, with no real-time tier

Output is consistent. Users have to wait after every tempo edit.

## Consequences

### Positive

- Cache invalidation follows the timeline ranges that actually changed.

### Negative and trade-offs

- Two algorithms must be maintained, and the swap between them made inaudible.

## Evidence

Required before acceptance:

- [ ] Measure cross-platform bit-exactness of the chosen high-quality algorithm.
      If it is not exact, the cache must not be shared across devices.
- [ ] Shortlist stretch algorithms with licences and implementation language.

## Fitness functions

- Test: editing the tempo in bars 9 to 12 invalidates only cache entries that
  overlap those bars.
- Test: cache entries are never included in sync.

## Review triggers

- A single high-quality algorithm runs in real time within the CPU budget on the
  lowest target device.

## Notes

- 2026-10-04: Split from the former ADR-0033 when ADRs were grouped by layer. The
  rule that the engine's cache is local and never synced moved here from the
  render and store record.
