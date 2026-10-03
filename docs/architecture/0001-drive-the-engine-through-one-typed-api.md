# 0001. Drive the engine through one typed API

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on: None
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Tributary will be driven by a GUI, a CLI, scripts, hardware controllers and AI
agents. In conventional DAWs the GUI drives the engine through private calls, and
the scripting interface (for example ReaScript or the Live Object Model) is added
later, covers only part of the feature set and lags new features.

Facts:

- The first usable milestone is creating, editing, playing and rendering a
  multitrack project entirely from a CLI, before any GUI exists.
- Agents need the same validation and permission checks as humans.

Assumptions:

- One command/query contract can serve both in-process and out-of-process clients.
- High-rate UI streams (meters, waveform overviews, playhead) can go through the
  same API as subscriptions without breaking the UI frame budget.

Unresolved questions:

- Does the GUI run the engine in-process or connect to a separate engine process?
- Should telemetry streams (meters, spectra) use the same contract as commands, or
  a separate lossy channel?

## Decision

Run the engine headless. Expose all of its capabilities through one versioned,
typed command and query API. Every client, including the first-party GUI, uses
only this API. No client gets a private path into engine or domain state.

## Alternatives considered

### GUI-first application with a scripting API added later

This is quicker to get a demo running. It also produces two kinds of client: the
GUI can do everything and scripts or agents can only do some things.

### Separate APIs for humans and agents

Each could be tuned for its caller. The cost is two permission models and two
validation paths, which will drift apart.

### Untyped control protocol only (OSC or MIDI)

OSC and MIDI are widely supported by controllers, but they have no schema,
versioning or authorisation. They are better used as adapters on top of the typed
API.

## Consequences

### Positive

- The CLI, scripts and agents get the same features the GUI has.
- The engine can be tested end to end without a UI.
- The engine and UI can run in separate processes.

### Negative and trade-offs

- API design work is needed before the first visible feature.
- High-rate telemetry needs a streaming design that is efficient on every
  transport.
- API versioning becomes a public compatibility commitment.

## Evidence

Required before acceptance:

- [ ] Prototype streaming meters for 128 tracks at 60 Hz over an out-of-process
      transport and measure CPU and latency on the lowest target device.
- [ ] Spike a GUI screen that uses only the public contract assembly.
- [ ] Measure controller latency through the API, from the input event to the
      parameter change being audible.

## Fitness functions

- An architecture test (for example ArchUnitNET or NetArchTest) fails if any client
  assembly references engine or domain implementation assemblies rather than the
  API contract.
- An end-to-end scenario (create, edit, play and render a multitrack project) runs
  in CI using only the CLI.

## Review triggers

- A feature cannot be built without privileged access from the UI.
- Transport overhead breaks the UI frame budget or the controller latency budget.
