# 0104. Identify entities with canonical string IDs

- Layer: L0 Primitives
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0101](0101-target-the-latest-dotnet-release.md): `Guid.CreateVersion7()`
    needs .NET 9 or later.
  - [ADR-0102](0102-encode-persisted-data-as-deterministic-cbor.md): IDs are encoded
    as CBOR text strings and checked by CDDL.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Entities (tracks, clips, buses, connections, sections) need identifiers that many
nodes can generate offline, with no coordination. Identifiers are persisted, so
they are permanent, and they are used as segments of structured identifiers such
as paths.

There are three kinds of identity:

- **Content addresses** ("what"): multihashes of stored objects.
- **Entity IDs** ("which"): covered by this ADR.
- **Actor IDs**: derived from public keys.

Facts:

- UUIDv7 ([RFC 9562](https://www.rfc-editor.org/rfc/rfc9562)) is a 48-bit Unix
  millisecond timestamp followed by 74 random bits. .NET 9 and later provide
  `Guid.CreateVersion7()`.
- `Guid.ToByteArray()` and the default `TryWriteBytes` use Microsoft's
  mixed-endian layout. `Guid.ToString("N")` prints in RFC order.
- The timestamp comes from the device clock, which can be wrong or deliberately
  forged.

## Decision

1. **Identifier segments are strings with one canonical spelling.** Every segment
   matches a strict grammar, for example lowercase ASCII `[a-z0-9._-]{1,64}` (to be
   finalized). Non-canonical input is rejected, never normalized. The grammar
   excludes every character reserved for patterns or separators.
2. **Generated entity IDs are UUIDv7 in lowercase hex.** They are 32 characters
   without dashes, produced from `Guid.CreateVersion7()` with `ToString("N")`. Hex
   keeps byte order, so sorted indexes and ranges group IDs by creation time.
3. **ID order carries no meaning.** Neither the order of IDs nor their timestamps
   are used for causality, tie-breaks or "which came first".
4. **Checks happen at two levels.** The schema enforces the grammar when decoding
   (CDDL `.regexp`), including input from peers. Domain code works only with
   strongly typed IDs (`readonly record struct TrackId`), which are created only
   by `TryParse` at the boundary.
5. **IDs are unique across the whole project.**
6. **External IDs are attributes, not identity.** IDs from DAWproject, other DAWs
   or other projects are stored as provenance. Copied or imported entities get
   newly generated IDs.

## Alternatives considered

### ULID

It has a shorter (26-character base32) text form and a similar layout. It is a
community specification with no IETF status, no BCL support and no CBOR tag.

### UUIDv4

It reveals nothing about creation time. IDs have no locality in sorted indexes or
ranges.

### 16-byte binary IDs (CBOR tag 37)

They are half the size. Identifiers become unreadable in logs and tools, the
mixed-endian `Guid` trap applies, and other identifier schemes (plugin IDs, fixed
names) cannot share the same segment type.

### IDs derived from the creating record (hash plus ordinal)

They are unique without randomness and tied to where the entity came from. The ID
is unknown until the record is hashed, so a client cannot create an entity offline
and refer to it straight away. They are also 32 bytes.

### Snowflake-style IDs (timestamp + node + counter)

They are compact and ordered. They need assigned node IDs, which requires
coordination.

## Consequences

### Positive

- Identifiers are readable in logs, the CLI and a hex dump.
- Fixed names, plugin IDs and generated IDs share one segment type.

### Negative and trade-offs

- Text IDs are about twice the size of binary ones. Compression of CBOR records
  removes most of the difference.
- UUIDv7 reveals creation time wherever IDs are visible.

## Evidence

Required before acceptance:

- [ ] Finalize the segment grammar and maximum length. Test it against plugin ID
      formats (CLAP reverse-domain IDs, VST3 class IDs).
- [ ] Measure the locality benefit of v7 over v4 for sorted indexes.
- [ ] Test `Guid.CreateVersion7()` under bursts and confirm that nothing depends
      on order within a millisecond.
- [ ] Measure record size with text vs. binary IDs after compression.

## Fitness functions

- A CDDL schema test rejects segments outside the grammar, including uppercase,
  Unicode and reserved characters.
- An analyzer or architecture test bans `Guid.ToByteArray()` and the
  little-endian `TryWriteBytes` in persisted code paths.
- A property test checks that `TryParse` and formatting round-trip for every typed
  ID.

## Review triggers

- ID size becomes significant in storage or transfer.
- A need appears for IDs that are not canonical text, such as user-chosen names.

## Notes

- 2026-10-04: Renumbered from the former ADR-0016 when ADRs were grouped by layer.
