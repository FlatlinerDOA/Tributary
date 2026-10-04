# 0033. Compose warp into one time map per clip

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0002](0002-keep-the-core-managed-with-optional-native-providers.md): limits
    on native stretch libraries.
  - [ADR-0004](0004-treat-audio-sources-as-immutable.md): warps are render-time
    transforms of the original source.
  - [ADR-0009](0009-address-content-with-multihash-sha-256.md): cache keys.
  - [ADR-0031](0031-swap-immutable-graph-snapshots-to-the-realtime-thread.md): the
    engine that plays the real-time tier.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

When stretches are stacked (warp markers, then a tempo change, then a clip
stretch), each render adds artefacts. Sources are immutable, so every warp is
resolved at render time.

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

Represent every warp, stretch and tempo-map effect on a clip as one composed,
monotonic function from source time to project time, applied in a single pass over
the original source. A clip is anchored to absolute time by default. It follows
the tempo map only when the user anchors it to musical time, so a tempo change does
not alter the audio of a clip anchored to absolute time. A clip whose composed
function is the identity is not stretched. A stretch applied to audio produced from
a new source (see [ADR-0004](0004-treat-audio-sources-as-immutable.md)) acts on that
source, not on the original, and is not composed with earlier stretches. Use a
real-time stretch for playback and swap in high-quality background renders when
they are ready. Key cached renders by
`hash(source, composed warp parameters, tempo-map segment, algorithm id and
version)` and invalidate only the affected time ranges.

## Alternatives considered

### Stacked renders (one per transform)

Each step is simple. Artefacts compound.

### High-quality render only, with no real-time tier

Output is consistent. Users have to wait after every tempo edit.

## Consequences

### Positive

- One resampling pass per clip, whatever the edit history.
- Cache invalidation follows the timeline ranges that actually changed.

### Negative and trade-offs

- The composition maths has to be correct and tested (monotonicity, numerical
  drift).
- Two algorithms must be maintained, and the swap between them made inaudible.

## Evidence

Required before acceptance:

- [ ] Listening tests and null tests comparing composed vs. stacked stretches.
- [ ] Measure cross-platform bit-exactness of the chosen high-quality algorithm.
      If it is not exact, the cache must not be shared across devices.
- [ ] Shortlist stretch algorithms with licences and implementation language.

## Fitness functions

- Property test: composing warps A∘B equals applying the two time maps in sequence,
  within tolerance.
- Test: editing the tempo in bars 9 to 12 invalidates only cache entries that
  overlap those bars.
- Test: changing the tempo map leaves the rendered audio of a clip anchored to
  absolute time unchanged.

## Review triggers

- A stretch algorithm needs per-pass state that cannot be composed.

## Notes

- 2026-10-04: The Decision now says that clips are anchored to absolute time by default, that a clip with an identity map is not stretched, and that a stretch applied to a newly created source is not composed with stretches applied before it.
