# 0203. Store blobs as content-defined chunks

- Layer: L1 Storage
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0107](0107-address-content-with-multihash-sha-256.md): chunk addresses and
    Merkle trees.
  - [ADR-0200](0200-never-modify-stored-objects.md): chunks are never modified.
  - [ADR-0201](0201-choose-storage-codecs-by-content-type.md): the codec for each
    chunk.
  - [ADR-0202](0202-frame-stored-objects-with-codec-and-length.md): chunks are stored
    as frames.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Audio blobs are large and immutable. They should be deduplicated across all
projects on a device, synced partially and resumably, and repaired locally when
storage corrupts. They are stored as files or packfiles, never in a database.

Challenge to content-defined chunking (CDC): CDC pays off when files are edited by
inserting or removing data. Sources here are never edited. Most deduplication will
probably come from whole-file duplicates (the same sample used in many projects),
which fixed-size or whole-file hashing catches too. Chunking still helps with
resumable sync, partial fetch and Merkle verification, but any chunking scheme
provides those. The evidence must show whether CDC is worth its cost.

Other points:

- Many small chunk files put load on the filesystem (inode count, antivirus
  scanning on Windows). Packfiles reduce this.
- Chunk boundaries should fall on whole sample frames so that chunks can be decoded
  independently.

## Decision

Split blobs into chunks with FastCDC on sample-frame boundaries, with a target size
to be chosen from evidence. Address each chunk by its multihash and store it as a
frame, encoded with the codec for its content type. Each chunk is its own unit of
compression. Group frames into append-only packfiles with a separate index.
Packfiles are never compressed as a whole, so any chunk can be read without
decoding the chunks before it. Re-hash stored chunks on a schedule (integrity
scrubbing) and repair them from replicas.

## Alternatives considered

### Fixed-size chunks aligned to sample frames

It is simpler and cheaper, and chunks decode independently. Deduplication drops
only when bytes are inserted or removed, which immutable sources rarely do.

### Whole-file blobs

This is simplest. Large files cannot be partially synced or resumed, and one bad
byte invalidates the whole file.

### Blobs in a database

Transactions come built in. Databases handle multi-gigabyte blobs badly, and the
format becomes harder to read without the database.

## Consequences

### Positive

- Partial and resumable transfer, local repair, and deduplication.

### Negative and trade-offs

- Chunking costs CPU at import.
- The packfile format needs to be specified and compacted.

## Evidence

Available:

- Xia et al., "FastCDC", USENIX ATC 2016.

Required before acceptance:

- [ ] Measure the deduplication ratio on a real sample library plus projects:
      FastCDC vs. fixed-size vs. whole-file.
- [ ] BenchmarkDotNet: chunking throughput (managed, `ReadOnlySpan<byte>`).
- [ ] Choose the target chunk size, balancing index memory against transfer
      granularity.
- [ ] Specify the packfile layout.

## Fitness functions

- Integrity-scrub test: corrupting a chunk is detected and repaired from a replica.
- The chunker produces identical boundaries on every platform (test vectors).

## Review triggers

- The evidence shows that CDC gives no deduplication advantage over fixed-size
  chunks.

## Notes

- 2026-10-04: Renumbered from the former ADR-0014 when ADRs were grouped by layer.
