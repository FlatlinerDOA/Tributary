# 0705. Run one host per user, with the engine in its own process

- Layer: L6 Host and API
- Status: Proposed
- Recorded: 2026-10-04
- Decision state in code: Planned
- Depends on:
  - [ADR-0109](0109-sign-with-recoverable-identity-keys.md): device and agent
    signing keys.
  - [ADR-0302](0302-authorize-actors-with-path-scoped-capabilities.md): each actor's
    capabilities.
  - [ADR-0310](0310-hold-uncommitted-edits-in-local-draft-overlays.md): draft
    overlays the host can compile for preview.
  - [ADR-0400](0400-sync-peers-by-range-based-set-reconciliation.md): sync between
    peers.
  - [ADR-0402](0402-share-projects-as-self-describing-trib-bundles.md): the project
    store is packed into bundles.
  - [ADR-0403](0403-develop-the-data-layer-as-a-separable-library.md): the data
    layer is a library that can be hosted anywhere.
  - [ADR-0600](0600-implement-the-realtime-engine-in-csharp.md): the engine runs in
    a dedicated process so that almost nothing allocates there.
  - [ADR-0601](0601-swap-immutable-graph-snapshots-to-the-realtime-thread.md):
    graph snapshots and the parameter queue.
  - [ADR-0603](0603-host-clap-plugins-first-out-of-process.md): plugin host
    processes share audio buffers through shared memory.
  - [ADR-0700](0700-drive-the-engine-through-one-typed-api.md): every client uses
    one typed API; whether the engine runs in or out of process is left open there.
  - [ADR-0701](0701-make-api-serialization-pluggable.md): codecs and framing are
    separate from transports.
  - [ADR-0702](0702-expose-application-operations-to-clients.md): clients call
    application operations.
  - [ADR-0703](0703-route-realtime-gestures-through-a-fast-path.md): gestures reach
    the parameter queue directly.
  - [ADR-0704](0704-timestamp-api-messages-in-declared-clock-domains.md): clock
    offsets are estimated per session.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Several clients use one project at once: a GUI (possibly React in a browser), the
CLI, and agents through MCP. Serving the API means decoding messages, deciding
commands, signing events, maintaining projections and syncing. All of these
allocate, and allocation in the engine process causes the GC pauses that
[ADR-0600](0600-implement-the-realtime-engine-in-csharp.md) is designed to avoid.

Facts:

- The real-time thread must never block, take a lock or make a system call, so it
  can communicate only through lock-free structures in memory.
- Browsers cannot use named pipes or shared memory. WebSockets are their
  bidirectional transport.
- Any web page in the user's browser can try to connect to a server on
  `localhost`.
- On Windows, ASIO drivers usually allow only one process to open a device, and
  exclusive-mode audio elsewhere behaves similarly.
- iOS apps cannot start helper processes; the only separate processes are app
  extensions such as AUv3. Android apps can run services in separate processes,
  but the OS kills background processes aggressively.
- MCP servers are reached over stdio or streamable HTTP.

Unresolved questions:

- Where graph snapshots are compiled: by the host into a flat binary layout in
  shared memory that the engine maps without allocating, or by the engine from a
  compact description using preallocated memory.
- How long the idle timeout is, and whether a host keeps running as a background
  sync peer by default.

## Decision

1. **Three kinds of process.**
   - The **host** runs the data layer, deciders, projections, application
     operations, sync and the API server.
   - The **engine** runs only real-time audio.
   - **Plugin hosts** run plugins, as in
     [ADR-0603](0603-host-clap-plugins-first-out-of-process.md).

   The host starts and supervises the engine and plugin host processes, restarts
   them after a crash, and hands each side its channel endpoints. No other process
   starts processes.
2. **One writer loop per open project.** Inside the host, one loop applies commands
   one at a time: decide, append, then update projections. API sessions, sync and
   application operations send their work to that loop as messages, and
   subscriptions are notified after each change. Lower layers create no threads of
   their own.
3. **One host per user session.** It can open several projects and holds an
   exclusive OS lock on the local store of each project it opens. It runs one
   engine, which owns the audio device.
4. **API clients are not sync peers.** A GUI, CLI, agent or remote control is an
   API client of a host and keeps no copy of the project. Hosts on different devices
   are peers and sync under
   [ADR-0400](0400-sync-peers-by-range-based-set-reconciliation.md).
5. **Clients find the host through a runtime file.** The host writes a file in the
   per-user runtime directory, readable only by that user, holding its process ID,
   endpoints, session token and API version. A client reads the file and connects.
   If there is no file, or the host is gone, the client starts a host and waits for
   its ready signal. The file is never written into a project store, so it is never
   synced or bundled.
6. **The OS lock decides ownership, not the runtime file.** When two hosts start
   at once, the one that fails to take the lock exits, and its client reads the
   winner's file. A crashed host releases its locks automatically.
7. **Clients connect over one of three transports**, all carrying the same
   contract and codecs ([ADR-0701](0701-make-api-serialization-pluggable.md)):
   - in-process calls, for the CLI, tests and mobile apps;
   - a named pipe or Unix domain socket, for native clients;
   - WebSockets, for browsers. The host serves the web UI itself and opens it
     with the session token in the URL fragment.
8. **Local connections are authenticated.** The host requires the session token
   and checks the `Origin` header of every WebSocket connection. Devices on the LAN
   discover hosts through mDNS/DNS-SD and must pair first, receiving a capability
   scoped under [ADR-0302](0302-authorize-actors-with-path-scoped-capabilities.md).
9. **Each session acts as one actor.** The host signs a session's events with that
   actor's key: the user's device key for the GUI and CLI, and the agent's own key
   ([ADR-0109](0109-sign-with-recoverable-identity-keys.md)) for an agent. Clock
   offsets under
   [ADR-0704](0704-timestamp-api-messages-in-declared-clock-domains.md) are
   estimated between client and host and between host and engine.
10. **Host and engine communicate as follows:**
   - Parameter changes, gestures and transport commands go through lock-free
     single-producer, single-consumer ring buffers in shared memory, which the
     real-time thread reads at buffer boundaries.
   - Telemetry (meters, playhead, xrun counts) is written to latest-value slots in
     shared memory and may be lost.
   - Graph snapshots are handed over off the real-time thread and swapped in
     atomically ([ADR-0601](0601-swap-immutable-graph-snapshots-to-the-realtime-thread.md)).
   - Lifecycle and errors use a local socket or pipe, whose closing tells the host
     that the engine died.
11. **Gestures go through the host.** Clients send gestures to the host, which
    pushes them into the engine's ring. A native client may be given its own ring
    into the engine only if measurements show that the extra hop is too slow.
12. **Drafts can be previewed.** The host can compile a draft's state into a second
    graph snapshot, so the user can switch between the main graph and the draft
    during playback.
13. **On mobile, everything runs in one process.** On iOS, host, engine and UI must
    share a process. On Android they share one by default. The channels in point 10
    are interfaces backed by ordinary memory in this layout, so the same code runs
    on every platform.
14. **The host stops when idle.** It shuts down after an idle timeout once no
    client is connected and nothing is playing, recording, rendering or syncing. A
    client that needs a newer API than the running host offers asks the user to
    restart the host. It never starts a second host.

## Alternatives considered

### One host per project

A host crash affects only one project. Several engines then compete for the audio
device, which ASIO and exclusive-mode drivers usually allow only one process to
open.

### Every process as a sync peer

There is one mechanism for everything. Every process keeps its own copy of the
store, the UI waits for sync instead of receiving subscriptions, and the engine
would run the data layer.

### Serve the API from the engine process

There is one process fewer. Serving the API allocates constantly, which brings back
the GC pauses ADR-0600 is designed to avoid.

### Localhost UDP between host and engine

It is simple and available everywhere. Every message is a system call and a kernel
copy, delivery is not guaranteed, and the real-time thread still cannot read it.

### Unauthenticated localhost server

There is nothing to configure. Any web page in the user's browser could read and
change projects.

## Consequences

### Positive

- Nothing in the engine process serves the API, decodes messages or syncs.
- Browser, native, agent and in-process clients share one contract.
- One engine owns the audio device, so projects never compete for it.
- Discovery survives crashes, because OS locks are released by the operating
  system.

### Negative and trade-offs

- A host crash closes every open project.
- Gestures make an extra local hop through the host.
- On mobile, the engine shares a process with allocating code, so the GC risk in
  ADR-0600 is at its highest there. A native real-time kernel is more likely to be
  needed on mobile than on desktop.
- Running a localhost server adds attack surface that must be tested.

## Evidence

Required before acceptance:

- [ ] Measure gesture latency from a WebSocket client through the host to an
      audible change.
- [ ] Prototype the start, lock and connect race with two clients starting at once,
      and with a crashed host.
- [ ] Test the WebSocket endpoint against cross-origin pages and DNS rebinding.
- [ ] Confirm audio-device exclusivity on ASIO, WASAPI exclusive mode, Core Audio
      and ALSA.
- [ ] Decide where graph snapshots are compiled.

## Fitness functions

- Architecture test: engine-process assemblies do not reference the data layer or
  any API transport.
- Test: a second host started on a locked project exits, and its client connects to
  the first host.
- Test: a WebSocket connection with a wrong token or foreign `Origin` is refused.
- Test: the runtime file is never written inside a project store.
- Test: the same client scenario passes over in-process, pipe and WebSocket
  transports.

## Review triggers

- Users regularly need two projects playing at once on different audio devices.
- Gesture latency through the host exceeds the controller latency budget.
- A mobile platform allows the engine to run in a separate process.

## Notes

- 2026-10-04: Renumbered from the former ADR-0043 when ADRs were grouped by layer.
  Gained process supervision (moved out of the engine), one writer loop per project,
  and draft preview.
