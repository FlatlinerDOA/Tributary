# 0042. Preview experiments in local draft overlays

- Status: Proposed
- Recorded: 2026-10-04
- Decision state in code: Planned
- Depends on:
  - [ADR-0019](0019-authorize-actors-with-path-scoped-capabilities.md): committing
    needs the same abilities as the events it records.
  - [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md): events are
    valid relative to their parents; branches.
  - [ADR-0022](0022-detect-conflicts-deterministically.md): conflicts with events
    that arrived during the draft.
  - [ADR-0024](0024-derive-reactions-and-subscriptions-from-projections.md):
    projections and subscriptions read the draft.
  - [ADR-0031](0031-swap-immutable-graph-snapshots-to-the-realtime-thread.md):
    switching between the main and draft graphs.
  - [ADR-0033](0033-compose-warp-into-one-time-map-per-clip.md): cache entries keyed
    by hash.
  - [ADR-0037](0037-deliver-generated-content-as-branch-proposals.md): shared
    proposals live on branches.
  - [ADR-0040](0040-integrate-aggregates-through-projections-and-application-operations.md):
    clients change state through operations and batches.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Users experiment: they try a different chain, tempo or arrangement, listen, and
usually throw it away. Branches
([ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md)) can hold
experiments, but every edit on a branch is a signed event that is stored, synced
and kept in history. That costs too much for an experiment that will most likely be
thrown away, and it fills the history with abandoned work.

Clients need the same thing for responsiveness. A UI wants to show the result of an
operation immediately, before it is committed, and an agent wants to apply several
operations all-or-nothing.

Unresolved questions:

- Are draft events signed? Unsigned drafts cost less but cannot leave the device.
- On commit, are draft events replayed one by one or squashed into fewer
  intent-level events?
- Must drafts survive a crash or restart?
- Can several drafts exist at once for side-by-side comparison? Each needs its own
  compiled graph and cache space.

## Decision

1. **A draft is a local overlay of events on top of a set of heads.** Operations
   applied to a draft are decided by the same deciders and produce ordinary events,
   held in the overlay instead of the DAG. Drafts are never synced.
2. **Projections and graph compilation can read heads plus overlay.** Clients
   subscribe to the draft's state like any other state, so the UI shows the result
   at once.
3. **The user can switch between the main graph and the draft graph** during
   playback. Each is compiled to its own snapshot and swapped in atomically.
4. **Discarding a draft leaves no trace in the DAG.** Cache entries created for it
   are keyed by hash and are evicted like any other cache entry.
5. **Committing records the draft as signed events against the current heads.**
   Events that arrived meanwhile are handled by ordinary validation against parents
   and conflict detection, as for any concurrent edit. The committing actor needs
   the abilities each recorded event requires.
6. **A draft is shared by promoting it to a branch.** Promotion records its events
   on a new branch, where the proposal rules of
   [ADR-0037](0037-deliver-generated-content-as-branch-proposals.md) apply.
7. **Batches from clients and agents run in a draft.** A batch is applied to a
   draft and committed only if every operation in it succeeds.

## Alternatives considered

### A branch for every experiment

Experiments get the full history and sync model. Every abandoned idea is stored,
synced and signed for good.

### Undo instead of drafts

Nothing new is needed. Comparing with the previous state means undoing and redoing
during playback, and the abandoned edits stay in history.

### Client-side optimistic state only

It is simple for one client. Other clients and the engine cannot hear the
experiment, and each client re-implements the domain rules.

## Consequences

### Positive

- Experiments cost nothing in history or sync until they are kept.
- The engine plays drafts, so previews sound exactly like the committed result.
- One mechanism serves user experiments, optimistic UI and all-or-nothing agent
  batches.

### Negative and trade-offs

- A second compiled graph and its cache entries use memory and CPU while a draft is
  active.
- A long-running draft can conflict with many events that arrived during it.
- Unless drafts are persisted, a crash loses the experiment.

## Evidence

Required before acceptance:

- [ ] Decide the four unresolved questions.
- [ ] Measure the memory and CPU of compiling and holding a second graph snapshot for
      a large project.
- [ ] Prototype committing a draft after concurrent events have arrived, and check
      the resulting conflict set against committing the same events directly.

## Fitness functions

- Test: discarding a draft leaves the DAG and its heads unchanged.
- Test: committing a draft produces the same state as applying its operations
  directly to the same heads.
- Test: a batch with one failing operation commits nothing.
- Test: draft events are never offered to sync.

## Review triggers

- Users need to share drafts live, without promoting them to a branch.
- Holding two compiled graphs is too expensive on target devices.
