# 0400. Sync peers by range-based set reconciliation

- Layer: L3 Replication
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0202](0202-frame-stored-objects-with-codec-and-length.md): objects are
    transferred as frames.
  - [ADR-0203](0203-store-blobs-as-content-defined-chunks.md): the chunks being
    synced.
  - [ADR-0301](0301-select-paths-with-one-pattern-language.md): sync scopes.
  - [ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md): the DAG
    being synced and validated.
  - [ADR-0305](0305-detect-conflicts-deterministically.md): what convergence means
    for conflicting edits.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Peers and pinning nodes are equal, and no service sits in the critical path. Two
devices that edited offline must reconnect and converge. Both the event DAG and the
chunks have to sync. The peer transport (connections, NAT traversal, relays and
discovery) has not been chosen yet. This decision only requires reliable, ordered,
authenticated streams between two peers.

Points to consider:

- Set reconciliation transfers the missing set, but events can only be validated
  once their parents are present.
- A device may want only part of a project (for example a phone syncing one track).

## Decision

Synchronise events and chunks between peers by range-based set reconciliation
(Negentropy or Willow style) over any peer transport that provides reliable,
ordered, authenticated streams. Exchange no central sequence numbers.

- **What is synchronised:** the event DAG together with each peer's current heads
  and refs, and the chunks those events reference.
- **Causal order:** a received event is validated and applied only after all its
  parents are present. Events whose parents are missing are buffered and their
  parents fetched.
- **Scope:** a sync session is limited by a path-pattern selector, which allows
  partial replication of subtrees. Matching includes events at ancestor paths.
- **Convergence:** after sync, peers holding the same events have identical state
  for non-conflicting edits and an identical conflict set for the rest.

## Alternatives considered

### Git-style want/have negotiation

It is proven. Its efficiency drops when histories diverge a lot and when the DAG
has many heads.

### Automerge sync protocol (Bloom filters)

It is designed for CRDT documents. It is tied to Automerge's data model.

### IPFS Bitswap

It is good for fetching blobs. It does not reconcile logs.

## Consequences

### Positive

- Round trips grow logarithmically with set size, and there is no coordinator.
- Partial replication follows the path hierarchy.

### Negative and trade-offs

- Tributary has to implement and maintain the protocol itself.
- Causal buffering adds complexity.
- A partial replica needs enough state outside its scope to validate events in
  scope.

## Evidence

Available:

- [Negentropy](https://github.com/hoytech/negentropy);
  [Willow](https://willowprotocol.org); Meyer, "Range-Based Set Reconciliation"
  (arXiv:2212.13567).

Required before acceptance:

- [ ] Simulate two logs of 1M events with 1,000 differences, measuring bytes and
      round trips for each candidate.
- [ ] Choose the peer transport (candidates: libp2p, iroh, QUIC through
      `System.Net.Quic` with Tributary's own NAT traversal and relays, WebRTC data
      channels) and design how sync maps onto it.
- [ ] Define what a partial replica must hold to validate events in its scope:
      parents outside the scope, and checkpoints of ancestor state.
- [ ] Assess Willow's path-based data model and Meadowcap capabilities as a fit for
      Tributary paths and capabilities.

## Fitness functions

- Test: two devices co-edit offline and reconnect. Non-conflicting edits converge
  to the same projection bytes, and both devices expose an identical conflict set.
- Test: an event delivered before its parents is applied only once they arrive.

## Review triggers

- A standard local-first sync protocol becomes widely adopted.

## Notes

- 2026-10-04: Renumbered from the former ADR-0026 when ADRs were grouped by layer.
