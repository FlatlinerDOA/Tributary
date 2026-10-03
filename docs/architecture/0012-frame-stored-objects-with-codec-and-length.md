# 0012. Frame stored objects with codec and length

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0010](0010-hash-and-sign-uncompressed-canonical-bytes.md): the address
    covers the decoded bytes, not the frame.
  - [ADR-0011](0011-choose-storage-codecs-by-content-type.md): the codec registry the
    header identifies.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Stored objects travel unchanged between the local store, peers and exported
bundles. A reader needs to know which codec encoded an object and how large it will
be once decoded. Objects received from peers are decoded before they can be
verified, so a malicious peer could send a decompression bomb.

## Decision

Store every object as a frame: a header holding the codec identifier and the
uncompressed length, followed by the encoded payload. The header is not part of
the content address. Readers check the declared uncompressed length against a
configured limit before decoding, and reject objects over the limit. Frames are
copied byte for byte wherever objects travel. Re-encoding is allowed only as a
deliberate repack, which never changes an address.

## Alternatives considered

### Compression applied by each container (ZIP Deflate in bundles, stream compression in sync)

Each container could rely on standard tools. The policy would be duplicated in
each consumer, every export or transfer would re-encode, and data inside a
compressed container entry cannot be read from the middle.

### Self-describing payloads with no header (detect the codec from magic bytes)

There is no header. Detection is ambiguous for raw payloads, and the decoded size
is unknown until decoding, so bombs cannot be rejected up front.

## Consequences

### Positive

- One encoding everywhere: transfer and export are byte copies plus verification.
- Oversized objects are rejected before any decoding work.

### Negative and trade-offs

- The frame format is permanent and must be specified in plain text.

## Evidence

Required before acceptance:

- [ ] Specify the frame header (codec ID encoding, uncompressed-length encoding,
      versioning).
- [ ] Choose the default decompression size limit and how it is configured.

## Fitness functions

- Test: an object whose declared uncompressed length exceeds the limit is rejected
  without being decoded.
- Test: a frame survives a round trip through the store, a sync transfer and a
  bundle export byte for byte.

## Review triggers

- A codec needs parameters that do not fit in the header.
