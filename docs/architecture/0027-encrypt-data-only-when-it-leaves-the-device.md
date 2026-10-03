# 0027. Encrypt data only when it leaves the device

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0008](0008-provide-cryptography-through-a-managed-first-interface.md): AEAD
    primitives.
  - [ADR-0012](0012-frame-stored-objects-with-codec-and-length.md): frames are what
    gets encrypted.
  - [ADR-0013](0013-sign-with-recoverable-identity-keys.md): key recovery.
  - [ADR-0019](0019-authorize-actors-with-path-scoped-capabilities.md): capability
    holders define who receives plaintext.
  - [ADR-0026](0026-sync-peers-by-range-based-set-reconciliation.md): replication is
    where encryption applies.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Projects are replicated to peers and pinning nodes, which may not be trusted.
Encryption affects deduplication, addressing and long-term access: a lost key
means a lost song.

Points to consider:

- Encrypting with per-project keys prevents deduplication across projects.
- Convergent encryption (the key derived from the content) keeps deduplication but
  lets anyone confirm whether a known file is present.
- Nodes holding only ciphertext can only check ciphertext addresses, so ciphertext
  needs its own addresses next to the plaintext ones.
- OS full-disk encryption already protects local storage on most target platforms.
- Compression must happen before encryption, and compressed sizes reveal a little
  about content.

## Decision

Store content unencrypted in the local store, and rely on the OS for disk
encryption. Encrypt frames and event segments with per-project keys and AEAD
whenever they are replicated to a peer or pinning node outside the project's
capability holders. Address the ciphertext separately and map it to plaintext
addresses inside an encrypted manifest.

## Alternatives considered

### Encrypt everything locally

This protects against other local users and malware reading the store directly.
It costs CPU on every read, prevents cross-project deduplication, and makes key
loss destroy data locally too.

### Convergent encryption everywhere

Deduplication survives. Confirmation-of-file attacks reveal which samples a user
has, and that matters for unreleased material.

### No encryption at all

This is simplest, but untrusted pinning becomes impossible.

## Consequences

### Positive

- The local store stays fast and deduplicated.
- Untrusted replicas learn only sizes and timing.

### Negative and trade-offs

- There are two address spaces to keep in step.
- Project keys need recovery tied to the identity root key.

## Evidence

Required before acceptance:

- [ ] Write a threat model: who are the adversaries (other local users,
      pinning-node operators, network observers)?
- [ ] Measure how much cross-project deduplication is lost with per-project keys
      on replicas.
- [ ] Define the key rotation and member-removal flow (forward secrecy for removed
      collaborators).

## Fitness functions

- Test: no plaintext frame is ever sent to a peer outside the project's capability
  holders.

## Review triggers

- A platform without OS disk encryption becomes a target.
- Users require local encryption.
