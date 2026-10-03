# 0037. Deliver generated content as branch proposals

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0019](0019-authorize-actors-with-path-scoped-capabilities.md): which
    branches an agent may write to.
  - [ADR-0023](0023-resolve-conflicts-with-semantic-events.md): acceptance is a
    resolution event.
  - [ADR-0031](0031-swap-immutable-graph-snapshots-to-the-realtime-thread.md):
    deterministic offline rendering of before and after audio.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Generated material (parts, arrangements, mix changes) needs human review before it
becomes part of the song. Musicians judge by ear, so a review means listening to
before and after renders, not reading a diff. Rendering before and after for every
proposal is expensive.

## Decision

Agents write generated changes to a new branch or a take lane, never to the branch
the user is working on, unless their capability explicitly allows it. Each proposal
includes a semantic diff, rendered before and after audio for the affected range,
and the agent's rationale.

A proposal is accepted by an `AcceptedProposal` resolution event whose parents are
the target head and the proposal head. Its `intent` records the scope (the whole
proposal, or a path scope such as the drum tracks), and its `decisions` list the
accepted and `Excluded` events, which is how cherry-picking works. Rejecting a
proposal records nothing on the target branch: the proposal branch is simply left
unmerged.

## Alternatives considered

### Apply directly, with undo

There is no review friction. Users lose track of what changed, and undo does not
work well alongside collaborators.

### Take lanes only

This matches how musicians already comp takes. It does not cover structural or mix
proposals.

## Consequences

### Positive

- Changes can be reviewed and attributed, and they are safe to explore in
  parallel.
- Proposals use the same branch and resolution machinery as concurrent edits.

### Negative and trade-offs

- Branches multiply. Rendering costs compute.

## Evidence

Required before acceptance:

- [ ] Prototype a partial acceptance (`Excluded` events) and check that its result
      passes the invariants of every affected aggregate.
- [ ] Measure the cost of before/after rendering for an 8-bar range.

## Fitness functions

- Test: an accepted proposal produces the same projection as applying its events
  directly.

## Review triggers

- Users want agents to edit live, which needs a "band member" mode instead.
