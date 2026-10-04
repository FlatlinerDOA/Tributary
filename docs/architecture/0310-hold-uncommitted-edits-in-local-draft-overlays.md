# 0310. Hold uncommitted edits in local draft overlays

- Layer: L2 Event model
- Status: Proposed
- Recorded: 2026-10-04
- Decision state in code: Planned
- Depends on:
  - [ADR-0302](0302-authorize-actors-with-path-scoped-capabilities.md): committing
    needs the same abilities as the events it records.
  - [ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md): events are
    valid relative to their parents; branches.
  - [ADR-0305](0305-detect-conflicts-deterministically.md): conflicts with events
    that arrived during the draft.
  - [ADR-0307](0307-derive-reactions-and-subscriptions-from-projections.md):
    projections and subscriptions read the draft.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Users experiment: they try a change, look at or listen to the result, and usually
throw it away. Branches
([ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md)) can hold
experiments, but every edit on a branch is a signed event that is stored, synced
and kept in history. That costs too much for an experiment that will most likely be
thrown away, and it fills the history with abandoned work.

Clients need the same thing for responsiveness: a UI wants to show the result of a
change immediately, before it is committed, and some changes must apply
all-or-nothing.

Unresolved questions:

- Are draft events signed? Unsigned drafts cost less but cannot leave the device.
- On commit, are draft events replayed one by one or squashed into fewer
  intent-level events?
- Must drafts survive a crash or restart?
- Can several drafts exist at once for side-by-side comparison?

## Decision

1. **A draft is a local overlay of events on top of a set of heads.** Commands
   applied to a draft are decided by the same deciders and produce ordinary events,
   held in the overlay instead of the DAG. Drafts are never synced.
2. **Projections can read heads plus overlay.** Clients subscribe to the draft's
   state like any other state, so they see the result at once.
3. **Discarding a draft leaves no trace in the DAG.**
4. **Committing records the draft as signed events against the current heads.**
   Events that arrived meanwhile are handled by ordinary validation against parents
   and conflict detection, as for any concurrent edit. The committing actor needs
   the abilities each recorded event requires.
5. **A draft is shared by promoting it to a branch.** Promotion records its events
   on a new branch.

## Alternatives considered

### A branch for every experiment

Experiments get the full history and sync model. Every abandoned idea is stored,
synced and signed for good.

### Undo instead of drafts

Nothing new is needed. Comparing with the previous state means undoing and redoing,
and the abandoned edits stay in history.

### Client-side optimistic state only

It is simple for one client. Other clients cannot see the experiment, and each
client re-implements the domain rules.

## Consequences

### Positive

- Experiments cost nothing in history or sync until they are kept.
- One mechanism serves user experiments, optimistic clients and all-or-nothing
  changes.

### Negative and trade-offs

- A draft's projections use memory while it is active.
- A long-running draft can conflict with many events that arrived during it.
- Unless drafts are persisted, a crash loses the experiment.

## Evidence

Required before acceptance:

- [ ] Decide the four unresolved questions.
- [ ] Prototype committing a draft after concurrent events have arrived, and check
      the resulting conflict set against committing the same events directly.

## Fitness functions

- Test: discarding a draft leaves the DAG and its heads unchanged.
- Test: committing a draft produces the same state as applying its commands
  directly to the same heads.
- Test: draft events are never offered to sync.

## Review triggers

- Users need to share drafts live, without promoting them to a branch.

## Notes

- 2026-10-04: Renumbered from the former ADR-0042 when ADRs were grouped by layer,
  and narrowed to the event model. Playing a draft in the engine moved to the host
  layer, and batches moved to the application operations record.
