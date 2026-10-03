# 0020. Make a DAG of signed events the source of truth

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0001](0001-drive-the-engine-through-one-typed-api.md): commands come from
    the typed API.
  - [ADR-0005](0005-encode-persisted-data-as-deterministic-cbor.md) and
    [ADR-0006](0006-evolve-schemas-additively.md): event encoding and evolution.
  - [ADR-0010](0010-hash-and-sign-uncompressed-canonical-bytes.md): event hashes and
    signatures.
  - [ADR-0012](0012-frame-stored-objects-with-codec-and-length.md): segments are
    stored as frames.
  - [ADR-0013](0013-sign-with-recoverable-identity-keys.md): event authorship.
  - [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md): each event targets one
    path.
  - [ADR-0019](0019-authorize-actors-with-path-scoped-capabilities.md): the `write`
    ability is checked on every event.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

A project should behave like a git repository: full history, named branches, and
merging and attribution between humans and agents. Conventional DAW project files
are snapshots. They keep no history beyond a session undo stack and record nothing
about who changed what.

Edits happen concurrently and offline, and there is no central server to put them
in order. A change can therefore only be judged valid against the state its author
could see.

Unresolved questions:

- How is content redacted (for example removing a collaborator's contribution or a
  leaked sample) when history is append-only and signed? The likely approach is
  tombstones plus blob deletion, which leaves hashes that point at nothing.
- How are named refs (branches) represented: as mutable pointers kept outside the
  DAG, as signed ref-update entries, or as per-actor namespaces?

## Decision

Make an append-only DAG of signed events the only authoritative project state.

- **Commands stay local.** A decider validates each command from the API against
  the actor's current heads and produces an event. Commands are never persisted
  or replicated.
- **Events record their parents.** Each event lists the hashes of the heads it was
  produced against, and targets exactly one aggregate path.
- **Events are valid relative to their parents.** Any receiver can re-check an
  event against the state at its parents, its signature, and the author's
  capability for the `write` ability covering the event's path.
- **Everything else is a projection.** The system must rebuild every projection
  from the DAG alone and produce identical bytes.
- **Events are stored in segments.** A segment of consecutive events is the unit
  of storage and compression for the DAG.

## Alternatives considered

### Snapshot project file (conventional DAW)

This is simple and well understood. It cannot merge or attribute changes, and it
keeps history only as manual "save as" copies.

### Snapshot commits of a state tree (git model)

Diffs are computed between trees. The intent of a change is lost ("moved clip"
becomes "these bytes changed"), and reviewers and agents need that intent.

### Replicate commands and re-run deciders on every peer

Authorisation attaches naturally to commands. Every historical decider version
must then be kept and replayed exactly, and a long-offline peer's late command can
reorder history and invalidate later work.

### Linear log with a central sequencer

Ordering is trivial, but it needs a server in the critical path.

## Consequences

### Positive

- Full history, attribution and branching come with the model.
- Parent hashes make history tamper-evident and give each event its causal
  context.
- Replay applies recorded events and never re-runs deciders.
- A corrupted projection or cache is recovered by rebuilding it.

### Negative and trade-offs

- The DAG grows without bound. Checkpoints and a compaction policy for cold
  projections are needed.
- Every event schema ever written must stay readable.
- Every event carries at least 32 bytes per parent hash.
- Redaction and legal removal need explicit design.

## Evidence

Required before acceptance:

- [ ] Estimate DAG growth per hour for a realistic session (editing, automation
      recording, agent work).
- [ ] Benchmark replay time for 1M events, with and without checkpoints, using
      BenchmarkDotNet.
- [ ] Choose the segment size from compression ratio, transfer granularity and
      crash-loss window.
- [ ] Write down a redaction design and check it against the signing rules.
- [ ] Choose how refs are represented.

## Fitness functions

- Property test: for random event DAGs, applying projections incrementally in any
  causal delivery order gives the same result as a full rebuild.
- Test: delete the index and caches, rebuild, and compare the bytes.
- Test: an event whose author lacks `write` on its path subtree is rejected by
  every receiver.

## Review triggers

- Replay or checkpoint costs exceed the budget for opening a project.
- A legal or user requirement for hard deletion cannot be met with tombstones.
