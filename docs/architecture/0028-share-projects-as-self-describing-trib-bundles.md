# 0028. Share projects as self-describing .trib bundles

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0005](0005-encode-persisted-data-as-deterministic-cbor.md): the manifest and
    schemas.
  - [ADR-0012](0012-frame-stored-objects-with-codec-and-length.md): store entries
    carry frames unchanged.
  - [ADR-0014](0014-store-blobs-as-content-defined-chunks.md): packfiles.
  - [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md): event
    segments.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Users share projects as a single file and archive them for decades. The bundle
must be readable using only its own contents and a text specification. A reader
must not need the specification in order to read the specification.

## Decision

Package a `.trib` bundle as a ZIP64 archive whose entries are all stored without
ZIP compression. It contains two kinds of entry:

- **Bootstrap entries**, written as plain files outside the object store: a
  plain-text format specification, the CDDL schemas, and the manifest as
  canonical CBOR.
- **Store entries:** event segments and chunk packfiles, copied byte for byte with
  their frames intact.

Exporting never re-encodes or recompresses data. Importing verifies every object.

## Alternatives considered

### tar

Readers exist everywhere. It has no central directory, so random access means
scanning the whole archive.

### IPLD CAR (v1/v2)

It is designed for content-addressed data. It is little known outside the IPFS
ecosystem.

### SQLite archive

It can be queried, but it is a native dependency, and long-term readability rests
on SQLite's file format.

### Custom container

It fits the data best, but it is one more format to specify and keep readable.

### ZIP with Deflate (or zstd, method 93) on each entry

Standard tools would decompress entries transparently. It recompresses data that
is already compressed, and stops chunks in a compressed packfile from being read
without decompressing everything before them. Few stock tools support method 93.

## Consequences

### Positive

- Standard OS tools can open it and read the bootstrap entries directly.
- Entries and the chunks inside packfiles can be read without scanning.
- Export and import are byte copies plus verification.

### Negative and trade-offs

- ZIP64 support varies in older tools.
- Getting audio out of a bundle needs the registered codecs, not only an unzip
  tool.

## Evidence

Required before acceptance:

- [ ] Open a bundle larger than 4 GB with the stock tools on Windows, macOS and
      Linux, and read the specification and schemas directly.
- [ ] Draft the text specification, including frames and the codec registry, and
      have someone else write a reader from it alone.

## Fitness functions

- Round trip: export, delete the project, import, and check that the result is
  bit-identical.
- CI validates every bundle against its embedded CDDL schemas.
- CI checks that every ZIP entry uses compression method 0 (stored).

## Review triggers

- Bundles must be streamed (as a pipe) more than accessed randomly.
