# Architecture decision log

## Purpose
This directory records architecturally significant decisions for the project. 
Each accepted record is intended to describe one decision, its context, alternatives 
considered, consequences, evidence, and conditions that should cause a review.

## Index

ADRs are numbered in dependency order: each record builds only on records with a
lower number, listed in its `Depends on` header entry (see
[ADR-0000](0000-record-architecture-decisions.md)).

| ADR | Area | Status | Decision |
|----:|------|--------|----------|
| [0000](0000-record-architecture-decisions.md) | Governance | Proposed | Record decisions as Markdown ADRs, numbered in dependency order, one owner per rule, linking only to earlier records. |
| [0001](0001-drive-the-engine-through-one-typed-api.md) | Foundations | Proposed | Drive the headless engine through one typed API used by every client. |
| [0002](0002-keep-the-core-managed-with-optional-native-providers.md) | Foundations | Proposed | Keep the core managed; allow native code only behind interfaces. |
| [0003](0003-target-the-latest-dotnet-release.md) | Foundations | Proposed | Target the latest GA .NET release (.NET 11 from November 2026); libraries also target the current LTS. |
| [0004](0004-treat-audio-sources-as-immutable.md) | Foundations | Proposed | Never modify stored audio; resolve edits as render-time transforms. |
| [0005](0005-encode-persisted-data-as-deterministic-cbor.md) | Encoding | Proposed | Encode persisted data with RFC 8949 deterministic CBOR. |
| [0006](0006-evolve-schemas-additively.md) | Encoding | Proposed | Evolve event schemas only additively; projections may change freely if rendered output is unchanged. |
| [0007](0007-make-api-serialization-pluggable.md) | Encoding | Proposed | Define the API contract independently of encoding; CBOR, Protobuf and JSON are codec plugins. |
| [0008](0008-provide-cryptography-through-a-managed-first-interface.md) | Integrity | Proposed | Put crypto behind interfaces with a managed default provider. |
| [0009](0009-address-content-with-multihash-sha-256.md) | Integrity | Proposed | Address content with multihash-prefixed SHA-256. |
| [0010](0010-hash-and-sign-uncompressed-canonical-bytes.md) | Integrity | Proposed | Hash and sign the uncompressed canonical bytes; never re-encode to verify. |
| [0011](0011-choose-storage-codecs-by-content-type.md) | Storage | Proposed | Choose each object's codec from its declared content type. |
| [0012](0012-frame-stored-objects-with-codec-and-length.md) | Storage | Proposed | Store objects as frames with codec and length headers; copy frames byte for byte. |
| [0013](0013-sign-with-recoverable-identity-keys.md) | Identity | Proposed | Sign with device and agent keys certified by a SLIP-39-recoverable root. |
| [0014](0014-store-blobs-as-content-defined-chunks.md) | Storage | Proposed | Store blobs as FastCDC chunks in packfiles, with integrity scrubbing. |
| [0015](0015-decompose-imported-audio-into-samples-metadata-and-recipe.md) | Storage | Proposed | Split imported audio into samples, metadata and a byte-exact rebuild recipe. |
| [0016](0016-identify-entities-with-canonical-string-ids.md) | Data model | Proposed | Use canonical string IDs; generate UUIDv7 as lowercase hex. |
| [0017](0017-key-aggregates-by-hierarchical-paths.md) | Data model | Proposed | Key aggregates by hierarchical paths; keep relationships in their own aggregates. |
| [0018](0018-select-paths-with-one-pattern-language.md) | Data model | Proposed | Use one path-pattern language wherever paths are selected. |
| [0019](0019-authorize-actors-with-path-scoped-capabilities.md) | Identity | Proposed | Authorize every actor with delegated capabilities scoped by path pattern. |
| [0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md) | Data model | Proposed | Keep commands local; make a DAG of signed events the source of truth. |
| [0021](0021-merge-non-conflicting-edits-implicitly.md) | Data model | Proposed | Define state at a set of heads as a deterministic union; store no merge nodes. |
| [0022](0022-detect-conflicts-deterministically.md) | Data model | Proposed | Detect conflicts by path overlap and declared invariants; keep conflicts as state. |
| [0023](0023-resolve-conflicts-with-semantic-events.md) | Data model | Proposed | Resolve conflicts with typed events recording intent and explicit decisions. |
| [0024](0024-derive-reactions-and-subscriptions-from-projections.md) | Data model | Proposed | Compute reactions as projections; subscribe to projected changes by path. |
| [0025](0025-keep-the-index-disposable-without-a-database.md) | Storage | Proposed | Keep the index disposable, using segments and checkpoints instead of a database. |
| [0026](0026-sync-peers-by-range-based-set-reconciliation.md) | Replication | Proposed | Sync the event DAG and chunks by range-based set reconciliation. |
| [0027](0027-encrypt-data-only-when-it-leaves-the-device.md) | Replication | Proposed | Keep the local store unencrypted; encrypt data replicated to untrusted peers. |
| [0028](0028-share-projects-as-self-describing-trib-bundles.md) | Replication | Proposed | Package bundles as stored-only ZIP64: plain bootstrap files plus byte-copied frames. |
| [0029](0029-develop-the-data-layer-as-a-separable-library.md) | Organisation | Proposed | Build the data layer as separate packages in-repo until it has a second consumer. |
| [0030](0030-implement-the-realtime-engine-in-csharp.md) | Engine | Proposed | Write the RT engine in C#, depending on a GC spike; fall back to a Rust kernel. |
| [0031](0031-swap-immutable-graph-snapshots-to-the-realtime-thread.md) | Engine | Proposed | Compile immutable graph snapshots from projections and swap them atomically. |
| [0032](0032-route-realtime-gestures-through-a-fast-path.md) | Engine | Proposed | Send gestures straight to the engine; record them later as coalesced commands. |
| [0033](0033-compose-warp-into-one-time-map-per-clip.md) | Engine | Proposed | Anchor clips to absolute time by default; compose warp into one time map per clip, with a two-tier render cache. |
| [0034](0034-host-clap-plugins-first-out-of-process.md) | Engine | Proposed | Host CLAP first, in sandboxed out-of-process hosts. |
| [0035](0035-schedule-ml-inference-as-two-tier-graph-nodes.md) | Engine | Proposed | Run ML as RT CPU nodes or as frozen background GPU jobs. |
| [0036](0036-guarantee-longevity-through-degradation-tiers.md) | Longevity | Proposed | Degrade through three longevity tiers, with print insurance. |
| [0037](0037-deliver-generated-content-as-branch-proposals.md) | Collaboration | Proposed | Deliver generated content as branch proposals accepted by resolution events. |
| [0038](0038-timestamp-api-messages-in-declared-clock-domains.md) | Engine | Proposed | Timestamp API messages in wall, engine or musical clock domains, with offset estimation per session. |
| [0039](0039-generate-schemas-from-csharp-types.md) | Encoding | Proposed | Generate CBOR mappers and archived CDDL from C# types; verify with Rust and Python. |
| [0040](0040-integrate-aggregates-through-projections-and-application-operations.md) | Data model | Proposed | Keep deciders inside their aggregate; evaluate rules across aggregates in projections; clients use application operations. |
| [0041](0041-render-and-store-graph-sections-with-explicit-filters.md) | Engine | Proposed | Render before every audio sink and store as a separate synced filter; compilation adds no nodes. |
| [0042](0042-preview-experiments-in-local-draft-overlays.md) | Data model | Proposed | Preview experiments in local, unsynced draft overlays; commit as signed events or promote to a branch. |

## Governance

- Use [the template](template.md) for new decisions.
- One ADR records one architecturally significant decision.
- Accepted records are append-only except for status and clearly dated notes.
- A replacement decision must create a new ADR and link both directions with `Supersedes` and `Superseded by`.
- ADRs stand alone: they state their own context and never rely on planning documents outside this directory.
- An ADR links only to lower-numbered ADRs, through the `Depends on` entry in its header and in its text.
- Each rule has one owning ADR; other records link to it rather than restating it.
- Evidence should point to implementation, tests, protocol captures, or published project documentation. Do not claim hardware safety from code structure alone.
- A decision that cannot be checked automatically should state the manual or hardware validation required.
