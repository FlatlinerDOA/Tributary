# 0109. Sign with recoverable identity keys

- Layer: L0 Primitives
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0102](0102-encode-persisted-data-as-deterministic-cbor.md): certificate
    encoding.
  - [ADR-0106](0106-provide-cryptography-through-a-managed-first-interface.md):
    signature and key-derivation primitives.
  - [ADR-0108](0108-hash-and-sign-uncompressed-canonical-bytes.md): what a
    signature covers.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Every persisted change is signed and attributed to a human or an agent, with
model and licence metadata for agents. There is no central server, so identity
must be self-sovereign. A lost key must not lose the user their identity or their
encrypted data.

Unresolved questions:

- How are compromised device keys revoked when peers are offline?
- How can a signature be shown to predate a later compromise? Trusted timestamps
  or anchoring are options.

## Decision

Give each user a root identity key that is used only to certify per-device and
per-agent signing keys. Sign every change with a device or agent key whose
certificate chain leads to the user root. Actor IDs are derived from public keys.
Protect the root key with a key-encryption key that can be recovered through
SLIP-39 Shamir shares.

## Alternatives considered

### One user key copied to every device

This is simple. A compromise of any device compromises everything, and revoking a
device means rotating the identity.

### BIP-39 mnemonic of the root key

It is familiar from crypto wallets. A single phrase is a single point of failure,
and there is no threshold sharing.

### Passkeys / WebAuthn

They are good for usability and synced by the platform. They are tied to vendor
ecosystems and designed for login challenges, not for signing arbitrary data
offline.

### Recovery escrow on a server

Users are used to it, but it needs a central server.

## Consequences

### Positive

- A device can be revoked without changing the user's identity.
- Agents get their own keys, so their changes are attributable separately from the
  user who runs them.

### Negative and trade-offs

- Certificate chains must be distributed and checked wherever signatures are
  verified.
- Distributing SLIP-39 shares is a UX problem.

## Evidence

Available:

- [SLIP-0039](https://github.com/satoshilabs/slips/blob/master/slip-0039.md).

Required before acceptance:

- [ ] Specify the certificate format (CBOR) and revocation semantics.
- [ ] Prototype the recovery UX.
- [ ] Choose a timestamping or anchoring strategy, or record that there is none.

## Fitness functions

- Test: a signature by a revoked device key after its revocation point is rejected.
- SLIP-39 test vectors pass.

## Review triggers

- Post-quantum migration of the signature algorithm.

## Notes

- 2026-10-04: Renumbered from the former ADR-0013 when ADRs were grouped by layer.
