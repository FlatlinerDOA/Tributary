# 0107. Address content with multihash SHA-256

- Layer: L0 Primitives
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0106](0106-provide-cryptography-through-a-managed-first-interface.md): the
    hash function comes from the crypto interface.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Every stored blob, chunk, tree node and record is addressed by its hash.
Addresses must stay valid for 20 years, and it must be possible to migrate to a
new algorithm without repeating git's long SHA-1 → SHA-256 migration.

Facts:

- SHA-256 is in the BCL and is hardware accelerated on x64 (SHA-NI) and on ARMv8
  cryptography extensions.
- BLAKE3 is faster and has a built-in tree mode, but it is not in the BCL. A
  managed port would be slower than the native SIMD implementation.
- A home-grown Merkle tree needs **domain separation** between leaf and
  internal-node hashes (for example the `0x00`/`0x01` prefixes in RFC 6962).
  Without it, an internal node can be presented as a leaf, a second-preimage
  attack.

## Decision

Address all content with SHA-256 digests carrying a multihash prefix
(code `0x12`, length `0x20`). Build Merkle trees with prefixes that separate leaves
from internal nodes. All code handles addresses as opaque multihash values, so
other algorithms can be added later.

## Alternatives considered

### BLAKE3

It is much faster for large blobs and has a native tree mode. It needs a native
dependency or a slower managed port. It remains an option to add later.

### Unprefixed SHA-256

It saves two bytes per address. The algorithm cannot change without breaking every
reference.

### CIDv1 (multibase + multicodec + multihash)

It also encodes the content codec and is compatible with IPFS. It adds complexity
and a dependency on multiformats conventions.

## Consequences

### Positive

- Pure managed code, conservative and hardware accelerated.
- The algorithm can be changed later.

### Negative and trade-offs

- Hashing large audio imports is slower than with BLAKE3.
- The Merkle tree layout is Tributary's own design and has to be specified.

## Evidence

Available:

- [multihash spec](https://github.com/multiformats/multihash);
  [RFC 6962](https://www.rfc-editor.org/rfc/rfc6962) §2.1.

Required before acceptance:

- [ ] BenchmarkDotNet: BCL SHA-256 throughput on x64, Apple Silicon, iOS and
      Android ARM64, compared with a managed BLAKE3 port.
- [ ] Measure import time for a 2 GB multitrack session and decide whether hashing
      is a bottleneck.
- [ ] Write the Merkle tree specification, including leaf and node prefixes and
      fan-out.

## Fitness functions

- No API takes or returns a raw `byte[32]` as an address (architecture test).
- Merkle tree test vectors are committed.

## Review triggers

- Cryptanalysis weakens SHA-256.
- Hashing is shown to be an import bottleneck.

## Notes

- 2026-10-04: Renumbered from the former ADR-0009 when ADRs were grouped by layer.
