# Tempo adoption (feature idea)

Product behaviour built on the `/tempo` aggregate. Clips are anchored to absolute
time by default (ADR-0033), so these features change the grid and never the audio.

## Adopt tempo from the first recording

- When the first audio clip is added to a project whose tempo no user has set, and
  the clip's tempo is known, set the tempo map from it. The tempo comes from file
  metadata (`acid`, ADR-0015) or from detection above a confidence threshold.
- Show the change ("Project tempo set to 93 BPM from this clip") and allow it to be
  undone.
- Adopt only once. Later clips never change the tempo, and clips with no known
  tempo leave it untouched.
- Skip adoption if the actor adding the clip has no `write` capability on `/tempo`
  (ADR-0019).
- Two peers adopting offline produce concurrent `/tempo` events, which ADR-0022
  surfaces as a conflict.

## Match project to recording

- Write a tempo map, which may vary over time, derived from a recording. Leave the
  recording itself unchanged.

## Open questions

- Is tempo detection in scope for the first release, or do users place beat
  markers manually?
- What confidence threshold should adoption require?
- How accurate is detection on loops, drums, speech, polyphonic music and live
  recordings?
- Do sample-pack loops prompt the user to fit them to the project tempo?
