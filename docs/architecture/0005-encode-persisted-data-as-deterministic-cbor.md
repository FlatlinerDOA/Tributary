# 0005. Encode persisted data as deterministic CBOR

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on: None
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Persisted records are hashed and signed, so a given value must always encode to
the same bytes. The format also has to be readable in 20 years with nothing more
than a text specification.

The intended implementation is a source generator over `System.Formats.Cbor` that
emits integer keys in ascending order, on the assumption that this is "canonical
by construction". That holds only partly:

- RFC 8949 §4.2.1 (core deterministic encoding) sorts map keys by the **bytewise
  lexicographic order of their encoded form**. For non-negative integer keys this
  matches numeric order. Negative integer keys break it: `-1` is encoded as `0x20`,
  which sorts after `23` (`0x17`) but before `24` (`0x18 0x18`).
- RFC 7049 §3.9 "canonical CBOR" uses length-first ordering, which is different.
  `System.Formats.Cbor` offers `CborConformanceMode.Canonical` and
  `Ctap2Canonical`, and it is not yet confirmed which of these orderings either
  mode enforces.
- Floating-point values need a rule: preferred (shortest) serialization, a single
  NaN representation, and a decision on `-0.0`.
  [draft-ietf-cbor-cde](https://datatracker.ietf.org/doc/draft-ietf-cbor-cde/)
  (Common Deterministic Encoding) and dCBOR (draft-mcnally-deterministic-cbor) are
  candidate profiles that define these rules.
- Indefinite-length items and duplicate keys must be rejected.

## Decision

Encode every persisted record using the RFC 8949 §4.2.1 core deterministic
encoding. Use non-negative integer map keys produced by a source generator, a
pinned float rule, no indefinite-length items and no duplicate map keys. Readers
reject non-conforming input. Archive CDDL (RFC 8610) schemas alongside persisted data.

## Alternatives considered

### Protocol Buffers

Protobuf has excellent tooling. Its deterministic serialization is explicitly not
canonical across languages or library versions, and the data is not
self-describing without the schema.

### JSON with JCS (RFC 8785)

JSON is readable by humans. It is larger, has no binary type, and canonical float
formatting through ES6 number serialization is easy to get wrong.

### MessagePack

It is compact and has broad support. It has no standardized deterministic
encoding.

### Custom binary format

It can be as compact as needed. It must be specified and maintained alone.

## Consequences

### Positive

- The format is an IETF standard and self-describing, with deterministic bytes for
  hashing and signing.
- Readers in other languages are easy to write.

### Negative and trade-offs

- The source generator and the CDDL schemas must be kept in step.
- Floats from different code paths (computed vs. parsed) must encode identically.

## Evidence

Available:

- [RFC 8949](https://www.rfc-editor.org/rfc/rfc8949) §4.2;
  [RFC 8610](https://www.rfc-editor.org/rfc/rfc8610) (CDDL).

Required before acceptance:

- [ ] Find out and document the key-ordering rule that `System.Formats.Cbor`'s
      conformance modes enforce, and whether they reject non-deterministic input.
- [ ] Choose a float profile (plain §4.2.1, CDE or dCBOR) and record why.
- [ ] Encode a fixture set with Tributary and with at least one independent
      implementation (for example Python `cbor2` or Rust `ciborium`) and compare
      the bytes.

## Fitness functions

- Round-trip and canonical-bytes tests in the generator project, including
  fuzzing (decode → encode → bytes match).
- The generator rejects negative map keys at compile time.
- Decoder tests: input with unsorted keys, duplicate keys, indefinite-length items
  or non-preferred floats is rejected.
- A golden fixture corpus of encoded records that must never change.

## Review triggers

- The IETF publishes a deterministic CBOR profile that conflicts with the chosen
  rules.
- A target platform lacks `System.Formats.Cbor`.
