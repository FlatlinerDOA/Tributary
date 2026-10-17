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
- Spike: [`spikes/adr-0005-cbor/`](../../spikes/adr-0005-cbor/), with the full
  comparison in [`results/report.md`](../../spikes/adr-0005-cbor/results/report.md).
  It encodes 89 fixtures and 29 decoder cases with `System.Formats.Cbor` 10.0.0,
  `cbor2` 6.1.5, `ciborium` 0.2.2 and `dcbor` 0.25.2. They are checked against a
  reference encoder that reproduces every vector published in RFC 8949 Appendix A
  and draft-mcnally-deterministic-cbor-12. The results are reproducible byte for byte.

Required before acceptance:

- [x] Find out and document the key-ordering rule that `System.Formats.Cbor`'s
      conformance modes enforce, and whether they reject non-deterministic input.
      - `Canonical` sorts keys length-first (RFC 7049 §3.9), not bytewise, and its
        reader rejects the RFC 8949 order. `Ctap2Canonical` sorts bytewise but
        rejects tags. The two orders agree when every key in a map has the same
        major type. They differ only across major types (for example `24` and `-1`).
      - `Canonical` and `Ctap2Canonical` readers reject unsorted and duplicate keys,
        non-shortest integers and lengths, and indefinite lengths. No mode rejects
        non-preferred floats: 1.5 encoded as binary64 is accepted. The
        "readers reject non-conforming input" rule therefore needs a float check
        on top of the library.
      - In every mode except `Ctap2Canonical`, `WriteDouble` writes the shortest
        exact float and writes every NaN as `f97e00`.
- [ ] Choose a float profile (plain §4.2.1, CDE or dCBOR) and record why.
      Data for the choice:
      - CDE keeps -0.0 and NaN payloads, and leaves integral floats as floats.
        `ciborium` with keys sorted by hand matches CDE on every fixture. The
        `System.Formats.Cbor` writer differs from CDE only on NaN, which it
        canonicalises.
      - dCBOR reduces integral floats to integers, so a `double` field must decode
        from an integer. It also canonicalises NaN, requires NFC text, and limits
        negative integers to -2^63. Only one implementation was found (`dcbor`),
        and it departs from draft-12 on the lower integer bound.
      - A third option is CDE, with NaN either rejected or written only as
        `f97e00`. That matches the .NET writer.
- [ ] Encode a fixture set with Tributary and with at least one independent
      implementation (for example Python `cbor2` or Rust `ciborium`) and compare
      the bytes. The fixture set, the reference encoder and harnesses for three
      independent implementations exist. The Tributary encoder does not exist yet.

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
