# 0108. Hash and sign uncompressed canonical bytes

- Layer: L0 Primitives
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0102](0102-encode-persisted-data-as-deterministic-cbor.md): the canonical
    bytes of structured records.
  - [ADR-0106](0106-provide-cryptography-through-a-managed-first-interface.md): the
    signature primitives.
  - [ADR-0107](0107-address-content-with-multihash-sha-256.md): the address
    computed over those bytes.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Signatures and hashes break if anything re-encodes the bytes before checking them.
This happens when a parse → re-serialize round trip changes field order, float form
or integer width. Compression output is also not stable: zstd versions and settings
produce different bytes for the same input. If addresses were taken over
compressed bytes, identical content would fail to deduplicate and addresses would
depend on the compressor version.

## Decision

Compute every content address and every signature over the exact uncompressed
canonical bytes of the object: deterministic CBOR for structured records, and
canonical sample frames for audio. Readers recover those exact bytes (removing
any storage encoding) and verify the hash before any decoding. They never
re-encode an object in order to verify it. Storage encodings such as compression
are applied after hashing and never affect an address.

## Alternatives considered

### Hash the compressed bytes

Verification is cheaper because no decompression is needed. Addresses then depend
on the compressor version and settings, and identical content compressed
differently does not deduplicate.

### Sign a re-derived canonical form (JWS-style)

Storage could change the encoding freely. Verification would depend on a correct
re-canonicalizer, which is fragile across decades.

## Consequences

### Positive

- Addresses are stable regardless of compression settings or codec upgrades.
- Objects can be re-encoded for storage (for example recompressed during a repack)
  without changing their address.

### Negative and trade-offs

- Data must be decoded from its storage encoding before verification, which costs
  CPU during integrity checks.

## Evidence

Required before acceptance:

- [ ] Code-review rule or analyzer: verification APIs accept `ReadOnlySpan<byte>`
      of canonical bytes only, never a decoded object.

## Fitness functions

- Test: the same content stored with two different compression settings has the
  same address.
- Test: modifying one canonical byte fails verification before any decoding
  happens.

## Review triggers

- A storage encoding is adopted whose decoded output is not byte-exact.

## Notes

- 2026-10-04: Renumbered from the former ADR-0010 when ADRs were grouped by layer.
