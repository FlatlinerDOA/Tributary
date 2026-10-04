# 0308. Keep the index disposable, without a database

- Layer: L2 Event model
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0100](0100-keep-the-core-managed-with-optional-native-providers.md): no
    native storage engine in the core.
  - [ADR-0203](0203-store-blobs-as-content-defined-chunks.md): the packfiles being
    indexed.
  - [ADR-0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md): the source
    the index is rebuilt from.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

The index maps addresses to packfile locations and supports projection queries.
It can always be rebuilt from the event DAG and the packfiles, so its archival
properties do not matter.

Memory is a concern: a 1 TB library cut into 64 KiB chunks has about 16 million
entries. At roughly 100 bytes per `Dictionary` entry (32-byte digest + location +
overhead), a fully in-memory index needs about 1.6 GB. Larger chunks or on-disk
sorted segments with Bloom filters may be required.

SQLite is often avoided on iOS. iOS ships SQLite as a system library, so the real
issue may be native bundling or AOT with SQLitePCLRaw. This needs confirming.

## Decision

Put the index behind an `IIndex` interface. Implement it first as append-only
on-disk segments, an in-memory index of recent segments, and periodic checkpoints.
Deleting the index is always safe: on startup the system rebuilds it from the DAG
and the packfiles.

## Alternatives considered

### SQLite

It is mature and can be queried. It is a native dependency, and its platform
issues are unconfirmed.

### ZoneTree (pure C# LSM tree)

It is managed and persistent. Its long-term maintenance is unproven.

### LMDB or RocksDB

They are fast and proven, but they are native dependencies.

## Consequences

### Positive

- No database dependency, and recovery is trivial.

### Negative and trade-offs

- Tributary has to write and maintain its own segment and checkpoint code.
- Ad hoc querying is limited.

## Evidence

Required before acceptance:

- [ ] Model index memory for 100 GB, 1 TB and 4 TB libraries at the chosen chunk
      size.
- [ ] Rebuild-time benchmark for a 1 TB store.
- [ ] Verify or withdraw the claim about SQLite on iOS.

## Fitness functions

- Test: delete the index, rebuild, and compare the bytes.

## Review triggers

- Profiling or query needs exceed what the custom index can do.

## Notes

- 2026-10-04: Renumbered from the former ADR-0025 when ADRs were grouped by layer.
