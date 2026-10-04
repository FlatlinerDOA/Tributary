# 0704. Timestamp API messages in declared clock domains

- Layer: L6 Host and API
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0601](0601-swap-immutable-graph-snapshots-to-the-realtime-thread.md): the
    engine sample clock and parameter queue that timestamps are converted into.
  - [ADR-0603](0603-host-clap-plugins-first-out-of-process.md): offloading plugins to
    other machines is the main use of media streams.
  - [ADR-0700](0700-drive-the-engine-through-one-typed-api.md): timed messages are
    API messages.
  - [ADR-0701](0701-make-api-serialization-pluggable.md): the clock domain is a
    contract field, encoded by every codec.
  - [ADR-0703](0703-route-realtime-gestures-through-a-fast-path.md): gestures carry
    engine sample timestamps.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Clients, controllers, other apps and other machines send timed messages to the
engine: gestures, MIDI, transport commands and audio streams. "Time" means three
different things, each needing different precision:

| Clock | Used for | Precision needed | Existing protocols |
|---|---|---|---|
| **Wall clock** | Event metadata, display, latency measurement | Milliseconds | NTP ([RFC 5905](https://www.rfc-editor.org/rfc/rfc5905)) or the OS clock |
| **Musical timeline** | Shared tempo, beat and phase with other apps and devices | About 1 ms | [Ableton Link](https://github.com/Ableton/link); MIDI Clock and MTC |
| **Sample (media) clock** | Audio streams between machines (plugin offload, AES67 hardware) | Microseconds | PTP (IEEE 1588), gPTP (IEEE 802.1AS), AES67 (PTPv2 + RTP + SDP) |

Facts:

- RTP and RTCP ([RFC 3550](https://www.rfc-editor.org/rfc/rfc3550)) carry media
  timestamps, and RTCP sender reports map them to wall-clock time. AES67 and
  WebRTC both use this to keep streams in sync.
  [RFC 7273](https://www.rfc-editor.org/rfc/rfc7273) advertises a stream's clock
  source in SDP.
- MIDI 2.0 UMP has Jitter Reduction timestamps. RTP-MIDI
  ([RFC 6295](https://www.rfc-editor.org/rfc/rfc6295)) and Network MIDI 2.0 (UDP)
  carry MIDI over networks.
- OSC bundles carry NTP-format timestamps.
- Ableton Link is licensed GPLv2+ or commercially.
- Synchronised time does not mean synchronised samples. Audio interfaces drift by
  parts per million, so streaming audio between machines needs hardware locked to
  the media clock, or asynchronous sample-rate conversion with drift estimation.
  Software PTP without hardware timestamping typically reaches tens of
  microseconds on a LAN, which is under a few samples at 48 kHz.
- Ensemble playing tolerates roughly 25 ms of one-way latency. Light in fiber
  covers about 1,000 km per 5 ms, so live remote playing is limited to a city or
  region.
- Ordering of persisted changes does not depend on any of these clocks. It is
  decided by the event DAG.

## Decision

1. **Every timed API message declares its clock domain:** `wall` (UTC from the
   sender's clock), `engine` (sample position in the engine's sample clock), or
   `musical` (beat position on the project timeline). Every timestamp is converted
   into engine sample time before it reaches the engine, which handles late entries
   as [ADR-0601](0601-swap-immutable-graph-snapshots-to-the-realtime-thread.md)
   specifies. The reported lateness is passed back so clients can adjust how far
   ahead they schedule.
2. **Each session estimates its clock offset.** When a session opens, and
   periodically afterwards, client and engine exchange four timestamps NTP-style.
   The offset estimate is filtered to the samples with the lowest round-trip time,
   along with an estimate of drift. Clients may then send `engine` timestamps
   directly.
3. **External protocols map onto the domains:**
   - Musical timeline sync with other apps: Ableton Link (subject to a licence
     decision), with MIDI Clock and MTC as fallbacks.
   - Media streams between machines: RTP with RTCP mapping to wall-clock time,
     AES67-compatible where hardware interoperability is needed, using PTP when
     available. Drift is always corrected by sample-rate conversion unless the
     audio hardware is locked to the same media clock.
   - MIDI: MIDI 2.0 Jitter Reduction timestamps map onto `engine` time.
   - OSC: bundle timestamps map onto `wall` time.

## Alternatives considered

### A single global clock (everything in wall-clock time)

It is simple. Wall clocks are too imprecise for sample-accurate scheduling, and
musical positions change meaning whenever the tempo changes.

### Engine sample time only

It is precise. Clients cannot produce it without first estimating the offset, and
musical intent ("on beat 3") would be lost.

### Require PTP on every device

It gives the best precision. Most consumer networks and devices lack hardware
timestamping, and wall-clock and musical uses do not need it.

## Consequences

### Positive

- Sample-accurate scheduling from any client, with an explicit precision model.
- Compatibility with the standard ecosystem: Link, AES67 and MIDI 2.0.
- Musical-time messages survive tempo changes made between sending and playback.

### Negative and trade-offs

- Every timed message carries a domain tag, and the engine maintains an offset
  estimate per session.
- Drift correction (sample-rate conversion) costs CPU and adds a little latency to
  media streams.
- Ableton Link's licence may rule it out or require a commercial agreement.

## Evidence

Required before acceptance:

- [ ] Measure the accuracy of offset estimation over Wi-Fi and wired LAN
      (95th and 99th percentile error) with the four-timestamp exchange.
- [ ] Decide Ableton Link licensing against the project's licence.
- [ ] Spike an RTP audio stream between two machines with drift-correcting
      resampling, and measure added latency and quality.
- [ ] Check the status and C# availability of Network MIDI 2.0 (UDP)
      implementations.

## Fitness functions

- Test: a message with a `musical` timestamp sent before a tempo change plays at
  the correct beat after it.
- Soak test: offset estimates stay within the agreed bound over 8 hours with an
  artificially drifting client clock.

## Review triggers

- A target platform exposes hardware PTP to applications.
- Ableton Link changes its licence, or an open alternative appears.

## Notes

- 2026-10-04: Renumbered from the former ADR-0038 when ADRs were grouped by layer.
  Handling late entries in the engine moved to the graph snapshot record.
