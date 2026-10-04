# Architecture decision log

## Purpose
This directory records architecturally significant decisions for the project.
Each accepted record is intended to describe one decision, its context, alternatives
considered, consequences, evidence, and conditions that should cause a review.

## Layers

ADRs are grouped into layers, and the first two digits of an ADR's number are its
layer (see [ADR-0000](0000-record-architecture-decisions.md)). An ADR depends only
on ADRs in its own layer or a lower one, and within a layer only on lower-numbered
ADRs. Layers L0 to L3 form the data layer library and know nothing about audio.

```
L7  Product features   0800s
L6  Host and API       0700s
L5  Engine             0600s
L4  Domain model       0500s
──── data layer library below: no audio or DAW concepts ────
L3  Replication        0400s
L2  Event model        0300s
L1  Storage            0200s
L0  Primitives         0100s
```

## Index

### Governance

| ADR | Status | Decision |
|----:|--------|----------|
| [0000](0000-record-architecture-decisions.md) | Proposed | Record decisions as Markdown ADRs grouped into layers with number ranges; depend only on the same or lower layers; one owner per rule. |

### L0 Primitives

| ADR | Status | Decision |
|----:|--------|----------|
| [0100](0100-keep-the-core-managed-with-optional-native-providers.md) | Proposed | Keep the core managed; allow native code only behind interfaces. |
| [0101](0101-target-the-latest-dotnet-release.md) | Proposed | Target the latest GA .NET release (.NET 11 from November 2026); libraries also target the current LTS. |
| [0102](0102-encode-persisted-data-as-deterministic-cbor.md) | Proposed | Encode persisted data with RFC 8949 deterministic CBOR. |
| [0103](0103-evolve-schemas-additively.md) | Proposed | Evolve event schemas only additively; projections may change freely if rendered output is unchanged. |
| [0104](0104-identify-entities-with-canonical-string-ids.md) | Proposed | Use canonical string IDs; generate UUIDv7 as lowercase hex. |
| [0105](0105-generate-schemas-from-csharp-types.md) | Proposed | Generate CBOR mappers and archived CDDL from C# types; verify with Rust and Python. |
| [0106](0106-provide-cryptography-through-a-managed-first-interface.md) | Proposed | Put crypto behind interfaces with a managed default provider. |
| [0107](0107-address-content-with-multihash-sha-256.md) | Proposed | Address content with multihash-prefixed SHA-256. |
| [0108](0108-hash-and-sign-uncompressed-canonical-bytes.md) | Proposed | Hash and sign the uncompressed canonical bytes; never re-encode to verify. |
| [0109](0109-sign-with-recoverable-identity-keys.md) | Proposed | Sign with device and agent keys certified by a SLIP-39-recoverable root. |

### L1 Storage

| ADR | Status | Decision |
|----:|--------|----------|
| [0200](0200-never-modify-stored-objects.md) | Proposed | Never modify a stored object; a change creates a new object with a new address. |
| [0201](0201-choose-storage-codecs-by-content-type.md) | Proposed | Choose each object's codec from its declared content type. |
| [0202](0202-frame-stored-objects-with-codec-and-length.md) | Proposed | Store objects as frames with codec and length headers; copy frames byte for byte. |
| [0203](0203-store-blobs-as-content-defined-chunks.md) | Proposed | Store blobs as FastCDC chunks in packfiles, with integrity scrubbing. |

### L2 Event model

| ADR | Status | Decision |
|----:|--------|----------|
| [0300](0300-key-aggregates-by-hierarchical-paths.md) | Proposed | Key aggregates by hierarchical paths; keep relationships in their own aggregates. |
| [0301](0301-select-paths-with-one-pattern-language.md) | Proposed | Use one path-pattern language wherever paths are selected. |
| [0302](0302-authorize-actors-with-path-scoped-capabilities.md) | Proposed | Authorize every actor with delegated capabilities scoped by path pattern. |
| [0303](0303-make-a-dag-of-signed-events-the-source-of-truth.md) | Proposed | Keep commands local; make a DAG of signed events the source of truth. |
| [0304](0304-merge-non-conflicting-edits-implicitly.md) | Proposed | Define state at a set of heads as a deterministic union; store no merge nodes. |
| [0305](0305-detect-conflicts-deterministically.md) | Proposed | Detect conflicts by path overlap and declared invariants; keep conflicts as state. |
| [0306](0306-resolve-conflicts-with-semantic-events.md) | Proposed | Resolve conflicts with typed events recording intent and explicit decisions. |
| [0307](0307-derive-reactions-and-subscriptions-from-projections.md) | Proposed | Compute reactions as projections; subscribe to projected changes by path. |
| [0308](0308-keep-the-index-disposable-without-a-database.md) | Proposed | Keep the index disposable, using segments and checkpoints instead of a database. |
| [0309](0309-evaluate-rules-that-span-aggregates-in-projections.md) | Proposed | Keep deciders inside their aggregate; evaluate rules that span aggregates in projections and make violations inert. |
| [0310](0310-hold-uncommitted-edits-in-local-draft-overlays.md) | Proposed | Hold uncommitted edits in local, unsynced draft overlays; commit as signed events or promote to a branch. |

### L3 Replication

| ADR | Status | Decision |
|----:|--------|----------|
| [0400](0400-sync-peers-by-range-based-set-reconciliation.md) | Proposed | Sync the event DAG and chunks by range-based set reconciliation. |
| [0401](0401-encrypt-data-only-when-it-leaves-the-device.md) | Proposed | Keep the local store unencrypted; encrypt data replicated to untrusted peers. |
| [0402](0402-share-projects-as-self-describing-trib-bundles.md) | Proposed | Package bundles as stored-only ZIP64: plain bootstrap files plus byte-copied frames. |
| [0403](0403-develop-the-data-layer-as-a-separable-library.md) | Proposed | Build the data layer (L0–L3) as separate packages in-repo until it has a second consumer. |

### L4 Domain model

| ADR | Status | Decision |
|----:|--------|----------|
| [0500](0500-lay-out-the-project-as-a-path-tree.md) | Proposed | Lay out the project as a path tree: tempo, key, arrangement, tracks, buses, routing and modulation. |
| [0501](0501-resolve-audio-edits-as-render-time-transforms.md) | Proposed | Resolve audio edits as render-time transforms of immutable sources; a render pass creates a new source. |
| [0502](0502-decompose-imported-audio-into-samples-metadata-and-recipe.md) | Proposed | Split imported audio into samples, metadata and a byte-exact rebuild recipe. |
| [0503](0503-compose-warp-into-one-time-map-per-clip.md) | Proposed | Anchor clips to absolute time by default; compose every warp into one time map per clip. |
| [0504](0504-model-render-and-store-as-explicit-graph-filters.md) | Proposed | Render before every audio sink; keep rendered audio through a separate, synced store filter. |

### L5 Engine

| ADR | Status | Decision |
|----:|--------|----------|
| [0600](0600-implement-the-realtime-engine-in-csharp.md) | Proposed | Write the RT engine in C#, depending on a GC spike; fall back to a Rust kernel. |
| [0601](0601-swap-immutable-graph-snapshots-to-the-realtime-thread.md) | Proposed | Compile immutable graph snapshots from projections, adding no nodes, and swap them atomically. |
| [0602](0602-stretch-in-real-time-and-cache-renders-by-hash.md) | Proposed | Stretch in real time and swap in high-quality renders; cache renders locally by hash. |
| [0603](0603-host-clap-plugins-first-out-of-process.md) | Proposed | Host CLAP first, in sandboxed out-of-process hosts that the engine never starts. |
| [0604](0604-schedule-ml-inference-as-two-tier-graph-nodes.md) | Proposed | Run ML as RT CPU nodes or as background GPU jobs whose output is stored. |

### L6 Host and API

| ADR | Status | Decision |
|----:|--------|----------|
| [0700](0700-drive-the-engine-through-one-typed-api.md) | Proposed | Drive the headless engine through one typed API used by every client. |
| [0701](0701-make-api-serialization-pluggable.md) | Proposed | Define the API contract independently of encoding; CBOR, Protobuf and JSON are codec plugins. |
| [0702](0702-expose-application-operations-to-clients.md) | Proposed | Give clients queries, property sets, intent operations and all-or-nothing batches, never aggregate commands. |
| [0703](0703-route-realtime-gestures-through-a-fast-path.md) | Proposed | Send gestures to the engine without the command layer; record them later as coalesced commands. |
| [0704](0704-timestamp-api-messages-in-declared-clock-domains.md) | Proposed | Timestamp API messages in wall, engine or musical clock domains, with offset estimation per session. |
| [0705](0705-run-one-host-per-user-with-the-engine-in-its-own-process.md) | Proposed | One host per user serves all clients, supervises the engine and plugin processes, and owns the audio device; in-process on mobile. |

### L7 Product features

| ADR | Status | Decision |
|----:|--------|----------|
| [0800](0800-guarantee-longevity-through-degradation-tiers.md) | Proposed | Degrade through three longevity tiers, with print insurance. |
| [0801](0801-deliver-generated-content-as-branch-proposals.md) | Proposed | Deliver generated content as branch proposals accepted by resolution events. |

## Governance

- Use [the template](template.md) for new decisions.
- One ADR records one architecturally significant decision.
- Accepted records are append-only except for status and clearly dated notes.
- A replacement decision must create a new ADR and link both directions with `Supersedes` and `Superseded by`.
- ADRs stand alone: they state their own context and never rely on planning documents outside this directory.
- An ADR depends only on its own or lower layers, and links only to lower-numbered ADRs, through the `Depends on` entry in its header and in its text.
- Each rule has one owning ADR; other records link to it rather than restating it. A rule that spans layers is split so each layer owns its part.
- Evidence should point to implementation, tests, protocol captures, or published project documentation. Do not claim hardware safety from code structure alone.
- A decision that cannot be checked automatically should state the manual or hardware validation required.
