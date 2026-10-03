# 0011. Choose storage codecs by content type

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0002](0002-keep-the-core-managed-with-optional-native-providers.md): codecs
    need managed implementations, with native ones optional.
  - [ADR-0010](0010-hash-and-sign-uncompressed-canonical-bytes.md): codecs are
    applied after hashing.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Stored objects range from highly compressible CBOR records to audio that was
already compressed when imported. Compressing data that is already compressed
wastes CPU, and general-purpose compressors do poorly on PCM audio.

Facts:

- A lossless audio codec (FLAC, [RFC 9639](https://www.rfc-editor.org/rfc/rfc9639))
  compresses integer PCM much better than zstd
  ([RFC 8878](https://www.rfc-editor.org/rfc/rfc8878)). Both have IETF
  specifications.
- FLAC stores integer PCM up to 32 bits (libFLAC 1.4 or later; many decoders stop
  at 24 bits). It cannot store IEEE float samples.
- Pure managed zstd exists (for example ZstdSharp). Native libzstd is faster.

## Decision

Every object written to the store declares a content type. The codec is chosen
from that content type using a fixed policy table, without trial compression:

| Content type | Codec |
|---|---|
| Canonical CBOR records (any) | zstd |
| Integer PCM sample chunk | FLAC |
| Float PCM sample chunk | zstd (to be confirmed by evidence) |
| Already-compressed media (FLAC, MP3, AAC, Ogg imports; images; model weights) | raw |
| Unknown content type | raw |

The codec registry is append-only. Every version of Tributary must read every
codec ever written, and new codecs must have a published, open specification.
Imported files are typed by their container format, not their file extension.

## Alternatives considered

### Trial compression with a savings threshold

Compress every object and keep the result only if it saves more than a fixed
percentage. This needs no type information. It spends CPU compressing data that is
already compressed, can encode objects of the same type differently, and the
threshold is an arbitrary tuning value.

### One general-purpose codec for everything

It is simplest. PCM audio, the bulk of storage, compresses poorly.

## Consequences

### Positive

- No CPU is spent compressing data that is already compressed.
- Every object of the same content type is encoded the same way, which makes
  storage behaviour predictable and testable.

### Negative and trade-offs

- Codecs can never be removed, so every reader carries every historical decoder.
- Every write path must supply a correct content type. A mislabelled object is
  still stored correctly, but either wastes space or wastes CPU.
- The policy table has to be maintained as new content types appear.

## Evidence

Required before acceptance:

- [ ] Measure the compression ratio for each content type in the table on real
      data, in particular raw vs. zstd for model weights.
- [ ] Benchmark float PCM codecs (zstd, zstd with byte-plane shuffling, WavPack,
      raw) to settle the float row.
- [ ] Benchmark managed vs. native zstd and FLAC on target platforms.
- [ ] Define the content-type registry (identifiers, container-format detection).

## Fitness functions

- Test: every content type in the registry has an entry in the policy table, and
  the store API rejects writes that do not declare a content type.
- Test: a fixture object for every registered codec decodes in CI.

## Review triggers

- A codec in the registry loses maintained implementations.
- A new content type does not fit an existing codec.
