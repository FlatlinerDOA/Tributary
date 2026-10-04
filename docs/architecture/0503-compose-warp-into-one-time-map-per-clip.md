# 0503. Compose warp into one time map per clip

- Layer: L4 Domain model
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0501](0501-resolve-audio-edits-as-render-time-transforms.md): warps are
    render-time transforms of the original source; a render pass creates a new
    source.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

When stretches are stacked (warp markers, then a tempo change, then a clip
stretch), each render adds artefacts. Sources are immutable, so every warp is
resolved at render time.

Many DAWs warp audio to the project tempo by default, so changing the tempo to
match a recording stretches the recording, when the user wanted the grid to follow
the audio.

## Decision

Represent every warp, stretch and tempo-map effect on a clip as one composed,
monotonic function from source time to project time, applied in a single pass over
the original source.

- A clip is anchored to absolute time by default. It follows the tempo map only
  when the user anchors it to musical time, so a tempo change does not alter the
  audio of a clip anchored to absolute time.
- A clip whose composed function is the identity is not stretched.
- A stretch applied to audio produced from a new source acts on that source, not on
  the original, and is not composed with earlier stretches.

## Alternatives considered

### Stacked renders (one per transform)

Each step is simple. Artefacts compound.

### Clips follow the project tempo by default

Loops lock to the grid immediately. Recordings are stretched whenever the tempo
changes, without the user asking.

## Consequences

### Positive

- One resampling pass per clip, whatever the edit history.
- Audio is only stretched when the user asks for it.

### Negative and trade-offs

- The composition maths has to be correct and tested (monotonicity, numerical
  drift).
- Loops do not follow the project tempo until the user anchors them to musical
  time.

## Evidence

Required before acceptance:

- [ ] Listening tests and null tests comparing composed vs. stacked stretches.

## Fitness functions

- Property test: composing warps A∘B equals applying the two time maps in sequence,
  within tolerance.
- Test: changing the tempo map leaves the rendered audio of a clip anchored to
  absolute time unchanged.

## Review triggers

- A stretch algorithm needs per-pass state that cannot be composed.

## Notes

- 2026-10-04: Clips are anchored to absolute time by default, a clip with an
  identity map is not stretched, and a stretch applied to a newly created source is
  not composed with earlier stretches.
- 2026-10-04: Split from the former ADR-0033 when ADRs were grouped by layer. How
  stretches are executed and cached moved to the engine layer.
